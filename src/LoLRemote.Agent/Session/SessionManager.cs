using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using LoLRemote.Agent.Capture;
using LoLRemote.Agent.Core.Input;
using LoLRemote.Agent.Core.Protocol;
using LoLRemote.Agent.Input;
using LoLRemote.Agent.League;
using LoLRemote.Agent.Platform;
using LoLRemote.Agent.Video;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;

namespace LoLRemote.Agent.Session;

/// <summary>
/// Sessão WebRTC com o celular: negocia a conexão, recebe toques pelo canal
/// "control", valida cada um (Agent.Core), injeta o clique e responde com
/// input.ack. Também envia o estado sempre que ele muda. Uma sessão por vez.
/// </summary>
internal sealed class SessionManager : IAsyncDisposable
{
    public const string ControlChannel = "control";
    private const long CaptureFreshMs = 5000;
    private const long StateHeartbeatMs = 5000;

    private readonly TargetWindow _target;
    private readonly FrameSource _frames;
    private readonly VideoStreamer _streamer;
    private readonly PhaseMonitor _phase;
    private readonly WindowsInputInjector _injector;
    private readonly IPAddress _bindAddress;
    private readonly long _remoteModeDeadlineMs;
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _stop = new();
    private ActiveSession? _current;
    private Task? _stateLoop;
    private string? _lastConsoleState;

    public SessionManager(
        TargetWindow target,
        FrameSource frames,
        VideoStreamer streamer,
        PhaseMonitor phase,
        WindowsInputInjector injector,
        IPAddress bindAddress,
        TimeSpan remoteMode)
    {
        _target = target;
        _frames = frames;
        _streamer = streamer;
        _phase = phase;
        _injector = injector;
        _bindAddress = bindAddress;
        _remoteModeDeadlineMs = ServerClock.NowMs + (long)remoteMode.TotalMilliseconds;
    }

    public void Start() => _stateLoop = Task.Run(() => StateLoopAsync(_stop.Token));

    /// <summary>Negocia uma nova sessão; retorna o SDP de resposta ou null se a oferta for inválida.</summary>
    public async Task<string?> CreateSessionAsync(string offer)
    {
        var peer = CreatePeer();
        var result = peer.setRemoteDescription(new RTCSessionDescriptionInit { sdp = offer, type = RTCSdpType.offer });
        if (result != SetDescriptionResultEnum.OK)
        {
            peer.Close("oferta inválida");
            Console.WriteLine($"Oferta recusada: {result}");
            return null;
        }

        var answer = peer.createAnswer();
        if (answer is null)
        {
            peer.Close("sem resposta SDP");
            return null;
        }

        await peer.setLocalDescription(answer).ConfigureAwait(false);
        return AddPictureLossFeedback(peer.localDescription?.sdp?.ToString() ?? answer.sdp);
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        if (_stateLoop is not null)
        {
            try
            {
                await _stateLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Encerramento normal.
            }
        }

        _stop.Dispose();
    }

    /// <summary>Estado de bloqueio atual, na ordem de segurança do Agent.Core.</summary>
    private InputGateState CurrentGate()
    {
        var target = _target.Describe();
        var latest = _frames.Latest;
        return new InputGateState(
            TargetValid: target.Valid,
            TargetMinimized: target.Minimized,
            CaptureFresh: latest is not null && ServerClock.NowMs - latest.CapturedAtMs <= CaptureFreshMs,
            Phase: _phase.Current,
            LocalActivity: _injector.LocalActivityDetected(),
            RemoteModeActive: ServerClock.NowMs < _remoteModeDeadlineMs);
    }

    private RTCPeerConnection CreatePeer()
    {
        var peer = new RTCPeerConnection(new RTCConfiguration
        {
            X_BindAddress = _bindAddress,
            X_UseRtpFeedbackProfile = true,
        });
        var h264 = new VideoFormat(
            VideoCodecsEnum.H264,
            100,
            VideoFormat.DEFAULT_CLOCK_RATE,
            "packetization-mode=1;profile-level-id=42e01f");
        peer.addTrack(new MediaStreamTrack(new List<VideoFormat> { h264 }, MediaStreamStatusEnum.SendOnly));

        var session = new ActiveSession(peer);

        peer.OnReceiveReport += (_, media, report) =>
        {
            var header = report.Feedback?.Header;
            if (media == SDPMediaTypesEnum.video
                && header is not null
                && header.PacketType == RTCPReportTypesEnum.PSFB
                && header.PayloadFeedbackMessageType is PSFBFeedbackTypesEnum.PLI or PSFBFeedbackTypesEnum.FIR)
            {
                _streamer.RequestKeyFrame();
            }
        };

        peer.ondatachannel += channel =>
        {
            if (channel.label != ControlChannel)
            {
                channel.close();
                return;
            }

            session.Channel = channel;
            channel.onopen += () => SendState(session, force: true);
            channel.onmessage += (_, _, data) => OnControlMessage(session, data);
            if (channel.readyState == RTCDataChannelState.open)
            {
                SendState(session, force: true);
            }
        };

        peer.onconnectionstatechange += state =>
        {
            Console.WriteLine($"Conexão com o celular: {Describe(state)}");
            switch (state)
            {
                case RTCPeerConnectionState.connected:
                    ActiveSession? previous;
                    lock (_gate)
                    {
                        previous = _current;
                        _current = session;
                    }

                    if (previous is not null && !ReferenceEquals(previous, session))
                    {
                        previous.Peer.Close("nova sessão");
                    }

                    _streamer.Attach(peer);
                    break;
                case RTCPeerConnectionState.failed:
                case RTCPeerConnectionState.disconnected:
                case RTCPeerConnectionState.closed:
                    _streamer.Detach(peer);
                    lock (_gate)
                    {
                        if (ReferenceEquals(_current, session))
                        {
                            _current = null;
                        }
                    }

                    if (state == RTCPeerConnectionState.failed)
                    {
                        peer.Close("falha de conexão");
                    }

                    break;
            }
        };

        return peer;
    }

    private void OnControlMessage(ActiveSession session, byte[] data)
    {
        if (data.Length > ControlMessageSerializer.MaxMessageLength
            || !ControlMessageSerializer.TryParse(Encoding.UTF8.GetString(data), out var message)
            || message is not TapMessage tap)
        {
            return;
        }

        InputRejectReason? reason;
        lock (session.InputLock)
        {
            reason = HandleTap(session, tap);
        }

        // Nunca registrar coordenadas: só a sequência e o resultado.
        Console.WriteLine(reason is null
            ? $"Toque #{tap.Sequence}: clique executado."
            : $"Toque #{tap.Sequence}: recusado ({reason}).");

        Send(session, new InputAckMessage
        {
            Version = InputLimits.ProtocolVersion,
            Sequence = tap.Sequence,
            Status = reason is null ? AckStatus.Accepted : AckStatus.Rejected,
            Reason = reason,
        });
    }

    private InputRejectReason? HandleTap(ActiveSession session, TapMessage tap)
    {
        var layout = _streamer.LastLayout;
        if (layout is null)
        {
            return InputRejectReason.CaptureStale;
        }

        var command = new TapCommand(tap.Version, tap.Sequence, tap.SentAtMs, new(tap.X, tap.Y));
        var decision = session.Validator.Validate(
            command,
            ServerClock.NowMs,
            CurrentGate(),
            layout,
            _target.Describe().ClientAreaInFrame);
        if (!decision.Accepted)
        {
            return decision.Reason;
        }

        return _injector.Click(_target, decision.Point!.Value) switch
        {
            InjectionResult.Clicked => null,
            InjectionResult.TargetUnavailable => InputRejectReason.TargetUnavailable,
            InjectionResult.TargetMinimized => InputRejectReason.TargetMinimized,
            _ => InputRejectReason.TargetObscured,
        };
    }

    private async Task StateLoopAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
        {
            var state = BuildState();
            var consoleState = state.InputAllowed ? $"{state.Phase}: controle liberado" : $"{state.Phase}: bloqueado ({state.Reason})";
            if (consoleState != _lastConsoleState)
            {
                _lastConsoleState = consoleState;
                Console.WriteLine($"Estado: {consoleState} | {_phase.Status}");
            }

            ActiveSession? session;
            lock (_gate)
            {
                session = _current;
            }

            if (session is not null)
            {
                SendState(session, force: false, state);
            }
        }
    }

    private StateMessage BuildState()
    {
        var gate = CurrentGate();
        var reason = InputGate.Evaluate(gate);
        return new StateMessage
        {
            Version = InputLimits.ProtocolVersion,
            Phase = gate.Phase,
            InputAllowed = reason is null,
            Reason = reason,
        };
    }

    private void SendState(ActiveSession session, bool force, StateMessage? state = null)
    {
        var json = ControlMessageSerializer.Serialize(state ?? BuildState());
        var now = ServerClock.NowMs;
        lock (session.StateLock)
        {
            if (!force && json == session.LastStateJson && now - session.LastStateSentMs < StateHeartbeatMs)
            {
                return;
            }

            session.LastStateJson = json;
            session.LastStateSentMs = now;
        }

        SendRaw(session, json);
    }

    private static void Send(ActiveSession session, ControlMessage message) =>
        SendRaw(session, ControlMessageSerializer.Serialize(message));

    private static void SendRaw(ActiveSession session, string json)
    {
        var channel = session.Channel;
        if (channel?.readyState == RTCDataChannelState.open)
        {
            channel.send(json);
        }
    }

    private static string Describe(RTCPeerConnectionState state) => state switch
    {
        RTCPeerConnectionState.connected => "conectado",
        RTCPeerConnectionState.connecting => "conectando",
        RTCPeerConnectionState.disconnected => "desconectado",
        RTCPeerConnectionState.failed => "falhou",
        RTCPeerConnectionState.closed => "encerrado",
        _ => state.ToString(),
    };

    // O SIPSorcery só anuncia transport-cc; sem "nack pli" o Safari não pede
    // quadros-chave (ADR 0002).
    private static string AddPictureLossFeedback(string sdp)
    {
        var result = new StringBuilder();
        foreach (var line in sdp.Split("\r\n"))
        {
            result.Append(line).Append("\r\n");
            var match = Regex.Match(line, @"^a=rtpmap:(\d+) H264/90000", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            if (match.Success && !sdp.Contains($"a=rtcp-fb:{match.Groups[1].Value} nack pli", StringComparison.Ordinal))
            {
                result.Append("a=rtcp-fb:").Append(match.Groups[1].Value).Append(" nack pli\r\n");
            }
        }

        return result.ToString().TrimEnd('\r', '\n') + "\r\n";
    }

    private sealed class ActiveSession(RTCPeerConnection peer)
    {
        public RTCPeerConnection Peer { get; } = peer;

        public InputValidator Validator { get; } = new();

        public Lock InputLock { get; } = new();

        public Lock StateLock { get; } = new();

        public RTCDataChannel? Channel { get; set; }

        public string? LastStateJson { get; set; }

        public long LastStateSentMs { get; set; }
    }
}

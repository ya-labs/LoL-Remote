using System.Numerics;
using System.Runtime.InteropServices;
using LoLRemote.Agent.Capture;
using LoLRemote.Agent.Core.Geometry;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.FFmpeg;

namespace LoLRemote.Agent.Video;

/// <summary>
/// Envia o frame mais recente a uma taxa fixa: redimensiona para 1280x720 com
/// faixas pretas, carimba a hora do servidor em um código de barras, codifica em
/// H.264 e entrega à conexão WebRTC ativa.
/// </summary>
internal sealed class VideoStreamer : IAsyncDisposable
{
    public const int Width = 1280;
    public const int Height = 720;
    public const int Fps = 15;

    // Código de barras: 4 bits de início (1010), 32 bits de hora e 4 de checksum.
    public const int StampBlock = 16;
    public const int StampBits = 40;
    public const int StampX = 4;
    public const int StampY = Height - StampBlock - 4;

    private const uint RtpDuration = 90_000 / Fps;
    private const uint White = 0xFFFFFFFF;
    private const uint Black = 0xFF000000;

    private readonly FrameSource _source;
    private readonly bool _latencyStamp;
    // Quadro-chave a cada 15 quadros (1 s): após perda de pacotes, o vídeo se
    // recupera em no máximo 1 s mesmo sem pedido explícito do iPhone.
    private readonly FFmpegVideoEncoder _encoder = new(new Dictionary<string, string>
    {
        ["x264-params"] = "keyint=15:min-keyint=15:scenecut=0",
    });
    private readonly uint[] _output = new uint[Width * Height];
    private readonly byte[] _outputBytes = new byte[Width * Height * 4];
    private readonly CancellationTokenSource _stop = new();
    private readonly Lock _gate = new();
    private int[] _xMap = [];
    private int _mapSourceWidth;
    private int _mapTargetWidth;
    private RTCPeerConnection? _peer;
    private VideoLayout? _lastLayout;
    private Task? _loop;
    private long _framesSent;
    private long _bytesSent;
    private long _encodeMicros;
    private long _keyFramesRequested;
    private long _lastKeyFrameRequestMs = long.MinValue / 2;

    public VideoStreamer(FrameSource source, bool latencyStamp)
    {
        _source = source;
        _latencyStamp = latencyStamp;
    }

    public long FramesSent => Interlocked.Read(ref _framesSent);

    public long BytesSent => Interlocked.Read(ref _bytesSent);

    /// <summary>Tempo médio de composição + codificação por frame, em ms.</summary>
    public double AverageEncodeMs
    {
        get
        {
            var frames = FramesSent;
            return frames == 0 ? 0 : Interlocked.Read(ref _encodeMicros) / 1000.0 / frames;
        }
    }

    public string? LastError { get; private set; }

    /// <summary>Encaixe do último frame enviado; é a referência para converter toques.</summary>
    public VideoLayout? LastLayout => Volatile.Read(ref _lastLayout);

    /// <summary>Quadros-chave pedidos pelo iPhone (PLI/FIR) e atendidos.</summary>
    public long KeyFramesRequested => Interlocked.Read(ref _keyFramesRequested);

    /// <summary>Atende pedido de quadro-chave, no máximo um a cada 300 ms.</summary>
    public void RequestKeyFrame()
    {
        var now = ServerClock.NowMs;
        var last = Interlocked.Read(ref _lastKeyFrameRequestMs);
        if (now - last < 300 || Interlocked.CompareExchange(ref _lastKeyFrameRequestMs, now, last) != last)
        {
            return;
        }

        Interlocked.Increment(ref _keyFramesRequested);
        _encoder.ForceKeyFrame();
    }

    public void Start() => _loop = Task.Run(() => RunAsync(_stop.Token));

    /// <summary>Passa a enviar para esta conexão; a anterior é encerrada.</summary>
    public void Attach(RTCPeerConnection peer)
    {
        RTCPeerConnection? previous;
        lock (_gate)
        {
            previous = _peer;
            _peer = peer;
        }

        if (previous is not null && !ReferenceEquals(previous, peer))
        {
            previous.Close("nova sessão");
        }

        _encoder.ForceKeyFrame();
    }

    public void Detach(RTCPeerConnection peer)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_peer, peer))
            {
                _peer = null;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        if (_loop is not null)
        {
            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Encerramento normal.
            }
        }

        RTCPeerConnection? peer;
        lock (_gate)
        {
            peer = _peer;
            _peer = null;
        }

        peer?.Close("encerrando");
        _encoder.Dispose();
        _stop.Dispose();
    }

    private async Task RunAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1.0 / Fps));
        while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
        {
            RTCPeerConnection? peer;
            lock (_gate)
            {
                peer = _peer;
            }

            if (peer is null || _source.Latest is not { } frame)
            {
                continue;
            }

            var started = ServerClock.NowMs;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                Compose(frame);
                if (_latencyStamp)
                {
                    Stamp((uint)started);
                }
                MemoryMarshal.AsBytes(_output.AsSpan()).CopyTo(_outputBytes);
                var encoded = _encoder.EncodeVideo(Width, Height, _outputBytes, VideoPixelFormatsEnum.Bgra, VideoCodecsEnum.H264);
                if (encoded is { Length: > 0 })
                {
                    peer.SendVideo(RtpDuration, encoded);
                    Interlocked.Add(ref _bytesSent, encoded.Length);
                    Interlocked.Increment(ref _framesSent);
                    Interlocked.Add(ref _encodeMicros, (long)watch.Elapsed.TotalMicroseconds);
                }
            }
            catch (Exception ex) when (ex is ApplicationException or InvalidOperationException or NotImplementedException or ObjectDisposedException)
            {
                if (LastError != ex.Message)
                {
                    LastError = ex.Message;
                    Console.WriteLine($"ERRO ao codificar/enviar: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Copia o frame para 1280x720 mantendo a proporção (vizinho mais próximo),
    /// com o mesmo encaixe que o agente usa para converter toques.
    /// </summary>
    private void Compose(CapturedFrame frame)
    {
        var layout = VideoLayout.Fit(frame.Width, frame.Height, Width, Height);
        var content = layout.Content;

        if (_mapSourceWidth != frame.Width || _mapTargetWidth != content.Width)
        {
            _xMap = new int[content.Width];
            for (var x = 0; x < content.Width; x++)
            {
                _xMap[x] = Math.Min(frame.Width - 1, (int)(x / layout.Scale));
            }

            _mapSourceWidth = frame.Width;
            _mapTargetWidth = content.Width;
        }

        Array.Fill(_output, Black);
        var source = MemoryMarshal.Cast<byte, uint>(frame.Bgra.AsSpan());
        for (var y = 0; y < content.Height; y++)
        {
            var sourceY = Math.Min(frame.Height - 1, (int)(y / layout.Scale));
            var sourceRow = source.Slice(sourceY * frame.Width, frame.Width);
            var targetRow = _output.AsSpan(((content.Y + y) * Width) + content.X, content.Width);
            for (var x = 0; x < content.Width; x++)
            {
                targetRow[x] = sourceRow[_xMap[x]];
            }
        }

        Volatile.Write(ref _lastLayout, layout);
    }

    private void Stamp(uint value)
    {
        var checksum = (uint)BitOperations.PopCount(value) & 0xF;
        for (var i = 0; i < StampBits; i++)
        {
            bool on;
            if (i < 4)
            {
                on = i % 2 == 0;
            }
            else if (i < 36)
            {
                on = ((value >> (35 - i)) & 1) == 1;
            }
            else
            {
                on = ((checksum >> (39 - i)) & 1) == 1;
            }

            var color = on ? White : Black;
            var left = StampX + (i * StampBlock);
            for (var y = 0; y < StampBlock; y++)
            {
                _output.AsSpan(((StampY + y) * Width) + left, StampBlock).Fill(color);
            }
        }
    }
}

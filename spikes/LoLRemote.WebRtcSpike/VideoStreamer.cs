using System.Numerics;
using System.Runtime.InteropServices;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.FFmpeg;

namespace LoLRemote.WebRtcSpike;

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
    private readonly FFmpegVideoEncoder _encoder = new();
    private readonly uint[] _output = new uint[Width * Height];
    private readonly byte[] _outputBytes = new byte[Width * Height * 4];
    private readonly CancellationTokenSource _stop = new();
    private readonly Lock _gate = new();
    private int[] _xMap = [];
    private int _mapSourceWidth;
    private int _mapTargetWidth;
    private RTCPeerConnection? _peer;
    private Task? _loop;
    private long _framesSent;
    private long _bytesSent;
    private long _encodeMicros;

    public VideoStreamer(FrameSource source)
    {
        _source = source;
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
                Stamp((uint)started);
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

    /// <summary>Copia o frame para 1280x720 mantendo a proporção (vizinho mais próximo).</summary>
    private void Compose(CapturedFrame frame)
    {
        var scale = Math.Min((double)Width / frame.Width, (double)Height / frame.Height);
        var targetWidth = Math.Clamp((int)(frame.Width * scale), 1, Width);
        var targetHeight = Math.Clamp((int)(frame.Height * scale), 1, Height);
        var offsetX = (Width - targetWidth) / 2;
        var offsetY = (Height - targetHeight) / 2;

        if (_mapSourceWidth != frame.Width || _mapTargetWidth != targetWidth)
        {
            _xMap = new int[targetWidth];
            for (var x = 0; x < targetWidth; x++)
            {
                _xMap[x] = Math.Min(frame.Width - 1, (int)(x / scale));
            }

            _mapSourceWidth = frame.Width;
            _mapTargetWidth = targetWidth;
        }

        Array.Fill(_output, Black);
        var source = MemoryMarshal.Cast<byte, uint>(frame.Bgra.AsSpan());
        for (var y = 0; y < targetHeight; y++)
        {
            var sourceY = Math.Min(frame.Height - 1, (int)(y / scale));
            var sourceRow = source.Slice(sourceY * frame.Width, frame.Width);
            var targetRow = _output.AsSpan(((offsetY + y) * Width) + offsetX, targetWidth);
            for (var x = 0; x < targetWidth; x++)
            {
                targetRow[x] = sourceRow[_xMap[x]];
            }
        }
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

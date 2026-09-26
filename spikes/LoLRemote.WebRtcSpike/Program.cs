using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using LoLRemote.CaptureSpike;
using LoLRemote.WebRtcSpike;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.FFmpeg;

const string TargetTitle = "LoL Remote Simulator";
const string TargetProcess = "LoLRemote.Simulator";
const int Port = 5080;
const int MaxStatsBytes = 16 * 1024;

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine("LoL Remote - spike de WebRTC H.264");
Console.WriteLine(new string('=', 40));

// 1. FFmpeg (codificador H.264).
var ffmpegPath = SpikeEnvironment.FindFfmpeg(args);
if (ffmpegPath is null)
{
    Console.WriteLine("ERRO: FFmpeg não encontrado. Instale com:");
    Console.WriteLine("  winget install \"FFmpeg (Shared)\" --version 8.1");
    Console.WriteLine("ou informe a pasta com --ffmpeg <pasta bin>.");
    return 2;
}

try
{
    FFmpegInit.Initialise(FfmpegLogLevelEnum.AV_LOG_ERROR, ffmpegPath);
}
catch (Exception ex) when (ex is ApplicationException or DllNotFoundException or NotSupportedException)
{
    Console.WriteLine($"ERRO ao carregar o FFmpeg de {ffmpegPath}: {ex.Message}");
    Console.WriteLine("A versão esperada é a 8.1 (Shared).");
    return 2;
}

Console.WriteLine($"FFmpeg: {ffmpegPath}");

// 2. Janela alvo, com falha fechada.
var candidates = TargetWindow.Find(TargetTitle, TargetProcess);
if (candidates.Count != 1)
{
    Console.WriteLine(candidates.Count == 0
        ? "ERRO: simulador não encontrado. Abra com: dotnet run --project src/LoLRemote.Simulator -- --fast"
        : $"ERRO: {candidates.Count} janelas do simulador abertas. Deixe só uma.");
    return 3;
}

// 3. Rede: mídia só pela interface Tailscale; sem Tailscale, só local.
var tailscale = SpikeEnvironment.FindTailscaleAddress();
var mediaAddress = tailscale ?? IPAddress.Loopback;
Console.WriteLine(tailscale is null
    ? "AVISO: Tailscale não encontrado. A mídia fica restrita a este PC (127.0.0.1)."
    : "Tailscale encontrado: a mídia usa somente a interface Tailscale.");

var outputDir = Path.Combine(
    Environment.CurrentDirectory,
    "spike-output",
    "webrtc-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
Directory.CreateDirectory(outputDir);
var statsPath = Path.Combine(outputDir, "stats.jsonl");
var statsLock = new Lock();

using var source = new FrameSource(candidates[0].Handle);
await using var streamer = new VideoStreamer(source);
streamer.Start();

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
    WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot"),
});
builder.WebHost.UseUrls($"http://127.0.0.1:{Port}");
builder.Logging.SetMinimumLevel(LogLevel.Warning);
var app = builder.Build();
SIPSorcery.LogFactory.Set(app.Services.GetRequiredService<ILoggerFactory>());

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/time", () => Results.Text(ServerClock.NowMs.ToString(CultureInfo.InvariantCulture)));

app.MapPost("/offer", async (HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body);
    var offer = await reader.ReadToEndAsync().ConfigureAwait(false);

    var peer = CreatePeer(mediaAddress, streamer);
    var result = peer.setRemoteDescription(new RTCSessionDescriptionInit { sdp = offer, type = RTCSdpType.offer });
    if (result != SetDescriptionResultEnum.OK)
    {
        peer.Close("oferta inválida");
        Console.WriteLine($"Oferta recusada: {result}");
        return Results.BadRequest(result.ToString());
    }

    var answer = peer.createAnswer();
    if (answer is null)
    {
        peer.Close("sem resposta SDP");
        return Results.Problem("Não foi possível criar a resposta SDP.");
    }

    await peer.setLocalDescription(answer).ConfigureAwait(false);
    Console.WriteLine("Nova conexão negociada.");
    return Results.Text(peer.localDescription?.sdp?.ToString() ?? answer.sdp, "application/sdp");
});

app.MapPost("/stats", async (HttpRequest request) =>
{
    if (request.ContentLength is > MaxStatsBytes)
    {
        return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
    }

    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync().ConfigureAwait(false);
    if (body.Length > MaxStatsBytes)
    {
        return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
    }

    JsonElement stats;
    try
    {
        using var document = JsonDocument.Parse(body);
        stats = document.RootElement.Clone();
    }
    catch (JsonException)
    {
        return Results.BadRequest();
    }

    var line = JsonSerializer.Serialize(new
    {
        at = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
        framesCaptured = source.FramesCaptured,
        framesSent = streamer.FramesSent,
        kbSent = streamer.BytesSent / 1024,
        encodeMs = Math.Round(streamer.AverageEncodeMs, 1),
        windowClosed = source.Closed,
        client = stats,
    });
    lock (statsLock)
    {
        File.AppendAllText(statsPath, line + Environment.NewLine);
    }

    Console.WriteLine(
        $"[{Text(stats, "scenario")}] latência p50 {Text(stats, "latencyP50")} ms, p95 {Text(stats, "latencyP95")} ms, " +
        $"{Text(stats, "fps")} fps, {Text(stats, "codec")}, conexão {Text(stats, "localType")}/{Text(stats, "remoteType")}");
    return Results.NoContent();
});

Console.WriteLine();
Console.WriteLine($"Servidor em http://127.0.0.1:{Port} (somente local).");
Console.WriteLine($"No iPhone, abra o endereço do Tailscale Serve para a porta {Port}.");
Console.WriteLine($"Estatísticas em: {statsPath}");
Console.WriteLine("Ctrl+C para encerrar.");
await app.RunAsync().ConfigureAwait(false);
return 0;

static RTCPeerConnection CreatePeer(IPAddress bindAddress, VideoStreamer streamer)
{
    var peer = new RTCPeerConnection(new RTCConfiguration { X_BindAddress = bindAddress });
    var h264 = new VideoFormat(
        VideoCodecsEnum.H264,
        100,
        VideoFormat.DEFAULT_CLOCK_RATE,
        "packetization-mode=1;profile-level-id=42e01f");
    peer.addTrack(new MediaStreamTrack(new List<VideoFormat> { h264 }, MediaStreamStatusEnum.SendOnly));

    peer.onconnectionstatechange += state =>
    {
        Console.WriteLine($"Conexão WebRTC: {state}");
        switch (state)
        {
            case RTCPeerConnectionState.connected:
                streamer.Attach(peer);
                break;
            case RTCPeerConnectionState.failed:
                streamer.Detach(peer);
                peer.Close("falha de ICE");
                break;
            case RTCPeerConnectionState.disconnected:
            case RTCPeerConnectionState.closed:
                streamer.Detach(peer);
                break;
        }
    };

    return peer;
}

static string Text(JsonElement element, string property) =>
    element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value)
        ? value.ToString()
        : "-";

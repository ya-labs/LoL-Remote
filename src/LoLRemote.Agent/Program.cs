using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using LoLRemote.Agent;
using LoLRemote.Agent.Capture;
using LoLRemote.Agent.Input;
using LoLRemote.Agent.League;
using LoLRemote.Agent.Platform;
using LoLRemote.Agent.Session;
using LoLRemote.Agent.Video;
using SIPSorceryMedia.FFmpeg;

const string Version = "0.1.0";
const int MaxOfferBytes = 64 * 1024;
const int MaxStatsBytes = 16 * 1024;

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine($"LoL Remote - agente {Version}");
Console.WriteLine(new string('=', 40));

var options = AgentOptions.Parse(args);
if (options is null)
{
    Console.WriteLine(AgentOptions.Usage);
    return 1;
}

// 1. Codificador de vídeo.
var ffmpegArgs = options.FfmpegPath is null ? Array.Empty<string>() : new[] { "--ffmpeg", options.FfmpegPath };
var ffmpegPath = AgentEnvironment.FindFfmpeg(ffmpegArgs);
if (ffmpegPath is null)
{
    Console.WriteLine("ERRO: FFmpeg não encontrado. Instale com: winget install \"FFmpeg (Shared)\" --version 8.1");
    return 2;
}

try
{
    FFmpegInit.Initialise(FfmpegLogLevelEnum.AV_LOG_ERROR, ffmpegPath);
}
catch (Exception ex) when (ex is ApplicationException or DllNotFoundException or NotSupportedException)
{
    Console.WriteLine($"ERRO ao carregar o FFmpeg: {ex.Message}");
    return 2;
}

// 2. Alvo: na v0.1, somente o simulador.
var target = TargetWindow.Find(TargetSpec.Simulator, out var matches);
if (target is null)
{
    Console.WriteLine(matches == 0
        ? "ERRO: simulador não encontrado. Abra com: dotnet run --project src/LoLRemote.Simulator -- --fast"
        : $"ERRO: {matches} janelas do simulador abertas. Deixe só uma.");
    return 3;
}

Console.WriteLine($"Alvo: {target.ProcessName} (PID {target.ProcessId}).");

// 3. Rede: mídia só pela Tailscale; HTTP só em loopback.
var tailscale = AgentEnvironment.FindTailscaleAddress();
Console.WriteLine(tailscale is null
    ? "AVISO: Tailscale não encontrado. A mídia fica restrita a este PC."
    : "Tailscale encontrado: a mídia usa somente a interface Tailscale.");

var outputDir = Path.Combine(Environment.CurrentDirectory, "agent-output");
Directory.CreateDirectory(outputDir);
var statsPath = Path.Combine(outputDir, $"stats-{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.jsonl");
var statsLock = new Lock();

using var frames = new FrameSource(target.Handle);
await using var streamer = new VideoStreamer(frames);
streamer.Start();
await using var phase = new PhaseMonitor(PhaseMonitor.SimulatorLockfilePath, target.ProcessId);
phase.Start();
var injector = new WindowsInputInjector();
await using var sessions = new SessionManager(
    target,
    frames,
    streamer,
    phase,
    injector,
    tailscale ?? IPAddress.Loopback,
    TimeSpan.FromMinutes(options.RemoteMinutes));
sessions.Start();

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = [],
    ContentRootPath = AppContext.BaseDirectory,
    WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot"),
});
builder.WebHost.UseUrls($"http://127.0.0.1:{options.Port}");
builder.Logging.SetMinimumLevel(LogLevel.Warning);
var app = builder.Build();
SIPSorcery.LogFactory.Set(app.Services.GetRequiredService<ILoggerFactory>());

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/v1/health", () => Results.Json(new { status = "ok", version = Version }));

app.MapGet("/api/v1/time", (HttpResponse response) =>
{
    response.Headers.CacheControl = "no-store";
    return Results.Json(new { serverTimeMs = ServerClock.NowMs });
});

app.MapPost("/api/v1/sessions", async (HttpRequest request) =>
{
    var offer = await ReadLimitedAsync(request, MaxOfferBytes).ConfigureAwait(false);
    if (offer is null)
    {
        return Problem(StatusCodes.Status413PayloadTooLarge, "payload-too-large", "Oferta SDP grande demais.");
    }

    var answer = await sessions.CreateSessionAsync(offer).ConfigureAwait(false);
    return answer is null
        ? Problem(StatusCodes.Status400BadRequest, "invalid-offer", "Oferta SDP inválida.")
        : Results.Text(answer, "application/sdp", Encoding.UTF8, StatusCodes.Status201Created);
});

app.MapPost("/api/v1/diagnostics/stats", async (HttpRequest request) =>
{
    var body = await ReadLimitedAsync(request, MaxStatsBytes).ConfigureAwait(false);
    if (body is null)
    {
        return Problem(StatusCodes.Status413PayloadTooLarge, "payload-too-large", "Estatísticas grandes demais.");
    }

    try
    {
        using var document = JsonDocument.Parse(body);
        var line = JsonSerializer.Serialize(new
        {
            at = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            framesSent = streamer.FramesSent,
            keyFramesRequested = streamer.KeyFramesRequested,
            client = document.RootElement,
        });
        lock (statsLock)
        {
            File.AppendAllText(statsPath, line + Environment.NewLine);
        }
    }
    catch (JsonException)
    {
        return Problem(StatusCodes.Status400BadRequest, "invalid-request", "JSON inválido.");
    }

    return Results.NoContent();
});

Console.WriteLine();
Console.WriteLine($"Pronto. HTTP em http://127.0.0.1:{options.Port} (publique com: tailscale serve --bg {options.Port}).");
Console.WriteLine($"Modo remoto ativo por {options.RemoteMinutes} minutos. Mexer no mouse/teclado do PC pausa o controle por 10 s.");
Console.WriteLine("Para encerrar tudo imediatamente: Ctrl+C.");
Console.WriteLine();
await app.RunAsync().ConfigureAwait(false);
return 0;

static IResult Problem(int status, string code, string detail) => Results.Problem(
    detail: detail,
    statusCode: status,
    extensions: new Dictionary<string, object?> { ["code"] = code });

static async Task<string?> ReadLimitedAsync(HttpRequest request, int maxBytes)
{
    if (request.ContentLength is { } length && length > maxBytes)
    {
        return null;
    }

    using var reader = new StreamReader(request.Body, Encoding.UTF8);
    var buffer = new char[maxBytes + 1];
    var total = 0;
    int read;
    while ((read = await reader.ReadAsync(buffer.AsMemory(total, buffer.Length - total)).ConfigureAwait(false)) > 0)
    {
        total += read;
        if (total > maxBytes)
        {
            return null;
        }
    }

    return new string(buffer, 0, total);
}

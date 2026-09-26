using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LoLRemote.Agent.Core.Input;
using LoLRemote.Agent.Core.League;

namespace LoLRemote.Agent.League;

/// <summary>
/// Lê a fase do jogo pela API local, a partir do lockfile. Na v0.1 só aceita o
/// simulador (HTTP em 127.0.0.1) e exige que o lockfile pertença ao mesmo
/// processo da janela alvo. Sem resposta recente, a fase vira Unknown.
/// </summary>
internal sealed class PhaseMonitor : IAsyncDisposable
{
    public const string PhaseEndpoint = "/lol-gameflow/v1/gameflow-phase";

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);
    private const long MaxAgeMs = 2000;

    private readonly string _lockfilePath;
    private readonly uint _expectedProcessId;
    private readonly CancellationTokenSource _stop = new();
    private HttpClient? _http;
    private string? _lockfileContent;
    private Task? _loop;
    private volatile string _status = "iniciando";
    private volatile GamePhase _phase = GamePhase.Unknown;
    private long _lastSuccessMs = long.MinValue / 2;

    public PhaseMonitor(string lockfilePath, uint expectedProcessId)
    {
        _lockfilePath = lockfilePath;
        _expectedProcessId = expectedProcessId;
    }

    public static string SimulatorLockfilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LoLRemote",
        "simulator",
        "lockfile");

    /// <summary>Fase atual, ou Unknown se não houver leitura recente.</summary>
    public GamePhase Current =>
        ServerClock.NowMs - Interlocked.Read(ref _lastSuccessMs) <= MaxAgeMs ? _phase : GamePhase.Unknown;

    /// <summary>Descrição curta para o console.</summary>
    public string Status => _status;

    public void Start() => _loop = Task.Run(() => RunAsync(_stop.Token));

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

        _http?.Dispose();
        _stop.Dispose();
    }

    private async Task RunAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(PollInterval);
        do
        {
            await PollAsync(token).ConfigureAwait(false);
        }
        while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false));
    }

    private async Task PollAsync(CancellationToken token)
    {
        string content;
        try
        {
            content = await File.ReadAllTextAsync(_lockfilePath, token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _status = "lockfile do simulador não encontrado";
            return;
        }

        if (!Lockfile.TryParse(content, out var lockfile))
        {
            _status = "lockfile inválido";
            return;
        }

        if (lockfile!.ProcessId != _expectedProcessId)
        {
            _status = "lockfile de outro processo";
            return;
        }

        if (lockfile.Protocol != "http")
        {
            _status = "protocolo não suportado na v0.1";
            return;
        }

        if (_http is null || content != _lockfileContent)
        {
            _http?.Dispose();
            _http = new HttpClient { BaseAddress = lockfile.BaseAddress, Timeout = TimeSpan.FromSeconds(1) };
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($"{Lockfile.User}:{lockfile.Password}")));
            _lockfileContent = content;
        }

        try
        {
            var body = await _http.GetStringAsync(new Uri(PhaseEndpoint, UriKind.Relative), token).ConfigureAwait(false);
            _phase = GamePhases.Parse(JsonSerializer.Deserialize<string>(body));
            Interlocked.Exchange(ref _lastSuccessMs, ServerClock.NowMs);
            _status = $"fase {_phase}";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            token.ThrowIfCancellationRequested();
            _status = "API de fase sem resposta";
        }
    }
}

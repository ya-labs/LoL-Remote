using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using LoLRemote.Agent.Core.Input;
using LoLRemote.Agent.Core.League;
using LoLRemote.Agent.Platform;

namespace LoLRemote.Agent.League;

/// <summary>De onde vem a fase e como confiar na resposta.</summary>
/// <param name="LockfilePath">Caminho do lockfile.</param>
/// <param name="ExpectedProcessId">PID que precisa constar no lockfile.</param>
/// <param name="RiotRoot">Certificado raiz da Riot para validar o HTTPS; null aceita só HTTP (simulador).</param>
internal sealed record PhaseSource(string LockfilePath, uint ExpectedProcessId, X509Certificate2? RiotRoot);

/// <summary>
/// Lê a fase do jogo pela API local a partir do lockfile, em 127.0.0.1.
/// O lockfile precisa ser do processo esperado (o simulador, ou o LeagueClient
/// pai da janela alvo). HTTPS só é aceito com cadeia até o certificado raiz da
/// Riot. Sem resposta nos últimos 2 s, a fase vira Unknown e o input bloqueia.
/// Somente leitura: nenhuma outra rota é chamada.
/// </summary>
internal sealed class PhaseMonitor : IAsyncDisposable
{
    public const string PhaseEndpoint = "/lol-gameflow/v1/gameflow-phase";

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);
    private const long MaxAgeMs = 2000;

    private readonly Lock _gate = new();
    private PhaseSource _source;
    private readonly CancellationTokenSource _stop = new();
    private HttpClient? _http;
    private string? _lockfileContent;
    private Task? _loop;
    private volatile string _status = "iniciando";
    private volatile GamePhase _phase = GamePhase.Unknown;
    private long _lastSuccessMs = long.MinValue / 2;

    public PhaseMonitor(PhaseSource source)
    {
        _source = source;
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

    /// <summary>Último motivo de recusa do certificado HTTPS, para diagnóstico.</summary>
    private static string? CertificateProblem { get; set; }

    public void Start() => _loop = Task.Run(() => RunAsync(_stop.Token));

    /// <summary>Troca a fonte (janela alvo reencontrada); a fase fica Unknown até a próxima leitura.</summary>
    public void Replace(PhaseSource source)
    {
        lock (_gate)
        {
            _source = source;
            _lockfileContent = null;
            Interlocked.Exchange(ref _lastSuccessMs, long.MinValue / 2);
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
        PhaseSource source;
        lock (_gate)
        {
            source = _source;
        }

        string content;
        try
        {
            // O LeagueClient mantém o lockfile aberto para escrita: é preciso
            // compartilhar leitura e escrita, senão o Windows nega o acesso.
            var stream = new FileStream(
                source.LockfilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            await using (stream.ConfigureAwait(false))
            {
                using var reader = new StreamReader(stream);
                content = await reader.ReadToEndAsync(token).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            _status = $"lockfile não encontrado em {source.LockfilePath}";
            return;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _status = $"lockfile inacessível ({ex.GetType().Name}: {ex.Message})";
            return;
        }

        if (!Lockfile.TryParse(content, out var lockfile))
        {
            _status = "lockfile inválido";
            return;
        }

        if (lockfile!.ProcessId != source.ExpectedProcessId)
        {
            _status = "lockfile de outro processo";
            return;
        }

        var https = lockfile.Protocol == "https";
        if (https && source.RiotRoot is null)
        {
            _status = "HTTPS sem certificado raiz da Riot";
            return;
        }

        if (!https && source.RiotRoot is not null)
        {
            _status = "cliente real sem HTTPS: recusado";
            return;
        }

        if (_http is null || content != _lockfileContent)
        {
            _http?.Dispose();
            _http = CreateClient(lockfile, source.RiotRoot);
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
            var certificate = CertificateProblem is { } problem ? $"; certificado: {problem}" : string.Empty;
            _status = $"API de fase sem resposta ({ex.GetType().Name}: {ex.GetBaseException().Message}{certificate})";
        }
    }

    private static HttpClient CreateClient(Lockfile lockfile, X509Certificate2? riotRoot)
    {
#pragma warning disable CA2000 // O HttpClient assume o handler e o descarta junto.
        var handler = new HttpClientHandler();
#pragma warning restore CA2000
        if (riotRoot is not null)
        {
            handler.ServerCertificateCustomValidationCallback = (request, certificate, _, errors) =>
                request.RequestUri?.Host == "127.0.0.1"
                && certificate is not null
                && IsSignedByRiot(certificate, riotRoot, errors);
        }

        var http = new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = lockfile.BaseAddress,
            Timeout = TimeSpan.FromSeconds(1),
        };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{Lockfile.User}:{lockfile.Password}")));
        return http;
    }

    /// <summary>
    /// Aceita apenas certificado emitido pela raiz da Riot. O nome no certificado
    /// da LCU não bate com 127.0.0.1, então só esse erro é tolerado.
    /// </summary>
    private static bool IsSignedByRiot(X509Certificate2 certificate, X509Certificate2 riotRoot, SslPolicyErrors errors)
    {
        if ((errors & ~(SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch)) != 0)
        {
            CertificateProblem = errors.ToString();
            return false;
        }

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(riotRoot);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid;
        var valid = chain.Build(certificate);
        CertificateProblem = valid
            ? null
            : $"emissor \"{certificate.Issuer}\"; " + string.Join(", ", chain.ChainStatus.Select(s => s.Status));
        return valid;
    }
}

/// <summary>Descobre de onde ler a fase para cada alvo.</summary>
internal static class PhaseSources
{
    public const string RiotRootFileName = "riotgames.pem";

    /// <summary>SHA-256 do certificado "LoL Game Engineering Certificate Authority" (válido até 2043).</summary>
    public const string RiotRootSha256 = "CA8C9D325B4CDC464C6C94A585C85E91EC23D40BA5BF3AE2822B951A4A504EA3";
    private const string DefaultLeagueLockfile = @"C:\Riot Games\League of Legends\lockfile";

    /// <summary>Simulador: lockfile em %LOCALAPPDATA%, do mesmo processo da janela, só HTTP.</summary>
    public static PhaseSource ForSimulator(TargetWindow target) =>
        new(PhaseMonitor.SimulatorLockfilePath, target.ProcessId, null);

    /// <summary>
    /// Cliente real: o lockfile é escrito pelo LeagueClient, processo pai da
    /// janela (LeagueClientUx), na pasta de instalação. Retorna null e o motivo
    /// quando algo não confere.
    /// </summary>
    public static PhaseSource? ForLeague(TargetWindow target, string? lockfileOverride, out string problem)
    {
        problem = string.Empty;
        var rootPath = Path.Combine(AppContext.BaseDirectory, RiotRootFileName);
        if (!File.Exists(rootPath))
        {
            problem = $"certificado raiz da Riot ausente ({RiotRootFileName}); veja docs/agente.md";
            return null;
        }

        var parent = TargetWindow.GetParentProcessId(target.ProcessId);
        if (parent is null || !string.Equals(TargetWindow.ProcessNameOf(parent.Value), "LeagueClient", StringComparison.OrdinalIgnoreCase))
        {
            problem = "a janela do cliente não pertence a um LeagueClient";
            return null;
        }

        var lockfile = lockfileOverride
            ?? (TargetWindow.ProcessDirectoryOf(parent.Value) is { } dir ? Path.Combine(dir, "lockfile") : null)
            ?? InstallFromRiotMetadata()
            ?? DefaultLeagueLockfile;
        var riotRoot = X509Certificate2.CreateFromPem(File.ReadAllText(rootPath));
        if (!string.Equals(riotRoot.GetCertHashString(System.Security.Cryptography.HashAlgorithmName.SHA256), RiotRootSha256, StringComparison.OrdinalIgnoreCase))
        {
            riotRoot.Dispose();
            problem = $"{RiotRootFileName} não é o certificado raiz esperado da Riot";
            return null;
        }

        Console.WriteLine($"Lockfile do cliente: {lockfile}");
        return new PhaseSource(lockfile, parent.Value, riotRoot);
    }

    /// <summary>
    /// Pasta de instalação informada pelo Riot Client em
    /// C:\ProgramData\Riot Games\Metadata (campo product_install_full_path).
    /// </summary>
    private static string? InstallFromRiotMetadata()
    {
        var settings = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Riot Games",
            "Metadata",
            "league_of_legends.live",
            "league_of_legends.live.product_settings.yaml");
        try
        {
            foreach (var line in File.ReadLines(settings))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("product_install_full_path:", StringComparison.Ordinal))
                {
                    var path = trimmed["product_install_full_path:".Length..].Trim().Trim('"', '\'');
                    return path.Length > 0 ? Path.Combine(path, "lockfile") : null;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Sem metadados: segue para o caminho padrão.
        }

        return null;
    }
}

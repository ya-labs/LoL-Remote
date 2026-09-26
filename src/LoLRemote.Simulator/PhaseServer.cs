using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using LoLRemote.Simulator.Core;

namespace LoLRemote.Simulator;

/// <summary>
/// Imita a API local do League Client só no necessário: um lockfile com porta
/// e senha, e a rota de fase de gameflow com autenticação Basic (usuário riot).
/// Escuta apenas em 127.0.0.1. A LCU usa HTTPS; o simulador usa HTTP.
/// </summary>
internal sealed class PhaseServer : IDisposable
{
    private const int MaxRequestBytes = 8 * 1024;

    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly string _password = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
    private readonly byte[] _expectedAuthorization;
    private string? _lockfileContent;
    private volatile string _phase = "None";

    public PhaseServer()
    {
        _expectedAuthorization = Encoding.ASCII.GetBytes(
            "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes("riot:" + _password)));
        LockfilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LoLRemote",
            "simulator",
            "lockfile");
    }

    public string LockfilePath { get; }

    public void SetPhase(string phase) => _phase = phase;

    public void Start()
    {
        _listener.Start();
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        Directory.CreateDirectory(Path.GetDirectoryName(LockfilePath)!);
        _lockfileContent = $"LoLRemoteSimulator:{Environment.ProcessId}:{port}:{_password}:http";
        File.WriteAllText(LockfilePath, _lockfileContent);
        _ = AcceptLoopAsync(_stop.Token);
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();
        try
        {
            if (_lockfileContent is not null && File.Exists(LockfilePath) && File.ReadAllText(LockfilePath) == _lockfileContent)
            {
                File.Delete(LockfilePath);
            }
        }
        catch (IOException)
        {
            // Outro simulador pode ter sobrescrito o arquivo; nada a fazer.
        }

        _stop.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                return;
            }

            _ = HandleAsync(client, token);
        }
    }

    private async Task HandleAsync(TcpClient client, CancellationToken token)
    {
        using (client)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(TimeSpan.FromSeconds(2));
                var stream = client.GetStream();
                var request = await ReadHeadersAsync(stream, timeout.Token).ConfigureAwait(false);
                var (status, body) = Route(request);
                var bodyBytes = Encoding.UTF8.GetBytes(body);
                var head = $"HTTP/1.1 {status}\r\nContent-Type: application/json\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(head), timeout.Token).ConfigureAwait(false);
                await stream.WriteAsync(bodyBytes, timeout.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
            {
                // Conexão encerrada ou lenta demais; o agente tenta de novo.
            }
        }
    }

    private (string Status, string Body) Route(string? request)
    {
        if (request is null)
        {
            return ("400 Bad Request", "{}");
        }

        var lines = request.Split("\r\n");
        var requestLine = lines[0].Split(' ');
        var authorization = lines
            .Skip(1)
            .Where(l => l.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase))
            .Select(l => l["Authorization:".Length..].Trim())
            .FirstOrDefault() ?? string.Empty;

        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(authorization), _expectedAuthorization))
        {
            return ("401 Unauthorized", "{}");
        }

        return requestLine.Length >= 2 && requestLine[0] == "GET" && requestLine[1] == LcuGameflow.PhaseEndpoint
            ? ("200 OK", $"\"{_phase}\"")
            : ("404 Not Found", "{}");
    }

    private static async Task<string?> ReadHeadersAsync(NetworkStream stream, CancellationToken token)
    {
        var buffer = new byte[MaxRequestBytes];
        var length = 0;
        while (length < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(length), token).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            length += read;
            var text = Encoding.ASCII.GetString(buffer, 0, length);
            var end = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (end >= 0)
            {
                return text[..end];
            }
        }

        return null;
    }
}

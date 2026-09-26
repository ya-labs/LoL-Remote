using System.Diagnostics;
using System.Text.Json;

namespace LoLRemote.WebRtcSpike;

/// <summary>
/// Consulta `tailscale status --json` a cada 10 s e informa se os iPhones
/// online falam com o PC direto ou por relay (DERP). Não registra endereços.
/// </summary>
internal sealed class TailscalePathMonitor
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);
    private readonly Lock _gate = new();
    private string _description = "desconhecido";
    private DateTime _updatedAt = DateTime.MinValue;
    private bool _refreshing;

    public string Describe()
    {
        lock (_gate)
        {
            if (!_refreshing && DateTime.UtcNow - _updatedAt > Interval)
            {
                _refreshing = true;
                _ = Task.Run(Refresh);
            }

            return _description;
        }
    }

    private void Refresh()
    {
        var description = Query();
        lock (_gate)
        {
            _description = description;
            _updatedAt = DateTime.UtcNow;
            _refreshing = false;
        }
    }

    private static string Query()
    {
        var executable = File.Exists(@"C:\Program Files\Tailscale\tailscale.exe")
            ? @"C:\Program Files\Tailscale\tailscale.exe"
            : "tailscale";
        try
        {
            using var process = Process.Start(new ProcessStartInfo(executable, "status --json")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (process is null)
            {
                return "tailscale indisponível";
            }

            var output = process.StandardOutput.ReadToEndAsync();
            if (!process.WaitForExit(3000) || !output.Wait(1000))
            {
                return "tailscale sem resposta";
            }

            using var document = JsonDocument.Parse(output.Result);
            if (!document.RootElement.TryGetProperty("Peer", out var peers) || peers.ValueKind != JsonValueKind.Object)
            {
                return "sem peers";
            }

            var phones = new List<string>();
            foreach (var peer in peers.EnumerateObject())
            {
                var value = peer.Value;
                if (Str(value, "OS") != "iOS" || !value.TryGetProperty("Online", out var online) || !online.GetBoolean())
                {
                    continue;
                }

                var direct = !string.IsNullOrEmpty(Str(value, "CurAddr"));
                var relay = Str(value, "Relay");
                phones.Add($"{Str(value, "HostName")}: {(direct ? "direto" : $"relay DERP {relay}")}");
            }

            return phones.Count == 0 ? "nenhum iPhone online" : string.Join("; ", phones);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or JsonException or InvalidOperationException or AggregateException)
        {
            return "tailscale indisponível";
        }
    }

    private static string Str(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}

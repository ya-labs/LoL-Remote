using System.Globalization;

namespace LoLRemote.Agent;

/// <summary>Opções de linha de comando do agente.</summary>
internal sealed record AgentOptions(string? FfmpegPath, int RemoteMinutes, int Port)
{
    public const int DefaultPort = 5080;
    public const int DefaultRemoteMinutes = 30;

    public static string Usage =>
        "Uso: LoLRemote.Agent [--ffmpeg <pasta bin>] [--remote-minutes 5..60] [--port 1024..65535]";

    public static AgentOptions? Parse(string[] args)
    {
        string? ffmpeg = null;
        var minutes = DefaultRemoteMinutes;
        var port = DefaultPort;
        for (var i = 0; i < args.Length; i++)
        {
            var value = i + 1 < args.Length ? args[i + 1] : null;
            switch (args[i])
            {
                case "--ffmpeg" when value is not null:
                    ffmpeg = value;
                    i++;
                    break;
                case "--remote-minutes" when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var m) && m is >= 5 and <= 60:
                    minutes = m;
                    i++;
                    break;
                case "--port" when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var p) && p is >= 1024 and <= 65535:
                    port = p;
                    i++;
                    break;
                default:
                    return null;
            }
        }

        return new AgentOptions(ffmpeg, minutes, port);
    }
}

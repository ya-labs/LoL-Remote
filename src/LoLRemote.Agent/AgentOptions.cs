using System.Globalization;

namespace LoLRemote.Agent;

/// <summary>Alvo do agente.</summary>
internal enum AgentTarget
{
    Simulator,
    League,
}

/// <summary>Opções de linha de comando do agente.</summary>
internal sealed record AgentOptions(
    AgentTarget Target,
    string? FfmpegPath,
    string? LockfilePath,
    int RemoteMinutes,
    int Port,
    bool LatencyStamp)
{
    public const int DefaultPort = 5080;
    public const int DefaultRemoteMinutes = 30;

    public static string Usage =>
        "Uso: LoLRemote.Agent [--target simulator|league] [--lockfile <caminho>] [--latency-stamp on|off]\n" +
        "                     [--ffmpeg <pasta bin>] [--remote-minutes 5..60] [--port 1024..65535]";

    public static AgentOptions? Parse(string[] args)
    {
        var target = AgentTarget.Simulator;
        string? ffmpeg = null;
        string? lockfile = null;
        bool? stamp = null;
        var minutes = DefaultRemoteMinutes;
        var port = DefaultPort;
        for (var i = 0; i < args.Length; i++)
        {
            var value = i + 1 < args.Length ? args[i + 1] : null;
            switch (args[i])
            {
                case "--target" when value is "simulator" or "league":
                    target = value == "league" ? AgentTarget.League : AgentTarget.Simulator;
                    i++;
                    break;
                case "--lockfile" when value is not null:
                    lockfile = value;
                    i++;
                    break;
                case "--latency-stamp" when value is "on" or "off":
                    stamp = value == "on";
                    i++;
                    break;
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

        // O carimbo de latência cobre uma faixa no canto inferior esquerdo do
        // vídeo: ligado por padrão só no simulador.
        return new AgentOptions(target, ffmpeg, lockfile, minutes, port, stamp ?? target == AgentTarget.Simulator);
    }
}

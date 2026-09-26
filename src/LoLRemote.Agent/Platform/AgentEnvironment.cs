using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace LoLRemote.Agent.Platform;

/// <summary>Descoberta do FFmpeg e do endereço Tailscale.</summary>
internal static class AgentEnvironment
{
    /// <summary>
    /// Procura as DLLs do FFmpeg: argumento --ffmpeg, variável LOLREMOTE_FFMPEG,
    /// instalação do winget (Gyan.FFmpeg.Shared) e, por fim, o PATH.
    /// </summary>
    public static string? FindFfmpeg(string[] args)
    {
        var index = Array.IndexOf(args, "--ffmpeg");
        if (index >= 0 && index + 1 < args.Length)
        {
            return HasFfmpeg(args[index + 1]) ? args[index + 1] : null;
        }

        var fromEnv = Environment.GetEnvironmentVariable("LOLREMOTE_FFMPEG");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return HasFfmpeg(fromEnv) ? fromEnv : null;
        }

        var wingetRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "WinGet",
            "Packages");
        if (Directory.Exists(wingetRoot))
        {
            foreach (var package in Directory.EnumerateDirectories(wingetRoot, "Gyan.FFmpeg.Shared*"))
            {
                foreach (var bin in Directory.EnumerateDirectories(package, "bin", SearchOption.AllDirectories))
                {
                    if (HasFfmpeg(bin))
                    {
                        return bin;
                    }
                }
            }
        }

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        return path
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(HasFfmpeg);
    }

    /// <summary>Endereço IPv4 da interface Tailscale (faixa 100.64.0.0/10), se houver.</summary>
    public static IPAddress? FindTailscaleAddress()
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            foreach (var unicast in nic.GetIPProperties().UnicastAddresses)
            {
                var address = unicast.Address;
                if (address.AddressFamily != AddressFamily.InterNetwork)
                {
                    continue;
                }

                var bytes = address.GetAddressBytes();
                if (bytes[0] == 100 && (bytes[1] & 0xC0) == 64)
                {
                    return address;
                }
            }
        }

        return null;
    }

    private static bool HasFfmpeg(string directory) =>
        Directory.Exists(directory) && Directory.EnumerateFiles(directory, "avcodec-*.dll").Any();
}

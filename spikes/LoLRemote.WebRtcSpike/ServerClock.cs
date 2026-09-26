using System.Diagnostics;

namespace LoLRemote.WebRtcSpike;

/// <summary>Relógio monotônico do servidor, em milissegundos desde o início.</summary>
internal static class ServerClock
{
    private static readonly Stopwatch Watch = Stopwatch.StartNew();

    public static long NowMs => Watch.ElapsedMilliseconds;
}

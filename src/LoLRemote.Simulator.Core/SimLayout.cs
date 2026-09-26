namespace LoLRemote.Simulator.Core;

/// <summary>Retângulo em coordenadas de referência (1280x720).</summary>
public readonly record struct RectF(double X, double Y, double Width, double Height)
{
    /// <summary>Borda direita (exclusiva).</summary>
    public double Right => X + Width;

    /// <summary>Borda inferior (exclusiva).</summary>
    public double Bottom => Y + Height;

    /// <summary>Indica se o ponto está dentro; bordas direita e inferior são exclusivas.</summary>
    public bool Contains(double x, double y) => x >= X && x < Right && y >= Y && y < Bottom;
}

/// <summary>Área clicável identificada.</summary>
public sealed record HitRegion(string Id, RectF Bounds);

/// <summary>Identificadores estáveis das áreas clicáveis.</summary>
public static class RegionIds
{
    /// <summary>Botão de entrar na fila.</summary>
    public const string FindMatch = "find-match";

    /// <summary>Botão de sair da fila.</summary>
    public const string CancelMatchmaking = "cancel-matchmaking";

    /// <summary>Botão de aceitar o Ready Check.</summary>
    public const string Accept = "accept";

    /// <summary>Botão de recusar o Ready Check.</summary>
    public const string Decline = "decline";

    /// <summary>Botão de confirmar banimento ou escolha.</summary>
    public const string LockIn = "lock-in";

    /// <summary>Toda a tela durante a partida.</summary>
    public const string GameplayArea = "gameplay-area";

    private const string ChampionPrefix = "champion-";

    /// <summary>Identificador do campeão no índice informado.</summary>
    public static string Champion(int index) => ChampionPrefix + index.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Extrai o índice de um identificador de campeão.</summary>
    public static bool TryParseChampion(string id, out int index)
    {
        index = -1;
        return id.StartsWith(ChampionPrefix, StringComparison.Ordinal)
            && int.TryParse(id.AsSpan(ChampionPrefix.Length), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out index);
    }
}

/// <summary>
/// Layout fixo do simulador em um espaço de referência 16:9. A janela WPF
/// escala esse espaço com letterbox, o que reproduz o problema de conversão
/// de coordenadas que o agente precisará resolver no League Client.
/// </summary>
public static class SimLayout
{
    /// <summary>Largura de referência.</summary>
    public const double ReferenceWidth = 1280;

    /// <summary>Altura de referência.</summary>
    public const double ReferenceHeight = 720;

    /// <summary>Quantidade de campeões na grade.</summary>
    public const int ChampionCount = 10;

    /// <summary>Colunas da grade de campeões.</summary>
    public const int ChampionColumns = 5;

    private const double CellSize = 120;
    private const double CellGap = 20;
    private const double GridTop = 170;

    private static readonly HitRegion[] LobbyRegions =
    [
        new(RegionIds.FindMatch, new RectF(520, 590, 240, 60)),
    ];

    private static readonly HitRegion[] MatchmakingRegions =
    [
        new(RegionIds.CancelMatchmaking, new RectF(560, 600, 160, 48)),
    ];

    private static readonly HitRegion[] ReadyCheckRegions =
    [
        new(RegionIds.Accept, new RectF(490, 400, 300, 70)),
        new(RegionIds.Decline, new RectF(565, 500, 150, 44)),
    ];

    private static readonly HitRegion[] ChampSelectRegions = BuildChampSelectRegions();

    private static readonly HitRegion[] GameplayRegions =
    [
        new(RegionIds.GameplayArea, new RectF(0, 0, ReferenceWidth, ReferenceHeight)),
    ];

    /// <summary>Área total da tela de referência.</summary>
    public static RectF Bounds { get; } = new(0, 0, ReferenceWidth, ReferenceHeight);

    /// <summary>Áreas clicáveis visíveis em uma fase e etapa.</summary>
    public static IReadOnlyList<HitRegion> RegionsFor(SimPhase phase, ChampSelectStage stage) => phase switch
    {
        SimPhase.Lobby => LobbyRegions,
        SimPhase.Matchmaking => MatchmakingRegions,
        SimPhase.ReadyCheck => ReadyCheckRegions,
        SimPhase.ChampSelect when stage is ChampSelectStage.Ban or ChampSelectStage.Pick => ChampSelectRegions,
        SimPhase.ChampSelect => [],
        SimPhase.InProgress => GameplayRegions,
        _ => [],
    };

    /// <summary>Retorna a área no ponto ou <c>null</c>.</summary>
    public static HitRegion? HitTest(SimPhase phase, ChampSelectStage stage, double x, double y)
    {
        foreach (var region in RegionsFor(phase, stage))
        {
            if (region.Bounds.Contains(x, y))
            {
                return region;
            }
        }

        return null;
    }

    /// <summary>Retângulo da célula de um campeão.</summary>
    public static RectF ChampionCell(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, ChampionCount);

        const double gridWidth = (ChampionColumns * CellSize) + ((ChampionColumns - 1) * CellGap);
        const double left = (ReferenceWidth - gridWidth) / 2;
        var column = index % ChampionColumns;
        var row = index / ChampionColumns;
        return new RectF(
            left + (column * (CellSize + CellGap)),
            GridTop + (row * (CellSize + CellGap)),
            CellSize,
            CellSize);
    }

    private static HitRegion[] BuildChampSelectRegions()
    {
        var regions = new List<HitRegion>(ChampionCount + 1);
        for (var i = 0; i < ChampionCount; i++)
        {
            regions.Add(new HitRegion(RegionIds.Champion(i), ChampionCell(i)));
        }

        regions.Add(new HitRegion(RegionIds.LockIn, new RectF(540, 490, 200, 56)));
        return [.. regions];
    }
}

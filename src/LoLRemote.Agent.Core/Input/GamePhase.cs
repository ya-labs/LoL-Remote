namespace LoLRemote.Agent.Core.Input;

/// <summary>
/// Fases de gameflow do League Client (nomes iguais aos da LCU). Qualquer valor
/// desconhecido vira <see cref="Unknown"/> e bloqueia input.
/// </summary>
public enum GamePhase
{
    /// <summary>Fase não informada ou não reconhecida.</summary>
    Unknown,

    /// <summary>Cliente aberto, fora de sala.</summary>
    None,

    /// <summary>Em sala.</summary>
    Lobby,

    /// <summary>Procurando partida.</summary>
    Matchmaking,

    /// <summary>Partida encontrada, aguardando aceite.</summary>
    ReadyCheck,

    /// <summary>Seleção de campeões.</summary>
    ChampSelect,

    /// <summary>Carregando a partida.</summary>
    GameStart,

    /// <summary>Partida em andamento.</summary>
    InProgress,

    /// <summary>Reconexão à partida.</summary>
    Reconnect,

    /// <summary>Aguardando estatísticas após a partida.</summary>
    WaitingForStats,

    /// <summary>Transição para o fim de jogo.</summary>
    PreEndOfGame,

    /// <summary>Tela de fim de jogo.</summary>
    EndOfGame,
}

/// <summary>Conversão de nomes da LCU para <see cref="GamePhase"/>.</summary>
public static class GamePhases
{
    /// <summary>Converte o texto da LCU; desconhecido vira <see cref="GamePhase.Unknown"/>.</summary>
    public static GamePhase Parse(string? value) =>
        value is not null
        && Enum.TryParse<GamePhase>(value, ignoreCase: false, out var phase)
        && phase != GamePhase.Unknown
        && Enum.IsDefined(phase)
        && !int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _)
            ? phase
            : GamePhase.Unknown;

    /// <summary>
    /// Fases em que a pessoa está nas telas do cliente e pode receber input remoto.
    /// Todas as outras, inclusive as desconhecidas, bloqueiam.
    /// </summary>
    public static bool AllowsInput(GamePhase phase) => phase is
        GamePhase.None or
        GamePhase.Lobby or
        GamePhase.Matchmaking or
        GamePhase.ReadyCheck or
        GamePhase.ChampSelect or
        GamePhase.EndOfGame;
}

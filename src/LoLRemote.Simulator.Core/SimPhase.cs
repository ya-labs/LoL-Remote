namespace LoLRemote.Simulator.Core;

/// <summary>
/// Fases do fluxo simulado. Os nomes acompanham as fases de gameflow do
/// League Client para facilitar o mapeamento futuro do monitor de estado.
/// </summary>
public enum SimPhase
{
    /// <summary>Sala aberta, fora da fila.</summary>
    Lobby,

    /// <summary>Procurando partida.</summary>
    Matchmaking,

    /// <summary>Partida encontrada; aguardando aceite.</summary>
    ReadyCheck,

    /// <summary>Seleção de campeões.</summary>
    ChampSelect,

    /// <summary>Partida em andamento: nenhum input remoto é permitido.</summary>
    InProgress,
}

/// <summary>Etapa interna da seleção de campeões.</summary>
public enum ChampSelectStage
{
    /// <summary>Fora da seleção de campeões.</summary>
    None,

    /// <summary>Turno de banir.</summary>
    Ban,

    /// <summary>Turno de escolher.</summary>
    Pick,

    /// <summary>Contagem final antes da partida.</summary>
    Finalization,
}

/// <summary>Resposta dada ao Ready Check.</summary>
public enum ReadyCheckResponse
{
    /// <summary>Sem resposta.</summary>
    None,

    /// <summary>Partida aceita.</summary>
    Accepted,
}

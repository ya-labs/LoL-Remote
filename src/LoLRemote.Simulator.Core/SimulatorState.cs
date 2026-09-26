namespace LoLRemote.Simulator.Core;

/// <summary>Instantâneo imutável do simulador.</summary>
/// <param name="Phase">Fase atual.</param>
/// <param name="Stage">Etapa da seleção de campeões.</param>
/// <param name="Deadline">Momento em que a etapa atual expira, se temporizada.</param>
/// <param name="ReadyCheck">Resposta ao Ready Check atual.</param>
/// <param name="HoveredChampion">Campeão destacado, ainda não confirmado.</param>
/// <param name="BannedChampion">Campeão banido nesta seleção.</param>
/// <param name="PickedChampion">Campeão escolhido nesta seleção.</param>
/// <param name="LastEvent">Mensagem curta sobre a última transição relevante.</param>
public sealed record SimulatorState(
    SimPhase Phase,
    ChampSelectStage Stage,
    DateTimeOffset? Deadline,
    ReadyCheckResponse ReadyCheck,
    int? HoveredChampion,
    int? BannedChampion,
    int? PickedChampion,
    string? LastEvent)
{
    /// <summary>Estado inicial: sala aberta.</summary>
    public static SimulatorState Initial { get; } = new(
        SimPhase.Lobby,
        ChampSelectStage.None,
        null,
        ReadyCheckResponse.None,
        null,
        null,
        null,
        null);
}

/// <summary>Resultado de um clique entregue ao simulador.</summary>
public enum ClickOutcome
{
    /// <summary>O clique acionou uma ação.</summary>
    Applied,

    /// <summary>O clique acertou uma área, mas a ação não era válida agora.</summary>
    Ignored,

    /// <summary>O clique não acertou nenhuma área.</summary>
    Missed,

    /// <summary>Coordenada fora do espaço de referência.</summary>
    OutOfBounds,

    /// <summary>
    /// Input recebido durante a partida. O agente nunca deveria permitir isso;
    /// o simulador conta essas ocorrências como violações.
    /// </summary>
    GameplayViolation,
}

/// <summary>Registro de um clique recebido.</summary>
/// <param name="At">Momento do clique.</param>
/// <param name="X">Coordenada X de referência.</param>
/// <param name="Y">Coordenada Y de referência.</param>
/// <param name="Phase">Fase no momento do clique.</param>
/// <param name="RegionId">Área atingida, se houver.</param>
/// <param name="Outcome">Resultado.</param>
public sealed record ClickRecord(
    DateTimeOffset At,
    double X,
    double Y,
    SimPhase Phase,
    string? RegionId,
    ClickOutcome Outcome);

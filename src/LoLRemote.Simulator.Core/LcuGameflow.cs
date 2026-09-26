namespace LoLRemote.Simulator.Core;

/// <summary>
/// Tradução das fases do simulador para as fases de gameflow do League Client,
/// servidas pelo simulador no mesmo endereço que a LCU usa.
/// </summary>
public static class LcuGameflow
{
    /// <summary>Caminho da fase de gameflow na API local (igual ao da LCU).</summary>
    public const string PhaseEndpoint = "/lol-gameflow/v1/gameflow-phase";

    /// <summary>Nome da fase no formato da LCU.</summary>
    public static string ToGameflowPhase(SimPhase phase) => phase switch
    {
        SimPhase.Lobby => "Lobby",
        SimPhase.Matchmaking => "Matchmaking",
        SimPhase.ReadyCheck => "ReadyCheck",
        SimPhase.ChampSelect => "ChampSelect",
        SimPhase.InProgress => "InProgress",
        _ => "None",
    };
}

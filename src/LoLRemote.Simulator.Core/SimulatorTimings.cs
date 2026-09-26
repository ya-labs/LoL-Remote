namespace LoLRemote.Simulator.Core;

/// <summary>Durações de cada etapa temporizada do fluxo simulado.</summary>
public sealed record SimulatorTimings
{
    /// <summary>Cria um conjunto de durações; todas devem ser positivas.</summary>
    public SimulatorTimings(
        TimeSpan matchmaking,
        TimeSpan readyCheck,
        TimeSpan ban,
        TimeSpan pick,
        TimeSpan finalization)
    {
        Matchmaking = RequirePositive(matchmaking, nameof(matchmaking));
        ReadyCheck = RequirePositive(readyCheck, nameof(readyCheck));
        Ban = RequirePositive(ban, nameof(ban));
        Pick = RequirePositive(pick, nameof(pick));
        Finalization = RequirePositive(finalization, nameof(finalization));
    }

    /// <summary>Tempo procurando partida até o Ready Check.</summary>
    public TimeSpan Matchmaking { get; }

    /// <summary>Janela para aceitar a partida.</summary>
    public TimeSpan ReadyCheck { get; }

    /// <summary>Turno de banimento.</summary>
    public TimeSpan Ban { get; }

    /// <summary>Turno de escolha.</summary>
    public TimeSpan Pick { get; }

    /// <summary>Contagem final antes da partida.</summary>
    public TimeSpan Finalization { get; }

    /// <summary>Durações próximas do fluxo real.</summary>
    public static SimulatorTimings Default { get; } = new(
        TimeSpan.FromSeconds(8),
        TimeSpan.FromSeconds(12),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(10));

    /// <summary>Durações curtas para iterar durante o desenvolvimento.</summary>
    public static SimulatorTimings Fast { get; } = new(
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(8),
        TimeSpan.FromSeconds(12),
        TimeSpan.FromSeconds(12),
        TimeSpan.FromSeconds(4));

    private static TimeSpan RequirePositive(TimeSpan value, string name) =>
        value > TimeSpan.Zero
            ? value
            : throw new ArgumentOutOfRangeException(name, value, "A duração deve ser positiva.");
}

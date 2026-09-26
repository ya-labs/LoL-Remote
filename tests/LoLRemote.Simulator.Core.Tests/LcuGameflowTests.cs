namespace LoLRemote.Simulator.Core.Tests;

public class LcuGameflowTests
{
    [Theory]
    [InlineData(SimPhase.Lobby, "Lobby")]
    [InlineData(SimPhase.Matchmaking, "Matchmaking")]
    [InlineData(SimPhase.ReadyCheck, "ReadyCheck")]
    [InlineData(SimPhase.ChampSelect, "ChampSelect")]
    [InlineData(SimPhase.InProgress, "InProgress")]
    public void Phases_use_lcu_names(SimPhase phase, string expected)
    {
        Assert.Equal(expected, LcuGameflow.ToGameflowPhase(phase));
    }

    [Fact]
    public void Every_phase_has_a_translation()
    {
        foreach (var phase in Enum.GetValues<SimPhase>())
        {
            Assert.False(string.IsNullOrEmpty(LcuGameflow.ToGameflowPhase(phase)));
        }
    }
}

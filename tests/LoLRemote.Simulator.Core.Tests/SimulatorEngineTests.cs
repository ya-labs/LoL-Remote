namespace LoLRemote.Simulator.Core.Tests;

public class SimulatorEngineTests
{
    private static readonly SimulatorTimings Timings = new(
        matchmaking: TimeSpan.FromSeconds(5),
        readyCheck: TimeSpan.FromSeconds(10),
        ban: TimeSpan.FromSeconds(20),
        pick: TimeSpan.FromSeconds(20),
        finalization: TimeSpan.FromSeconds(5));

    private readonly ManualTimeProvider _clock = new();
    private readonly SimulatorEngine _engine;

    public SimulatorEngineTests()
    {
        _engine = new SimulatorEngine(_clock, Timings);
    }

    [Fact]
    public void Starts_in_lobby_without_deadline()
    {
        Assert.Equal(SimPhase.Lobby, _engine.State.Phase);
        Assert.Null(_engine.State.Deadline);
        Assert.Null(_engine.Remaining);
    }

    [Fact]
    public void Find_match_enters_matchmaking_and_times_out_into_ready_check()
    {
        Click(RegionIds.FindMatch);
        Assert.Equal(SimPhase.Matchmaking, _engine.State.Phase);
        Assert.Equal(TimeSpan.FromSeconds(5), _engine.Remaining);

        _clock.Advance(TimeSpan.FromSeconds(4.9));
        _engine.Update();
        Assert.Equal(SimPhase.Matchmaking, _engine.State.Phase);

        _clock.Advance(TimeSpan.FromSeconds(0.1));
        _engine.Update();
        Assert.Equal(SimPhase.ReadyCheck, _engine.State.Phase);
        Assert.Equal(ReadyCheckResponse.None, _engine.State.ReadyCheck);
    }

    [Fact]
    public void Cancel_matchmaking_returns_to_lobby()
    {
        Click(RegionIds.FindMatch);
        var result = Click(RegionIds.CancelMatchmaking);

        Assert.Equal(ClickOutcome.Applied, result.Outcome);
        Assert.Equal(SimPhase.Lobby, _engine.State.Phase);
    }

    [Fact]
    public void Accepted_ready_check_waits_for_timer_then_enters_ban()
    {
        GoToReadyCheck();

        Assert.Equal(ClickOutcome.Applied, Click(RegionIds.Accept).Outcome);
        Assert.Equal(SimPhase.ReadyCheck, _engine.State.Phase);
        Assert.Equal(ReadyCheckResponse.Accepted, _engine.State.ReadyCheck);

        _clock.Advance(Timings.ReadyCheck);
        _engine.Update();

        Assert.Equal(SimPhase.ChampSelect, _engine.State.Phase);
        Assert.Equal(ChampSelectStage.Ban, _engine.State.Stage);
    }

    [Fact]
    public void Accept_or_decline_after_accepting_is_ignored()
    {
        GoToReadyCheck();
        Click(RegionIds.Accept);

        Assert.Equal(ClickOutcome.Ignored, Click(RegionIds.Accept).Outcome);
        Assert.Equal(ClickOutcome.Ignored, Click(RegionIds.Decline).Outcome);
        Assert.Equal(SimPhase.ReadyCheck, _engine.State.Phase);
    }

    [Fact]
    public void Decline_returns_to_lobby_immediately()
    {
        GoToReadyCheck();

        Click(RegionIds.Decline);

        Assert.Equal(SimPhase.Lobby, _engine.State.Phase);
        Assert.NotNull(_engine.State.LastEvent);
    }

    [Fact]
    public void Unanswered_ready_check_returns_to_lobby()
    {
        GoToReadyCheck();

        _clock.Advance(Timings.ReadyCheck);
        _engine.Update();

        Assert.Equal(SimPhase.Lobby, _engine.State.Phase);
    }

    [Fact]
    public void Lock_in_without_hovered_champion_is_ignored()
    {
        GoToBan();

        Assert.Equal(ClickOutcome.Ignored, Click(RegionIds.LockIn).Outcome);
        Assert.Equal(ChampSelectStage.Ban, _engine.State.Stage);
    }

    [Fact]
    public void Full_flow_ban_pick_finalization_in_progress()
    {
        GoToBan();

        Click(RegionIds.Champion(2));
        Assert.Equal(2, _engine.State.HoveredChampion);
        Click(RegionIds.LockIn);
        Assert.Equal(ChampSelectStage.Pick, _engine.State.Stage);
        Assert.Equal(2, _engine.State.BannedChampion);
        Assert.Null(_engine.State.HoveredChampion);

        Click(RegionIds.Champion(7));
        Click(RegionIds.LockIn);
        Assert.Equal(ChampSelectStage.Finalization, _engine.State.Stage);
        Assert.Equal(7, _engine.State.PickedChampion);

        _clock.Advance(Timings.Finalization);
        _engine.Update();
        Assert.Equal(SimPhase.InProgress, _engine.State.Phase);
        Assert.Null(_engine.State.Deadline);
    }

    [Fact]
    public void Banned_champion_cannot_be_picked()
    {
        GoToBan();
        Click(RegionIds.Champion(3));
        Click(RegionIds.LockIn);

        var result = Click(RegionIds.Champion(3));

        Assert.Equal(ClickOutcome.Ignored, result.Outcome);
        Assert.Null(_engine.State.HoveredChampion);
    }

    [Fact]
    public void Ban_timeout_moves_to_pick_without_ban()
    {
        GoToBan();
        Click(RegionIds.Champion(1));

        _clock.Advance(Timings.Ban);
        _engine.Update();

        Assert.Equal(ChampSelectStage.Pick, _engine.State.Stage);
        Assert.Null(_engine.State.BannedChampion);
        Assert.Null(_engine.State.HoveredChampion);
    }

    [Fact]
    public void Pick_timeout_dodges_back_to_lobby()
    {
        GoToBan();
        Click(RegionIds.Champion(0));
        Click(RegionIds.LockIn);

        _clock.Advance(Timings.Pick);
        _engine.Update();

        Assert.Equal(SimPhase.Lobby, _engine.State.Phase);
        Assert.Null(_engine.State.BannedChampion);
    }

    [Fact]
    public void Clicks_during_gameplay_are_counted_as_violations_and_change_nothing()
    {
        GoToInProgress();
        var before = _engine.State;

        var result = _engine.Click(640, 360);

        Assert.Equal(ClickOutcome.GameplayViolation, result.Outcome);
        Assert.Equal(1, _engine.GameplayViolationCount);
        Assert.Equal(before, _engine.State);
    }

    [Fact]
    public void End_game_returns_to_lobby()
    {
        GoToInProgress();

        _engine.EndGame();

        Assert.Equal(SimPhase.Lobby, _engine.State.Phase);
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(10, -0.001)]
    [InlineData(1280, 10)]
    [InlineData(10, 720)]
    [InlineData(double.NaN, 10)]
    public void Out_of_bounds_clicks_are_rejected(double x, double y)
    {
        Assert.Equal(ClickOutcome.OutOfBounds, _engine.Click(x, y).Outcome);
        Assert.Equal(SimPhase.Lobby, _engine.State.Phase);
    }

    [Fact]
    public void Click_outside_regions_is_a_miss()
    {
        Assert.Equal(ClickOutcome.Missed, _engine.Click(5, 5).Outcome);
    }

    [Fact]
    public void Click_applies_pending_expiration_before_hit_testing()
    {
        GoToReadyCheck();
        _clock.Advance(Timings.ReadyCheck);

        // O prazo já passou: o clique em "aceitar" chega tarde e atinge a sala.
        var result = Click(RegionIds.Accept);

        Assert.Equal(SimPhase.Lobby, result.Phase);
        Assert.Equal(ClickOutcome.Missed, result.Outcome);
        Assert.Equal(SimPhase.Lobby, _engine.State.Phase);
    }

    [Fact]
    public void Click_log_is_bounded_and_newest_first()
    {
        for (var i = 0; i < SimulatorEngine.ClickLogCapacity + 5; i++)
        {
            _engine.Click(i, 1);
        }

        Assert.Equal(SimulatorEngine.ClickLogCapacity, _engine.RecentClicks.Count);
        Assert.Equal(SimulatorEngine.ClickLogCapacity + 4, _engine.RecentClicks.First().X);
    }

    [Fact]
    public void Reset_clears_state_and_counters()
    {
        GoToInProgress();
        _engine.Click(1, 1);

        _engine.Reset();

        Assert.Equal(SimulatorState.Initial, _engine.State);
        Assert.Equal(0, _engine.GameplayViolationCount);
        Assert.Empty(_engine.RecentClicks);
    }

    [Fact]
    public void State_changed_is_raised_on_transitions()
    {
        var phases = new List<SimPhase>();
        _engine.StateChanged += (_, s) => phases.Add(s.Phase);

        Click(RegionIds.FindMatch);
        _clock.Advance(Timings.Matchmaking);
        _engine.Update();

        Assert.Equal(new[] { SimPhase.Matchmaking, SimPhase.ReadyCheck }, phases);
    }

    [Fact]
    public void Timings_reject_non_positive_durations()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SimulatorTimings(TimeSpan.Zero, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)));
    }

    /// <summary>Clica no centro da área usando o layout do estado conhecido (sem chamar Update).</summary>
    private ClickRecord Click(string regionId)
    {
        var bounds = SimLayout.RegionsFor(_engine.State.Phase, _engine.State.Stage)
            .Single(r => r.Id == regionId).Bounds;
        return _engine.Click(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));
    }

    private void GoToReadyCheck()
    {
        Click(RegionIds.FindMatch);
        _clock.Advance(Timings.Matchmaking);
        _engine.Update();
        Assert.Equal(SimPhase.ReadyCheck, _engine.State.Phase);
    }

    private void GoToBan()
    {
        GoToReadyCheck();
        Click(RegionIds.Accept);
        _clock.Advance(Timings.ReadyCheck);
        _engine.Update();
        Assert.Equal(ChampSelectStage.Ban, _engine.State.Stage);
    }

    private void GoToInProgress()
    {
        GoToBan();
        Click(RegionIds.Champion(0));
        Click(RegionIds.LockIn);
        Click(RegionIds.Champion(1));
        Click(RegionIds.LockIn);
        _clock.Advance(Timings.Finalization);
        _engine.Update();
        Assert.Equal(SimPhase.InProgress, _engine.State.Phase);
    }
}

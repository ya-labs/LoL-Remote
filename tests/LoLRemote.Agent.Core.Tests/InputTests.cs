using LoLRemote.Agent.Core.Geometry;
using LoLRemote.Agent.Core.Input;

namespace LoLRemote.Agent.Core.Tests;

public class InputTests
{
    private static readonly InputGateState Open = new(
        TargetValid: true,
        TargetMinimized: false,
        CaptureFresh: true,
        Phase: GamePhase.ChampSelect,
        LocalActivity: false,
        RemoteModeActive: true);

    private static readonly VideoLayout Layout = VideoLayout.Fit(1280, 720, 1280, 720);
    private static readonly PixelRect Client = new(0, 0, 1280, 720);

    [Theory]
    [InlineData(GamePhase.None, true)]
    [InlineData(GamePhase.Lobby, true)]
    [InlineData(GamePhase.Matchmaking, true)]
    [InlineData(GamePhase.ReadyCheck, true)]
    [InlineData(GamePhase.ChampSelect, true)]
    [InlineData(GamePhase.EndOfGame, true)]
    [InlineData(GamePhase.GameStart, false)]
    [InlineData(GamePhase.InProgress, false)]
    [InlineData(GamePhase.Reconnect, false)]
    [InlineData(GamePhase.WaitingForStats, false)]
    [InlineData(GamePhase.PreEndOfGame, false)]
    [InlineData(GamePhase.Unknown, false)]
    public void Only_client_screens_allow_input(GamePhase phase, bool allowed)
    {
        Assert.Equal(allowed, GamePhases.AllowsInput(phase));
    }

    [Theory]
    [InlineData("InProgress", GamePhase.InProgress)]
    [InlineData("ChampSelect", GamePhase.ChampSelect)]
    [InlineData("champselect", GamePhase.Unknown)]
    [InlineData("TerminatedInError", GamePhase.Unknown)]
    [InlineData("7", GamePhase.Unknown)]
    [InlineData("Unknown", GamePhase.Unknown)]
    [InlineData("", GamePhase.Unknown)]
    [InlineData(null, GamePhase.Unknown)]
    public void Phase_parsing_fails_closed(string? value, GamePhase expected)
    {
        Assert.Equal(expected, GamePhases.Parse(value));
    }

    [Fact]
    public void Open_gate_allows_input()
    {
        Assert.Null(InputGate.Evaluate(Open));
    }

    [Fact]
    public void Gate_blocks_in_the_safest_order()
    {
        var everythingWrong = new InputGateState(false, true, false, GamePhase.InProgress, true, false);

        Assert.Equal(InputRejectReason.RemoteModeInactive, InputGate.Evaluate(everythingWrong));
        Assert.Equal(InputRejectReason.PhaseBlocked, InputGate.Evaluate(everythingWrong with { RemoteModeActive = true }));
        Assert.Equal(InputRejectReason.PhaseUnknown, InputGate.Evaluate(Open with { Phase = GamePhase.Unknown }));
        Assert.Equal(InputRejectReason.TargetUnavailable, InputGate.Evaluate(Open with { TargetValid = false }));
        Assert.Equal(InputRejectReason.TargetMinimized, InputGate.Evaluate(Open with { TargetMinimized = true }));
        Assert.Equal(InputRejectReason.CaptureStale, InputGate.Evaluate(Open with { CaptureFresh = false }));
        Assert.Equal(InputRejectReason.LocalActivity, InputGate.Evaluate(Open with { LocalActivity = true }));
    }

    [Fact]
    public void Valid_tap_is_accepted_with_client_point()
    {
        var validator = new InputValidator();

        var decision = validator.Validate(Tap(1, 1000), 1000, Open, Layout, Client);

        Assert.True(decision.Accepted);
        Assert.Equal(new ClientPoint(640, 360), decision.Point);
    }

    [Fact]
    public void Unsupported_version_is_refused()
    {
        var decision = new InputValidator().Validate(Tap(1, 1000) with { Version = 2 }, 1000, Open, Layout, Client);

        Assert.Equal(InputRejectReason.UnsupportedVersion, decision.Reason);
    }

    [Fact]
    public void Replayed_or_older_sequence_is_refused()
    {
        var validator = new InputValidator();
        validator.Validate(Tap(5, 1000), 1000, Open, Layout, Client);

        Assert.Equal(InputRejectReason.OutOfOrder, validator.Validate(Tap(5, 1010), 1010, Open, Layout, Client).Reason);
        Assert.Equal(InputRejectReason.OutOfOrder, validator.Validate(Tap(4, 1010), 1010, Open, Layout, Client).Reason);
    }

    [Fact]
    public void Sequence_is_consumed_even_when_command_is_blocked()
    {
        var validator = new InputValidator();
        var blocked = Open with { Phase = GamePhase.InProgress };

        Assert.Equal(InputRejectReason.PhaseBlocked, validator.Validate(Tap(7, 1000), 1000, blocked, Layout, Client).Reason);
        Assert.Equal(InputRejectReason.OutOfOrder, validator.Validate(Tap(7, 1001), 1001, Open, Layout, Client).Reason);
        Assert.Equal(7, validator.LastSequence);
    }

    [Fact]
    public void Old_and_future_commands_are_refused()
    {
        var validator = new InputValidator();

        Assert.Equal(InputRejectReason.Expired, validator.Validate(Tap(1, 1000), 2001, Open, Layout, Client).Reason);
        Assert.True(validator.Validate(Tap(2, 1000), 2000, Open, Layout, Client).Accepted);
        Assert.Equal(InputRejectReason.FromFuture, validator.Validate(Tap(3, 2300), 2000, Open, Layout, Client).Reason);
    }

    [Fact]
    public void More_than_twenty_taps_per_second_are_rate_limited()
    {
        var validator = new InputValidator();
        for (var i = 1; i <= 20; i++)
        {
            Assert.True(validator.Validate(Tap(i, 1000 + i), 1000 + i, Open, Layout, Client).Accepted);
        }

        Assert.Equal(InputRejectReason.RateLimited, validator.Validate(Tap(21, 1021), 1021, Open, Layout, Client).Reason);
        Assert.True(validator.Validate(Tap(22, 2002), 2002, Open, Layout, Client).Accepted);
    }

    [Fact]
    public void Gate_reason_is_reported()
    {
        var decision = new InputValidator().Validate(Tap(1, 1000), 1000, Open with { TargetMinimized = true }, Layout, Client);

        Assert.Equal(InputRejectReason.TargetMinimized, decision.Reason);
        Assert.Null(decision.Point);
    }

    [Fact]
    public void Letterbox_touch_is_reported()
    {
        var wide = VideoLayout.Fit(2560, 720, 1280, 720);
        var command = Tap(1, 1000) with { Touch = new NormalizedPoint(0.5, 0.05) };

        Assert.Equal(InputRejectReason.Letterbox, new InputValidator().Validate(command, 1000, Open, wide, new PixelRect(0, 0, 2560, 720)).Reason);
    }

    private static TapCommand Tap(long sequence, long sentAt) =>
        new(InputLimits.ProtocolVersion, sequence, sentAt, new NormalizedPoint(0.5, 0.5));
}

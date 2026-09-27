using LoLRemote.Agent.Core.Geometry;
using LoLRemote.Agent.Core.Input;

namespace LoLRemote.Agent.Core.Tests;

public class InputKindsTests
{
    private static readonly InputGateState Open = new(true, false, true, GamePhase.ChampSelect, false, true);
    private static readonly VideoLayout Layout = VideoLayout.Fit(1280, 720, 1280, 720);
    private static readonly PixelRect Client = new(0, 0, 1280, 720);
    private static readonly NormalizedPoint Center = new(0.5, 0.5);

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(5)]
    [InlineData(-5)]
    public void Scroll_within_limits_is_accepted_at_the_point(int notches)
    {
        var decision = new InputValidator().Validate(new ScrollCommand(1, 1, 1000, Center, notches), 1000, Open, Layout, Client);

        Assert.True(decision.Accepted);
        Assert.Equal(new ClientPoint(640, 360), decision.Point);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-6)]
    public void Scroll_out_of_limits_is_refused(int notches)
    {
        var decision = new InputValidator().Validate(new ScrollCommand(1, 1, 1000, Center, notches), 1000, Open, Layout, Client);

        Assert.Equal(InputRejectReason.InvalidScroll, decision.Reason);
    }

    [Theory]
    [InlineData("Ahri")]
    [InlineData("ç ã é")]
    [InlineData("gg 👍")]
    public void Printable_text_is_accepted(string text)
    {
        var decision = new InputValidator().Validate(new TextCommand(1, 1, 1000, text), 1000, Open);

        Assert.True(decision.Accepted);
        Assert.Null(decision.Point);
    }

    [Theory]
    [InlineData("")]
    [InlineData("linha\nnova")]
    [InlineData("tab\t")]
    [InlineData("\u200Binvisivel")]
    public void Unacceptable_text_is_refused(string text)
    {
        var decision = new InputValidator().Validate(new TextCommand(1, 1, 1000, text), 1000, Open);

        Assert.Equal(InputRejectReason.InvalidText, decision.Reason);
    }

    [Fact]
    public void Broken_surrogate_pair_is_refused()
    {
        // Montado em tempo de execução: atributos não preservam surrogate isolado.
        var text = "a" + (char)0xD83D;

        Assert.Equal(InputRejectReason.InvalidText, new InputValidator().Validate(new TextCommand(1, 1, 1000, text), 1000, Open).Reason);
    }

    [Fact]
    public void Text_longer_than_limit_is_refused()
    {
        var text = new string('a', InputLimits.MaxTextLength + 1);

        Assert.Equal(InputRejectReason.InvalidText, new InputValidator().Validate(new TextCommand(1, 1, 1000, text), 1000, Open).Reason);
    }

    [Fact]
    public void Keyboard_is_blocked_during_gameplay_like_taps()
    {
        var inGame = Open with { Phase = GamePhase.InProgress };
        var validator = new InputValidator();

        Assert.Equal(InputRejectReason.PhaseBlocked, validator.Validate(new TextCommand(1, 1, 1000, "oi"), 1000, inGame).Reason);
        Assert.Equal(InputRejectReason.PhaseBlocked, validator.Validate(new KeyCommand(1, 2, 1000, SpecialKey.Enter), 1000, inGame).Reason);
    }

    [Fact]
    public void Unknown_key_value_is_refused()
    {
        var decision = new InputValidator().Validate(new KeyCommand(1, 1, 1000, (SpecialKey)99), 1000, Open);

        Assert.Equal(InputRejectReason.InvalidKey, decision.Reason);
    }

    [Fact]
    public void Sequence_is_shared_between_input_kinds()
    {
        var validator = new InputValidator();
        Assert.True(validator.Validate(new TextCommand(1, 5, 1000, "a"), 1000, Open).Accepted);

        var replay = validator.Validate(new TapCommand(1, 5, 1000, Center), 1000, Open, Layout, Client);

        Assert.Equal(InputRejectReason.OutOfOrder, replay.Reason);
    }
}

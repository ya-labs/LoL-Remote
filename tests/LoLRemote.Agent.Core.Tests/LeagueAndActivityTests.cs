using LoLRemote.Agent.Core.Geometry;
using LoLRemote.Agent.Core.Input;
using LoLRemote.Agent.Core.League;

namespace LoLRemote.Agent.Core.Tests;

public class LeagueAndActivityTests
{
    [Fact]
    public void Lockfile_in_lcu_format_is_parsed()
    {
        Assert.True(Lockfile.TryParse("LeagueClient:12345:54321:s3cr3t-Pass_word:https\r\n", out var lockfile));

        Assert.Equal("LeagueClient", lockfile!.Name);
        Assert.Equal(12345, lockfile.ProcessId);
        Assert.Equal(54321, lockfile.Port);
        Assert.Equal("s3cr3t-Pass_word", lockfile.Password);
        Assert.Equal(new Uri("https://127.0.0.1:54321/"), lockfile.BaseAddress);
    }

    [Theory]
    [InlineData((string?)null)]
    [InlineData("")]
    [InlineData("LeagueClient:1:2:pw")]
    [InlineData("LeagueClient:1:2:pw:https:extra")]
    [InlineData("LeagueClient:abc:2:pw:https")]
    [InlineData("LeagueClient:1:70000:pw:https")]
    [InlineData("LeagueClient:1:0:pw:https")]
    [InlineData("LeagueClient:1:2::https")]
    [InlineData("LeagueClient:1:2:pw:ftp")]
    [InlineData(":1:2:pw:http")]
    [InlineData("LeagueClient:-1:2:pw:http")]
    public void Malformed_lockfile_is_refused(string? content)
    {
        Assert.False(Lockfile.TryParse(content, out _));
    }

    [Fact]
    public void Lockfile_text_never_shows_the_password()
    {
        Assert.True(Lockfile.TryParse("LeagueClient:1:2:supersecret:https", out var lockfile));

        Assert.DoesNotContain("supersecret", lockfile!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Recent_local_input_is_activity()
    {
        Assert.True(LocalActivity.IsActive(nowTick: 20_000, lastInputTick: 15_000, lastInjectedTick: null));
    }

    [Fact]
    public void Old_local_input_is_not_activity()
    {
        Assert.False(LocalActivity.IsActive(nowTick: 30_000, lastInputTick: 15_000, lastInjectedTick: null));
    }

    [Fact]
    public void Input_injected_by_the_agent_is_ignored()
    {
        Assert.False(LocalActivity.IsActive(nowTick: 20_000, lastInputTick: 19_050, lastInjectedTick: 19_000));
    }

    [Fact]
    public void Local_input_after_injection_is_activity()
    {
        Assert.True(LocalActivity.IsActive(nowTick: 20_000, lastInputTick: 19_500, lastInjectedTick: 19_000));
    }

    [Fact]
    public void Tick_counter_wraparound_is_handled()
    {
        const uint nearMax = uint.MaxValue - 1_000;

        Assert.True(LocalActivity.IsActive(nowTick: 2_000, lastInputTick: nearMax, lastInjectedTick: null));
        Assert.False(LocalActivity.IsActive(nowTick: 2_000, lastInputTick: nearMax + 60, lastInjectedTick: nearMax));
    }

    [Fact]
    public void Client_area_is_located_inside_the_frame()
    {
        var frame = new PixelRect(100, 50, 1000, 600);
        var client = new PixelRect(101, 81, 998, 568);

        Assert.Equal(new PixelRect(1, 31, 998, 568), CoordinateMapper.ClientAreaInFrame(frame, client));
    }

    [Fact]
    public void Client_area_outside_the_frame_is_empty()
    {
        var frame = new PixelRect(0, 0, 100, 100);

        Assert.True(CoordinateMapper.ClientAreaInFrame(frame, new PixelRect(200, 200, 50, 50)).IsEmpty);
    }
}

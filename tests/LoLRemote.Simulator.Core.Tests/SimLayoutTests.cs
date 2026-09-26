namespace LoLRemote.Simulator.Core.Tests;

public class SimLayoutTests
{
    public static TheoryData<SimPhase, ChampSelectStage> AllScreens => new()
    {
        { SimPhase.Lobby, ChampSelectStage.None },
        { SimPhase.Matchmaking, ChampSelectStage.None },
        { SimPhase.ReadyCheck, ChampSelectStage.None },
        { SimPhase.ChampSelect, ChampSelectStage.Ban },
        { SimPhase.ChampSelect, ChampSelectStage.Pick },
        { SimPhase.InProgress, ChampSelectStage.None },
    };

    [Theory]
    [MemberData(nameof(AllScreens))]
    public void Regions_are_inside_reference_bounds_and_do_not_overlap(SimPhase phase, ChampSelectStage stage)
    {
        var regions = SimLayout.RegionsFor(phase, stage);

        Assert.NotEmpty(regions);
        foreach (var region in regions)
        {
            Assert.True(region.Bounds.X >= 0 && region.Bounds.Y >= 0, region.Id);
            Assert.True(region.Bounds.Right <= SimLayout.ReferenceWidth, region.Id);
            Assert.True(region.Bounds.Bottom <= SimLayout.ReferenceHeight, region.Id);
        }

        for (var i = 0; i < regions.Count; i++)
        {
            for (var j = i + 1; j < regions.Count; j++)
            {
                Assert.False(Overlaps(regions[i].Bounds, regions[j].Bounds), $"{regions[i].Id} x {regions[j].Id}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllScreens))]
    public void Region_ids_are_unique(SimPhase phase, ChampSelectStage stage)
    {
        var ids = SimLayout.RegionsFor(phase, stage).Select(r => r.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Finalization_has_no_clickable_regions()
    {
        Assert.Empty(SimLayout.RegionsFor(SimPhase.ChampSelect, ChampSelectStage.Finalization));
    }

    [Fact]
    public void Hit_test_uses_inclusive_start_and_exclusive_end()
    {
        var accept = SimLayout.RegionsFor(SimPhase.ReadyCheck, ChampSelectStage.None)
            .Single(r => r.Id == RegionIds.Accept).Bounds;

        Assert.Equal(RegionIds.Accept, SimLayout.HitTest(SimPhase.ReadyCheck, ChampSelectStage.None, accept.X, accept.Y)?.Id);
        Assert.Null(SimLayout.HitTest(SimPhase.ReadyCheck, ChampSelectStage.None, accept.Right, accept.Y));
        Assert.Null(SimLayout.HitTest(SimPhase.ReadyCheck, ChampSelectStage.None, accept.X, accept.Bottom));
    }

    [Fact]
    public void Champion_ids_round_trip()
    {
        for (var i = 0; i < SimLayout.ChampionCount; i++)
        {
            Assert.True(RegionIds.TryParseChampion(RegionIds.Champion(i), out var parsed));
            Assert.Equal(i, parsed);
        }

        Assert.False(RegionIds.TryParseChampion(RegionIds.LockIn, out _));
        Assert.False(RegionIds.TryParseChampion("champion--1", out _));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(SimLayout.ChampionCount)]
    public void Champion_cell_rejects_invalid_index(int index)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SimLayout.ChampionCell(index));
    }

    private static bool Overlaps(RectF a, RectF b) =>
        a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;
}

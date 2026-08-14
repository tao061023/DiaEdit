namespace DiaEditCore.Tests.Algorithm.CacheBuilder;

using DiaEditCore.Algorithm.CacheBuilder;
using DiaEditCore.Model;
using DiaEditCore.Model.TimeTable;
using Xunit;

public class TemporaryRestrictionBySegmentIndexBuilderTests
{
    private static readonly DateRange SampleRange = new(
        new DateTime(2026, 1, 1), new DateTime(2026, 1, 2));

    private static TemporaryRestriction MakeSegmentRestriction(int id, int segId) => new(
        new TemporaryRestrictionId(id),
        new RestrictionTarget.Segment(new StationConnectionSegmentId(segId)),
        ExtraRunTimeSec: 30,
        SpeedLimitKph: null,
        DateRange: SampleRange,
        Note: "");

    private static TemporaryRestriction MakeRailRestriction(int id, int railId) => new(
        new TemporaryRestrictionId(id),
        new RestrictionTarget.Rail(new RailId(railId)),
        ExtraRunTimeSec: null,
        SpeedLimitKph: 25,
        DateRange: SampleRange,
        Note: "");

    [Fact]
    public void Build_Segment対象のTemporaryRestrictionのみ登録される()
    {
        var restrictions = new[]
        {
            MakeSegmentRestriction(1, segId: 100),
            MakeRailRestriction(2, railId: 5), // Rail対象は対象外のはず
        };

        var result = TemporaryRestrictionBySegmentIndexBuilder.Build(restrictions);

        Assert.Single(result);
        Assert.Equal(
            new[] { new TemporaryRestrictionId(1) },
            result[new StationConnectionSegmentId(100)]);
    }

    [Fact]
    public void Build_同一Segmentに複数のTemporaryRestrictionが対象の場合すべて登録される()
    {
        var restrictions = new[]
        {
            MakeSegmentRestriction(1, segId: 100),
            MakeSegmentRestriction(2, segId: 100),
        };

        var result = TemporaryRestrictionBySegmentIndexBuilder.Build(restrictions);

        Assert.Equal(
            new[] { new TemporaryRestrictionId(1), new TemporaryRestrictionId(2) },
            result[new StationConnectionSegmentId(100)]);
    }

    [Fact]
    public void Build_Rail対象のみの場合は空辞書を返す()
    {
        var restrictions = new[] { MakeRailRestriction(1, railId: 5) };

        var result = TemporaryRestrictionBySegmentIndexBuilder.Build(restrictions);

        Assert.Empty(result);
    }

    [Fact]
    public void Build_空リストなら空辞書を返す()
    {
        var result = TemporaryRestrictionBySegmentIndexBuilder.Build(Array.Empty<TemporaryRestriction>());

        Assert.Empty(result);
    }
}
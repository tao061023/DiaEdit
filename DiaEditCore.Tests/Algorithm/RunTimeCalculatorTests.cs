using DiaEditCore.Algorithm;

using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.Stations;

using Xunit;

namespace DiaEditCore.Tests.Algorithm;

public class RunTimeCalculatorTests
{
    private static StationPath MakePath(int id, int adjustmentSec = 0) => new()
    {
        Id = new StationPathId(id),
        FloorUnitId = new FloorUnitId(1),
        Name = $"P{id}",
        Direction = StationPathDirection.Arrival,
        Waypoints = new List<StationPathWaypoint>(),
        AdjustmentSec = adjustmentSec
    };

    private static StationConnectionSegment MakeSegment(int id, int from, int to, int baseRunTimeSec) => new()
    {
        Id = new StationConnectionSegmentId(id),
        FromStationId = new StationId(from),
        ToStationId = new StationId(to),
        FromEntryPointId = new EntryPointId(from * 10),
        ToEntryPointId = new EntryPointId(to * 10),
        MainRouteId = new MainRouteId(1),
        LengthM = 1000,
        SpeedLimitKph = 100,
        BaseRunTimeSec = baseRunTimeSec
    };

    [Fact]
    public void NoAnchors_ReturnsBaselineSum()
    {
        var paths = new[] { MakePath(1), MakePath(2), MakePath(3) };
        var segments = new[]
        {
            MakeSegment(1, 1, 2, 100),
            MakeSegment(2, 2, 3, 200),
        };

        var result = RunTimeCalculator.Calculate(paths, segments, Array.Empty<RunTimeAnchor>(), AnchorMode.Auto);

        Assert.Equal(new[] { 100, 200 }, result.SegmentSeconds);
        Assert.Empty(result.ProposedAdjustments);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void AdjustmentSec_IsAddedFromBothEnds()
    {
        var paths = new[] { MakePath(1, adjustmentSec: 5), MakePath(2, adjustmentSec: 3), MakePath(3, adjustmentSec: 2) };
        var segments = new[]
        {
            MakeSegment(1, 1, 2, 100),
            MakeSegment(2, 2, 3, 200),
        };

        var result = RunTimeCalculator.Calculate(paths, segments, Array.Empty<RunTimeAnchor>(), AnchorMode.Auto);

        // segment0 = 100 + 5(departure@1) + 3(arrival@2) = 108
        // segment1 = 200 + 3(departure@2) + 2(arrival@3) = 205
        Assert.Equal(new[] { 108, 205 }, result.SegmentSeconds);
    }

    [Fact]
    public void Auto_DepartureAnchor_ResetsBasisForFollowingArrivalAnchor()
    {
        var paths = new[] { MakePath(1), MakePath(2), MakePath(3), MakePath(4) };
        var segments = new[]
        {
            MakeSegment(1, 1, 2, 100),
            MakeSegment(2, 2, 3, 100),
            MakeSegment(3, 3, 4, 100),
        };

        var anchors = new[]
        {
            new RunTimeAnchor(1, IsArrival: false, ActualElapsedSec: 100),
            new RunTimeAnchor(2, IsArrival: true,  ActualElapsedSec: 260),
        };

        var result = RunTimeCalculator.Calculate(paths, segments, anchors, AnchorMode.Auto);

        Assert.Equal(new[] { 100, 160, 100 }, result.SegmentSeconds);
        // Autoモードはその場でSegmentSecondsに適用するため、ProposedAdjustmentsは常に空
        Assert.Empty(result.ProposedAdjustments);
    }

    [Fact]
    public void Auto_ArrivalAnchor_AdjustsPrecedingSegment()
    {
        var paths = new[] { MakePath(1), MakePath(2), MakePath(3) };
        var segments = new[]
        {
            MakeSegment(1, 1, 2, 100),
            MakeSegment(2, 2, 3, 100),
        };

        // 駅2(index1)の到着が実測130秒 → segment[0]に+30吸収
        var anchors = new[] { new RunTimeAnchor(1, IsArrival: true, ActualElapsedSec: 130) };

        var result = RunTimeCalculator.Calculate(paths, segments, anchors, AnchorMode.Auto);

        Assert.Equal(new[] { 130, 100 }, result.SegmentSeconds);
    }

    [Fact]
    public void Auto_DepartureAnchorAtOrigin_ShiftsBasisForFirstSegment()
    {
        var paths = new[] { MakePath(1), MakePath(2), MakePath(3) };
        var segments = new[]
        {
            MakeSegment(1, 1, 2, 100),
            MakeSegment(2, 2, 3, 100),
        };

        var anchors = new[]
        {
            new RunTimeAnchor(0, IsArrival: false, ActualElapsedSec: 50),
            new RunTimeAnchor(1, IsArrival: true,  ActualElapsedSec: 180),
        };

        var result = RunTimeCalculator.Calculate(paths, segments, anchors, AnchorMode.Auto);

        Assert.Equal(new[] { 130, 100 }, result.SegmentSeconds);
        Assert.Empty(result.ProposedAdjustments);
    }

    [Fact]
    public void Auto_ArrivalDiffPropagatesToNextSegment_WhenNotConfirmed()
    {
        var paths = new[] { MakePath(1), MakePath(2), MakePath(3) };
        var segments = new[]
        {
            MakeSegment(1, 1, 2, 100),
            MakeSegment(2, 2, 3, 100),
        };

        // 出発アンカーなし。駅2(index1)の到着が実測150秒 → segment[0]が150に確定。
        // その後、駅3(index2)の到着(到着駅なので実質「最終到着」)が実測300秒 → segment[1] = 300 - 150 = 150
        var anchors = new[]
        {
            new RunTimeAnchor(1, IsArrival: true, ActualElapsedSec: 150),
            new RunTimeAnchor(2, IsArrival: true, ActualElapsedSec: 300),
        };

        var result = RunTimeCalculator.Calculate(paths, segments, anchors, AnchorMode.Auto);

        Assert.Equal(new[] { 150, 150 }, result.SegmentSeconds);
    }

    [Fact]
    public void Manual_ReturnsBaselineButProposesAdjustments()
    {
        var paths = new[] { MakePath(1), MakePath(2), MakePath(3) };
        var segments = new[]
        {
            MakeSegment(1, 1, 2, 100),
            MakeSegment(2, 2, 3, 100),
        };

        // 出発アンカー(index0)で基準時刻を60にリセットし、到着アンカー(index1)で実測200を要求
        // derived=60+100=160に対しdiff=+40 → segment0の提案値は140になるはず
        var anchors = new[]
        {
            new RunTimeAnchor(0, IsArrival: false, ActualElapsedSec: 60),
            new RunTimeAnchor(1, IsArrival: true,  ActualElapsedSec: 200),
        };

        var result = RunTimeCalculator.Calculate(paths, segments, anchors, AnchorMode.Manual);

        Assert.Equal(new[] { 100, 100 }, result.SegmentSeconds); // baselineのまま変更されない
        Assert.Single(result.ProposedAdjustments);
        Assert.Equal(0, result.ProposedAdjustments[0].SegmentIndex);
        Assert.Equal(100, result.ProposedAdjustments[0].OriginalSeconds);
        Assert.Equal(140, result.ProposedAdjustments[0].AdjustedSeconds);
    }

    [Fact]
    public void Disabled_ReturnsBaselineAndWarnsWhenActualBelowDerived()
    {
        var paths = new[] { MakePath(1), MakePath(2), MakePath(3) };
        var segments = new[]
        {
            MakeSegment(1, 1, 2, 100),
            MakeSegment(2, 2, 3, 100),
        };

        // derived at station1 = 100. 実測90 < 100 → 警告
        var anchors = new[] { new RunTimeAnchor(1, IsArrival: true, ActualElapsedSec: 90) };

        var result = RunTimeCalculator.Calculate(paths, segments, anchors, AnchorMode.Disabled);

        Assert.Equal(new[] { 100, 100 }, result.SegmentSeconds);
        Assert.Single(result.Warnings);
        Assert.Equal(100, result.Warnings[0].DerivedElapsedSec);
        Assert.Equal(90, result.Warnings[0].ActualElapsedSec);
    }

    [Fact]
    public void Disabled_NoWarning_WhenActualMeetsOrExceedsDerived()
    {
        var paths = new[] { MakePath(1), MakePath(2), MakePath(3) };
        var segments = new[]
        {
            MakeSegment(1, 1, 2, 100),
            MakeSegment(2, 2, 3, 100),
        };

        var anchors = new[] { new RunTimeAnchor(1, IsArrival: true, ActualElapsedSec: 100) };

        var result = RunTimeCalculator.Calculate(paths, segments, anchors, AnchorMode.Disabled);

        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Throws_WhenStationPathCountMismatch()
    {
        var paths = new[] { MakePath(1), MakePath(2) };
        var segments = new[]
        {
            MakeSegment(1, 1, 2, 100),
            MakeSegment(2, 2, 3, 100),
        };

        Assert.Throws<ArgumentException>(() =>
            RunTimeCalculator.Calculate(paths, segments, Array.Empty<RunTimeAnchor>(), AnchorMode.Auto));
    }

    [Fact]
    public void Throws_WhenFewerThanTwoStationPaths()
    {
        var paths = new[] { MakePath(1) };
        var segments = Array.Empty<StationConnectionSegment>();

        Assert.Throws<ArgumentException>(() =>
            RunTimeCalculator.Calculate(paths, segments, Array.Empty<RunTimeAnchor>(), AnchorMode.Auto));
    }

    [Fact]
    public void Throws_WhenArrivalAnchorAtDepartureStation()
    {
        var paths = new[] { MakePath(1), MakePath(2) };
        var segments = new[] { MakeSegment(1, 1, 2, 100) };
        var anchors = new[] { new RunTimeAnchor(0, IsArrival: true, ActualElapsedSec: 0) };

        Assert.Throws<ArgumentException>(() =>
            RunTimeCalculator.Calculate(paths, segments, anchors, AnchorMode.Auto));
    }

    [Fact]
    public void Throws_WhenDepartureAnchorAtArrivalStation()
    {
        var paths = new[] { MakePath(1), MakePath(2) };
        var segments = new[] { MakeSegment(1, 1, 2, 100) };
        var anchors = new[] { new RunTimeAnchor(1, IsArrival: false, ActualElapsedSec: 0) };

        Assert.Throws<ArgumentException>(() =>
            RunTimeCalculator.Calculate(paths, segments, anchors, AnchorMode.Auto));
    }
}
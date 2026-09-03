namespace DiaEditCore.Tests.Algorithm;

using DiaEditCore.Algorithm;
using DiaEditCore.Algorithm.CacheBuilder;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;

using Xunit;

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

    private static RunTimeHopInput MakeHop(int id, bool fromIsStop = true, bool toIsStop = true)
        => new(new StationConnectionSegmentId(id), fromIsStop, toIsStop);

    /// <summary>
    /// テスト用のbaseRunTimeIndexを構築する。VehicleTypeIdはnull固定
    /// （RunTimeCalculator.Calculate自体の計算ロジック検証が目的のため、
    /// BaseRunTimeIndexBuilderによるTrain実績からの導出過程は別テストの対象とする）。
    /// </summary>
    private static Dictionary<BaseRunTimeIndexBuilder.SelectionKey, int> MakeIndex(
        params (int SegmentId, bool FromIsStop, bool ToIsStop, int Seconds)[] entries)
    {
        var index = new Dictionary<BaseRunTimeIndexBuilder.SelectionKey, int>();
        foreach (var (segmentId, fromIsStop, toIsStop, seconds) in entries)
        {
            var key = new BaseRunTimeIndexBuilder.SelectionKey(
                new StationConnectionSegmentId(segmentId), fromIsStop, toIsStop, null);
            index[key] = seconds;
        }
        return index;
    }

    private static IReadOnlyList<int?> HopSeconds(RunTimeCalculationResult result)
        => result.Hops.Select(h => h is HopRunTimeOk ok ? (int?)ok.Seconds : null).ToList();

    [Fact]
    public void NoAnchors_ReturnsBaselineSum()
    {
        var paths = new[] { MakePath(1), MakePath(2), MakePath(3) };
        var hops = new[] { MakeHop(1), MakeHop(2) };
        var index = MakeIndex((1, true, true, 100), (2, true, true, 200));

        var result = RunTimeCalculator.Calculate(paths, hops, index, null, Array.Empty<RunTimeAnchor>(), AnchorMode.Auto);

        Assert.Equal(new int?[] { 100, 200 }, HopSeconds(result));
        Assert.Empty(result.ProposedAdjustments);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void AdjustmentSec_IsAddedFromBothEnds()
    {
        var paths = new[] { MakePath(1, adjustmentSec: 5), MakePath(2, adjustmentSec: 3), MakePath(3, adjustmentSec: 2) };
        var hops = new[] { MakeHop(1), MakeHop(2) };
        var index = MakeIndex((1, true, true, 100), (2, true, true, 200));

        var result = RunTimeCalculator.Calculate(paths, hops, index, null, Array.Empty<RunTimeAnchor>(), AnchorMode.Auto);

        // hop0 = 100 + 5(departure@1) + 3(arrival@2) = 108
        // hop1 = 200 + 3(departure@2) + 2(arrival@3) = 205
        Assert.Equal(new int?[] { 108, 205 }, HopSeconds(result));
    }

    [Fact]
    public void Auto_DepartureAnchor_ResetsBasisForFollowingArrivalAnchor()
    {
        var paths = new[] { MakePath(1), MakePath(2), MakePath(3), MakePath(4) };
        var hops = new[] { MakeHop(1), MakeHop(2), MakeHop(3) };
        var index = MakeIndex((1, true, true, 100), (2, true, true, 100), (3, true, true, 100));

        var anchors = new[]
        {
            new RunTimeAnchor(1, IsArrival: false, ActualElapsedSec: 100),
            new RunTimeAnchor(2, IsArrival: true,  ActualElapsedSec: 260),
        };

        var result = RunTimeCalculator.Calculate(paths, hops, index, null, anchors, AnchorMode.Auto);

        Assert.Equal(new int?[] { 100, 160, 100 }, HopSeconds(result));
        // Autoモードはその場でHopsに適用するため、ProposedAdjustmentsは常に空
        Assert.Empty(result.ProposedAdjustments);
    }

    [Fact]
    public void Auto_ArrivalAnchor_AdjustsPrecedingSegment()
    {
        var paths = new[] { MakePath(1), MakePath(2), MakePath(3) };
        var hops = new[] { MakeHop(1), MakeHop(2) };
        var index = MakeIndex((1, true, true, 100), (2, true, true, 100));

        // 駅2(index1)の到着が実測130秒 → hop[0]に+30吸収
        var anchors = new[] { new RunTimeAnchor(1, IsArrival: true, ActualElapsedSec: 130) };

        var result = RunTimeCalculator.Calculate(paths, hops, index, null, anchors, AnchorMode.Auto);

        Assert.Equal(new int?[] { 130, 100 }, HopSeconds(result));
    }

    [Fact]
    public void Auto_DepartureAnchorAtOrigin_ShiftsBasisForFirstSegment()
    {
        var paths = new[] { MakePath(1), MakePath(2), MakePath(3) };
        var hops = new[] { MakeHop(1), MakeHop(2) };
        var index = MakeIndex((1, true, true, 100), (2, true, true, 100));

        var anchors = new[]
        {
            new RunTimeAnchor(0, IsArrival: false, ActualElapsedSec: 50),
            new RunTimeAnchor(1, IsArrival: true,  ActualElapsedSec: 180),
        };

        var result = RunTimeCalculator.Calculate(paths, hops, index, null, anchors, AnchorMode.Auto);

        Assert.Equal(new int?[] { 130, 100 }, HopSeconds(result));
        Assert.Empty(result.ProposedAdjustments);
    }

    [Fact]
    public void Auto_ArrivalDiffPropagatesToNextSegment_WhenNotConfirmed()
    {
        var paths = new[] { MakePath(1), MakePath(2), MakePath(3) };
        var hops = new[] { MakeHop(1), MakeHop(2) };
        var index = MakeIndex((1, true, true, 100), (2, true, true, 100));

        // 出発アンカーなし。駅2(index1)の到着が実測150秒 → hop[0]が150に確定。
        // その後、駅3(index2)の到着（最終到着）が実測300秒 → hop[1] = 300 - 150 = 150
        var anchors = new[]
        {
            new RunTimeAnchor(1, IsArrival: true, ActualElapsedSec: 150),
            new RunTimeAnchor(2, IsArrival: true, ActualElapsedSec: 300),
        };

        var result = RunTimeCalculator.Calculate(paths, hops, index, null, anchors, AnchorMode.Auto);

        Assert.Equal(new int?[] { 150, 150 }, HopSeconds(result));
    }

    [Fact]
    public void Manual_ReturnsBaselineButProposesAdjustments()
    {
        var paths = new[] { MakePath(1), MakePath(2), MakePath(3) };
        var hops = new[] { MakeHop(1), MakeHop(2) };
        var index = MakeIndex((1, true, true, 100), (2, true, true, 100));

        // 出発アンカー(index0)で基準時刻を60にリセットし、到着アンカー(index1)で実測200を要求
        // derived=60+100=160に対しdiff=+40 → hop0の提案値は140になるはず
        var anchors = new[]
        {
            new RunTimeAnchor(0, IsArrival: false, ActualElapsedSec: 60),
            new RunTimeAnchor(1, IsArrival: true,  ActualElapsedSec: 200),
        };

        var result = RunTimeCalculator.Calculate(paths, hops, index, null, anchors, AnchorMode.Manual);

        Assert.Equal(new int?[] { 100, 100 }, HopSeconds(result)); // baselineのまま変更されない
        Assert.Single(result.ProposedAdjustments);
        Assert.Equal(0, result.ProposedAdjustments[0].SegmentIndex);
        Assert.Equal(100, result.ProposedAdjustments[0].OriginalSeconds);
        Assert.Equal(140, result.ProposedAdjustments[0].AdjustedSeconds);
    }

    [Fact]
    public void Disabled_ReturnsBaselineAndWarnsWhenActualBelowDerived()
    {
        var paths = new[] { MakePath(1), MakePath(2), MakePath(3) };
        var hops = new[] { MakeHop(1), MakeHop(2) };
        var index = MakeIndex((1, true, true, 100), (2, true, true, 100));

        // derived at station1 = 100. 実測90 < 100 → 警告
        var anchors = new[] { new RunTimeAnchor(1, IsArrival: true, ActualElapsedSec: 90) };

        var result = RunTimeCalculator.Calculate(paths, hops, index, null, anchors, AnchorMode.Disabled);

        Assert.Equal(new int?[] { 100, 100 }, HopSeconds(result));
        Assert.Single(result.Warnings);
        Assert.Equal(100, result.Warnings[0].DerivedElapsedSec);
        Assert.Equal(90, result.Warnings[0].ActualElapsedSec);
    }

    [Fact]
    public void Disabled_NoWarning_WhenActualMeetsOrExceedsDerived()
    {
        var paths = new[] { MakePath(1), MakePath(2), MakePath(3) };
        var hops = new[] { MakeHop(1), MakeHop(2) };
        var index = MakeIndex((1, true, true, 100), (2, true, true, 100));

        var anchors = new[] { new RunTimeAnchor(1, IsArrival: true, ActualElapsedSec: 100) };

        var result = RunTimeCalculator.Calculate(paths, hops, index, null, anchors, AnchorMode.Disabled);

        Assert.Empty(result.Warnings);
    }

    // ── v12.27新設：Undefined（基準実績が見つからないホップ）関連 ──

    [Fact]
    public void Undefined_WhenIndexHasNoMatchingKey()
    {
        var paths = new[] { MakePath(1), MakePath(2), MakePath(3) };
        var hops = new[] { MakeHop(1), MakeHop(2) };
        // hop1(SegmentId=1)に対応するキーのみ登録、hop2(SegmentId=2)は未登録
        var index = MakeIndex((1, true, true, 100));

        var result = RunTimeCalculator.Calculate(paths, hops, index, null, Array.Empty<RunTimeAnchor>(), AnchorMode.Auto);

        Assert.IsType<HopRunTimeOk>(result.Hops[0]);
        Assert.Equal(100, ((HopRunTimeOk)result.Hops[0]).Seconds);
        Assert.IsType<HopRunTimeUndefined>(result.Hops[1]);
    }

    [Fact]
    public void Undefined_DoesNotPropagateToOtherIndependentHops()
    {
        // hop2がUndefinedでも、hop1・hop3のOk判定には影響しない
        // （§9.1項目20確定方針：区間ごとに個別判定。アンカーが関与しない独立ホップ間では
        // Undefinedの伝播自体が発生しない）
        var paths = new[] { MakePath(1), MakePath(2), MakePath(3), MakePath(4) };
        var hops = new[] { MakeHop(1), MakeHop(2), MakeHop(3) };
        var index = MakeIndex((1, true, true, 100), (3, true, true, 300)); // hop2(id=2)は未登録

        var result = RunTimeCalculator.Calculate(paths, hops, index, null, Array.Empty<RunTimeAnchor>(), AnchorMode.Auto);

        Assert.Equal(new int?[] { 100, null, 300 }, HopSeconds(result));
    }

    [Fact]
    public void Undefined_AnchorSpanningUndefinedHop_IsSkippedButOtherAnchorsStillApply()
    {
        // hop2(index1、SegmentId=2)がUndefined。これを跨ぐ到着アンカー(index2)による
        // 調整は行われない（そのアンカー区間だけがUndefined扱い）が、hop2を跨がない
        // hop1単独の到着アンカー(index1)による調整は独立して適用される想定。
        var paths = new[] { MakePath(1), MakePath(2), MakePath(3), MakePath(4) };
        var hops = new[] { MakeHop(1), MakeHop(2), MakeHop(3) };
        var index = MakeIndex((1, true, true, 100), (3, true, true, 100)); // hop2(id=2)は未登録

        var anchors = new[]
        {
            new RunTimeAnchor(1, IsArrival: true, ActualElapsedSec: 150), // hop1のみ確定させる
            new RunTimeAnchor(3, IsArrival: true, ActualElapsedSec: 999), // hop2を跨ぐためスキップされる想定
        };

        var result = RunTimeCalculator.Calculate(paths, hops, index, null, anchors, AnchorMode.Auto);

        Assert.Equal(new int?[] { 150, null, 100 }, HopSeconds(result));
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Undefined_DisabledMode_SkipsWarningForAnchorSpanningUndefinedHop()
    {
        var paths = new[] { MakePath(1), MakePath(2), MakePath(3) };
        var hops = new[] { MakeHop(1), MakeHop(2) };
        var index = MakeIndex((2, true, true, 100)); // hop1(id=1)は未登録

        // 最終到着アンカー(index2)はhop0(Undefined)を跨ぐため、derivedが求まらず警告対象外
        var anchors = new[] { new RunTimeAnchor(2, IsArrival: true, ActualElapsedSec: 50) };

        var result = RunTimeCalculator.Calculate(paths, hops, index, null, anchors, AnchorMode.Disabled);

        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Throws_WhenStationPathCountMismatch()
    {
        var paths = new[] { MakePath(1), MakePath(2) };
        var hops = new[] { MakeHop(1), MakeHop(2) };
        var index = MakeIndex((1, true, true, 100), (2, true, true, 100));

        Assert.Throws<ArgumentException>(() =>
            RunTimeCalculator.Calculate(paths, hops, index, null, Array.Empty<RunTimeAnchor>(), AnchorMode.Auto));
    }

    [Fact]
    public void Throws_WhenFewerThanTwoStationPaths()
    {
        var paths = new[] { MakePath(1) };
        var hops = Array.Empty<RunTimeHopInput>();
        var index = new Dictionary<BaseRunTimeIndexBuilder.SelectionKey, int>();

        Assert.Throws<ArgumentException>(() =>
            RunTimeCalculator.Calculate(paths, hops, index, null, Array.Empty<RunTimeAnchor>(), AnchorMode.Auto));
    }

    [Fact]
    public void Throws_WhenArrivalAnchorAtDepartureStation()
    {
        var paths = new[] { MakePath(1), MakePath(2) };
        var hops = new[] { MakeHop(1) };
        var index = MakeIndex((1, true, true, 100));
        var anchors = new[] { new RunTimeAnchor(0, IsArrival: true, ActualElapsedSec: 0) };

        Assert.Throws<ArgumentException>(() =>
            RunTimeCalculator.Calculate(paths, hops, index, null, anchors, AnchorMode.Auto));
    }

    [Fact]
    public void Throws_WhenDepartureAnchorAtArrivalStation()
    {
        var paths = new[] { MakePath(1), MakePath(2) };
        var hops = new[] { MakeHop(1) };
        var index = MakeIndex((1, true, true, 100));
        var anchors = new[] { new RunTimeAnchor(1, IsArrival: false, ActualElapsedSec: 0) };

        Assert.Throws<ArgumentException>(() =>
            RunTimeCalculator.Calculate(paths, hops, index, null, anchors, AnchorMode.Auto));
    }
}
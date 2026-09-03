namespace DiaEditCore.Tests.Algorithm.CacheBuilder;

using DiaEditCore.Algorithm.CacheBuilder;
using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.TimeTable.Trains;

using Xunit;

public class BaseRunTimeIndexBuilderTests
{
    private static Train MakeTrain(
        int id,
        VehicleTypeId? vehicleTypeId,
        params (StationId From, StationId To, StationConnectionId ScId)[] hops)
        => new()
        {
            Id = new TrainId(id),
            TimeTableSetId = new TimeTableSetId(1),
            TrainNumber = $"T{id}",
            ServiceRouteId = new ServiceRouteId(1),
            TrainTypeId = new TrainTypeId(1),
            TrainTypeName = new DisplayName { Name = "普通" },
            Nickname = new DisplayName { Name = "" },
            DefaultVehicleTypeId = vehicleTypeId,
            RunSegments = hops.Select(h => new TrainRunSegment
            {
                FromStationId = h.From,
                ToStationId = h.To,
                StationConnectionId = h.ScId,
            }).ToList(),
        };

    private static void SetStopTime(Train train, StopKey key, int arrival, int departure, bool isStop)
        => train.StopTimesInternal[key] = new StopTime
        {
            ArrivalSeconds = arrival,
            DepartureSeconds = departure,
            IsStop = isStop,
        };

    private static StationConnectionSegment MakeSegment(int id, int from, int to)
        => new()
        {
            Id = new StationConnectionSegmentId(id),
            StationIdA = new StationId(from),
            StationIdB = new StationId(to),
            EntryPointIdA = new EntryPointId(from * 10),
            EntryPointIdB = new EntryPointId(to * 10),
            MainRouteId = new MainRouteId(1),
        };

    private static StationConnection MakeConnection(int id, params StationConnectionSegmentId[] segIds)
        => new()
        {
            Id = new StationConnectionId(id),
            Name = "sc",
            MainRouteId = new MainRouteId(1),
            Direction = StationConnectionDirection.Down,
            Segments = segIds.ToList(),
        };

    [Fact]
    public void 単一ホップ_停車停車パターンで実測所要秒数を索引化する()
    {
        var stA = new StationId(1);
        var stB = new StationId(2);
        var seg = MakeSegment(1, 1, 2);
        var sc = MakeConnection(1, seg.Id);

        var train = MakeTrain(1, vehicleTypeId: null, (stA, stB, sc.Id));
        var keys = StopKeySequenceBuilder.BuildVisitedStopKeys(train);
        SetStopTime(train, keys[0], arrival: -1, departure: 100, isStop: true);
        SetStopTime(train, keys[1], arrival: 220, departure: 230, isStop: true);

        var index = BaseRunTimeIndexBuilder.Build(
            new List<Train> { train },
            new List<StationConnection> { sc },
            new List<StationConnectionSegment> { seg });

        var key = new BaseRunTimeIndexBuilder.SelectionKey(seg.Id, true, true, null);
        Assert.True(index.TryGetValue(key, out var seconds));
        Assert.Equal(120, seconds); // 220(到着) - 100(出発)
    }

    [Fact]
    public void 通過駅は到着側基準にDepartureSecondsを使う()
    {
        var stA = new StationId(1);
        var stB = new StationId(2);
        var seg = MakeSegment(1, 1, 2);
        var sc = MakeConnection(1, seg.Id);

        var train = MakeTrain(1, vehicleTypeId: null, (stA, stB, sc.Id));
        var keys = StopKeySequenceBuilder.BuildVisitedStopKeys(train);
        SetStopTime(train, keys[0], arrival: -1, departure: 100, isStop: true);
        // To側は通過（IsStop=false）：ArrivalSecondsは未設定(-1)のままでもDepartureSecondsを基準に使う
        SetStopTime(train, keys[1], arrival: -1, departure: 215, isStop: false);

        var index = BaseRunTimeIndexBuilder.Build(
            new List<Train> { train },
            new List<StationConnection> { sc },
            new List<StationConnectionSegment> { seg });

        var key = new BaseRunTimeIndexBuilder.SelectionKey(seg.Id, true, false, null);
        Assert.True(index.TryGetValue(key, out var seconds));
        Assert.Equal(115, seconds);
    }

    [Fact]
    public void 停車パターンが異なると別キーとして扱われる()
    {
        var stA = new StationId(1);
        var stB = new StationId(2);
        var seg = MakeSegment(1, 1, 2);
        var sc = MakeConnection(1, seg.Id);

        // train1：To側停車
        var train1 = MakeTrain(1, null, (stA, stB, sc.Id));
        var keys1 = StopKeySequenceBuilder.BuildVisitedStopKeys(train1);
        SetStopTime(train1, keys1[0], -1, 100, true);
        SetStopTime(train1, keys1[1], 220, 225, true);

        // train2：To側通過（同じSCS、異なる停車パターン）
        var train2 = MakeTrain(2, null, (stA, stB, sc.Id));
        var keys2 = StopKeySequenceBuilder.BuildVisitedStopKeys(train2);
        SetStopTime(train2, keys2[0], -1, 300, true);
        SetStopTime(train2, keys2[1], -1, 405, false);

        var index = BaseRunTimeIndexBuilder.Build(
            new List<Train> { train1, train2 },
            new List<StationConnection> { sc },
            new List<StationConnectionSegment> { seg });

        Assert.Equal(2, index.Count);
        Assert.Equal(120, index[new BaseRunTimeIndexBuilder.SelectionKey(seg.Id, true, true, null)]);
        Assert.Equal(105, index[new BaseRunTimeIndexBuilder.SelectionKey(seg.Id, true, false, null)]);
    }

    [Fact]
    public void VehicleTypeIdが異なると別キーとして扱われる()
    {
        var stA = new StationId(1);
        var stB = new StationId(2);
        var seg = MakeSegment(1, 1, 2);
        var sc = MakeConnection(1, seg.Id);

        var vt1 = new VehicleTypeId(1);
        var vt2 = new VehicleTypeId(2);

        var train1 = MakeTrain(1, vt1, (stA, stB, sc.Id));
        var keys1 = StopKeySequenceBuilder.BuildVisitedStopKeys(train1);
        SetStopTime(train1, keys1[0], -1, 0, true);
        SetStopTime(train1, keys1[1], 100, 110, true);

        var train2 = MakeTrain(2, vt2, (stA, stB, sc.Id));
        var keys2 = StopKeySequenceBuilder.BuildVisitedStopKeys(train2);
        SetStopTime(train2, keys2[0], -1, 0, true);
        SetStopTime(train2, keys2[1], 150, 160, true);

        var index = BaseRunTimeIndexBuilder.Build(
            new List<Train> { train1, train2 },
            new List<StationConnection> { sc },
            new List<StationConnectionSegment> { seg });

        Assert.Equal(100, index[new BaseRunTimeIndexBuilder.SelectionKey(seg.Id, true, true, vt1)]);
        Assert.Equal(150, index[new BaseRunTimeIndexBuilder.SelectionKey(seg.Id, true, true, vt2)]);
    }

    [Fact]
    public void 一致するSCSが複数件のホップは読み飛ばされる()
    {
        // 同一From/Toの重複SCS（不整合データ）を含むSC
        var stA = new StationId(1);
        var stB = new StationId(2);
        var seg1 = MakeSegment(1, 1, 2);
        var seg2 = MakeSegment(2, 1, 2); // 同一From/To
        var sc = MakeConnection(1, seg1.Id, seg2.Id);

        var train = MakeTrain(1, null, (stA, stB, sc.Id));
        var keys = StopKeySequenceBuilder.BuildVisitedStopKeys(train);
        SetStopTime(train, keys[0], -1, 0, true);
        SetStopTime(train, keys[1], 100, 110, true);

        var index = BaseRunTimeIndexBuilder.Build(
            new List<Train> { train },
            new List<StationConnection> { sc },
            new List<StationConnectionSegment> { seg1, seg2 });

        Assert.Empty(index);
    }

    [Fact]
    public void StationConnectionIdが存在しないホップは読み飛ばされる()
    {
        var stA = new StationId(1);
        var stB = new StationId(2);
        var seg = MakeSegment(1, 1, 2);

        var train = MakeTrain(1, null, (stA, stB, new StationConnectionId(999))); // 存在しないSC参照
        var keys = StopKeySequenceBuilder.BuildVisitedStopKeys(train);
        SetStopTime(train, keys[0], -1, 0, true);
        SetStopTime(train, keys[1], 100, 110, true);

        var index = BaseRunTimeIndexBuilder.Build(
            new List<Train> { train },
            new List<StationConnection>(),
            new List<StationConnectionSegment> { seg });

        Assert.Empty(index);
    }

    [Fact]
    public void DepartureSecondsが未設定の出発側は読み飛ばされる()
    {
        var stA = new StationId(1);
        var stB = new StationId(2);
        var seg = MakeSegment(1, 1, 2);
        var sc = MakeConnection(1, seg.Id);

        var train = MakeTrain(1, null, (stA, stB, sc.Id));
        var keys = StopKeySequenceBuilder.BuildVisitedStopKeys(train);
        SetStopTime(train, keys[0], -1, -1, true); // DepartureSeconds未設定
        SetStopTime(train, keys[1], 100, 110, true);

        var index = BaseRunTimeIndexBuilder.Build(
            new List<Train> { train },
            new List<StationConnection> { sc },
            new List<StationConnectionSegment> { seg });

        Assert.Empty(index);
    }

    [Fact]
    public void 到着側基準が出発側より前なら不整合として読み飛ばされる()
    {
        var stA = new StationId(1);
        var stB = new StationId(2);
        var seg = MakeSegment(1, 1, 2);
        var sc = MakeConnection(1, seg.Id);

        var train = MakeTrain(1, null, (stA, stB, sc.Id));
        var keys = StopKeySequenceBuilder.BuildVisitedStopKeys(train);
        SetStopTime(train, keys[0], -1, 300, true);
        SetStopTime(train, keys[1], 100, 110, true); // 到着(100) < 出発(300)

        var index = BaseRunTimeIndexBuilder.Build(
            new List<Train> { train },
            new List<StationConnection> { sc },
            new List<StationConnectionSegment> { seg });

        Assert.Empty(index);
    }

    [Fact]
    public void 同一選定キーへの複数Trainは走査順で後勝ちになる()
    {
        // 一意性違反の検出・拒否はBaseTimeTableSetTrainDuplicationCrossValidator（保存時検証）の
        // 責務であり、Builder自体は例外を投げず上書きする防御的実装であることを確認する。
        var stA = new StationId(1);
        var stB = new StationId(2);
        var seg = MakeSegment(1, 1, 2);
        var sc = MakeConnection(1, seg.Id);

        var train1 = MakeTrain(1, null, (stA, stB, sc.Id));
        var keys1 = StopKeySequenceBuilder.BuildVisitedStopKeys(train1);
        SetStopTime(train1, keys1[0], -1, 0, true);
        SetStopTime(train1, keys1[1], 100, 110, true);

        var train2 = MakeTrain(2, null, (stA, stB, sc.Id));
        var keys2 = StopKeySequenceBuilder.BuildVisitedStopKeys(train2);
        SetStopTime(train2, keys2[0], -1, 0, true);
        SetStopTime(train2, keys2[1], 200, 210, true);

        var index = BaseRunTimeIndexBuilder.Build(
            new List<Train> { train1, train2 },
            new List<StationConnection> { sc },
            new List<StationConnectionSegment> { seg });

        // 走査順（train1→train2）に従い、後から処理されたtrain2の値が残る
        Assert.Equal(200, index[new BaseRunTimeIndexBuilder.SelectionKey(seg.Id, true, true, null)]);
    }

    [Fact]
    public void 複数ホップを持つTrainは各ホップを個別に索引化する()
    {
        var stA = new StationId(1);
        var stB = new StationId(2);
        var stC = new StationId(3);
        var seg1 = MakeSegment(1, 1, 2);
        var seg2 = MakeSegment(2, 2, 3);
        var sc1 = MakeConnection(1, seg1.Id);
        var sc2 = MakeConnection(2, seg2.Id);

        var train = MakeTrain(1, null, (stA, stB, sc1.Id), (stB, stC, sc2.Id));
        var keys = StopKeySequenceBuilder.BuildVisitedStopKeys(train);
        SetStopTime(train, keys[0], -1, 0, true);
        SetStopTime(train, keys[1], 100, 110, true);
        SetStopTime(train, keys[2], 250, 260, true);

        var index = BaseRunTimeIndexBuilder.Build(
            new List<Train> { train },
            new List<StationConnection> { sc1, sc2 },
            new List<StationConnectionSegment> { seg1, seg2 });

        Assert.Equal(100, index[new BaseRunTimeIndexBuilder.SelectionKey(seg1.Id, true, true, null)]);
        Assert.Equal(140, index[new BaseRunTimeIndexBuilder.SelectionKey(seg2.Id, true, true, null)]); // 250-110
    }

    [Fact]
    public void RunSegmentsが空のTrainは索引化対象外()
    {
        var train = MakeTrain(1, null); // hopsなし

        var index = BaseRunTimeIndexBuilder.Build(
            new List<Train> { train },
            new List<StationConnection>(),
            new List<StationConnectionSegment>());

        Assert.Empty(index);
    }
}
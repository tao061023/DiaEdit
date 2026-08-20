using DiaEditCore.Algorithm.CacheBuilder;

using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.TimeTable.Trains;

using Xunit;

namespace DiaEditCore.Tests.Algorithm.CacheBuilder;

public class BaseRunTimeIndexBuilderTests
{
    private static StationConnectionSegment MakeSegment(
        int id, int from, int to, int mainRouteId = 1)
        => new()
        {
            Id = new StationConnectionSegmentId(id),
            FromStationId = new StationId(from),
            ToStationId = new StationId(to),
            FromEntryPointId = new EntryPointId(from * 10),
            ToEntryPointId = new EntryPointId(to * 10),
            MainRouteId = new MainRouteId(mainRouteId),
        };

    private static StationConnection MakeConnection(
        int id, int mainRouteId, StationConnectionDirection direction, params int[] segmentIds)
        => new()
        {
            Id = new StationConnectionId(id),
            Name = "test-sc",
            MainRouteId = new MainRouteId(mainRouteId),
            Direction = direction,
            Segments = segmentIds.Select(i => new StationConnectionSegmentId(i)).ToList(),
        };

    /// <summary>
    /// stationIds（訪問順）・stationConnectionIds（ホップごと）・stopTimes（訪問順、
    /// (IsStop, ArrivalSeconds, DepartureSeconds)のタプル）からTrainを組み立てる。
    /// StopKeySequenceBuilderの規約通り、VisitCountは駅ごとのローカルカウンタとして自動採番される。
    /// </summary>
    private static Train MakeTrain(
        int id,
        int[] stationIds,
        int[] stationConnectionIds,
        (bool IsStop, int ArrivalSeconds, int DepartureSeconds)[] stopTimes,
        VehicleTypeId? defaultVehicleTypeId = null)
    {
        var runSegments = new List<TrainRunSegment>();
        for (var i = 0; i < stationConnectionIds.Length; i++)
        {
            runSegments.Add(new TrainRunSegment
            {
                FromStationId = new StationId(stationIds[i]),
                ToStationId = new StationId(stationIds[i + 1]),
                StationConnectionId = new StationConnectionId(stationConnectionIds[i]),
            });
        }

        var train = new Train
        {
            Id = new TrainId(id),
            TimeTableSetId = new TimeTableSetId(1),
            TrainNumber = $"T{id}",
            ServiceRouteId = new ServiceRouteId(1),
            TrainTypeId = new TrainTypeId(1),
            TrainTypeName = new DisplayName { Name = "test" },
            Nickname = new DisplayName { Name = "" },
            DefaultVehicleTypeId = defaultVehicleTypeId,
            RunSegments = runSegments,
        };

        var visitedKeys = StopKeySequenceBuilder.BuildVisitedStopKeys(train);
        Assert.Equal(stopTimes.Length, visitedKeys.Count); // テストの入力ミス検出用

        for (var i = 0; i < visitedKeys.Count; i++)
        {
            var (isStop, arrival, departure) = stopTimes[i];
            train.StopTimesInternal[visitedKeys[i]] = new StopTime
            {
                IsStop = isStop,
                ArrivalSeconds = arrival,
                DepartureSeconds = departure,
            };
        }

        return train;
    }

    [Fact]
    public void 単純な1ホップ_停車停車パターンで実測所要秒数が索引化される()
    {
        var segments = new[] { MakeSegment(1, from: 1, to: 2) };
        var connections = new[] { MakeConnection(1, mainRouteId: 1, StationConnectionDirection.Down, 1) };

        var train = MakeTrain(
            id: 1,
            stationIds: new[] { 1, 2 },
            stationConnectionIds: new[] { 1 },
            stopTimes: new[]
            {
                (IsStop: true, ArrivalSeconds: -1, DepartureSeconds: 100), // 始発駅：出発のみ
                (IsStop: true, ArrivalSeconds: 250, DepartureSeconds: 260), // 終着駅：到着基準
            });

        var index = BaseRunTimeIndexBuilder.Build(new[] { train }, connections, segments);

        var key = new BaseRunTimeIndexBuilder.SelectionKey(
            new StationConnectionSegmentId(1), FromIsStop: true, ToIsStop: true, VehicleTypeId: null);

        Assert.Equal(150, index[key]); // 250(到着) - 100(出発) = 150
    }

    [Fact]
    public void 通過駅は到着基準にDepartureSecondsを用いる()
    {
        var segments = new[]
        {
            MakeSegment(1, from: 1, to: 2),
            MakeSegment(2, from: 2, to: 3),
        };
        var connections = new[]
        {
            MakeConnection(1, mainRouteId: 1, StationConnectionDirection.Down, 1),
            MakeConnection(2, mainRouteId: 1, StationConnectionDirection.Down, 2),
        };

        var train = MakeTrain(
            id: 1,
            stationIds: new[] { 1, 2, 3 },
            stationConnectionIds: new[] { 1, 2 },
            stopTimes: new[]
            {
                (IsStop: true, ArrivalSeconds: -1, DepartureSeconds: 0),
                // 駅2は通過：ArrivalSecondsは無意味な値のはずだが、IsStop=falseならDepartureSecondsを
                // 到着基準・出発基準の両方に用いる（同一の通過時刻）
                (IsStop: false, ArrivalSeconds: -1, DepartureSeconds: 50),
                (IsStop: true, ArrivalSeconds: 120, DepartureSeconds: -1),
            });

        var index = BaseRunTimeIndexBuilder.Build(new[] { train }, connections, segments);

        var key1 = new BaseRunTimeIndexBuilder.SelectionKey(
            new StationConnectionSegmentId(1), FromIsStop: true, ToIsStop: false, VehicleTypeId: null);
        var key2 = new BaseRunTimeIndexBuilder.SelectionKey(
            new StationConnectionSegmentId(2), FromIsStop: false, ToIsStop: true, VehicleTypeId: null);

        Assert.Equal(50, index[key1]);  // 50(通過) - 0(出発) = 50
        Assert.Equal(70, index[key2]);  // 120(到着) - 50(通過) = 70
    }

    [Fact]
    public void 停車パターンが異なれば別キーとして共存する()
    {
        var segments = new[] { MakeSegment(1, from: 1, to: 2) };
        var connections = new[] { MakeConnection(1, mainRouteId: 1, StationConnectionDirection.Down, 1) };

        var trainStopStop = MakeTrain(
            id: 1, stationIds: new[] { 1, 2 }, stationConnectionIds: new[] { 1 },
            stopTimes: new[] { (true, -1, 0), (true, 100, -1) });

        var trainPassStop = MakeTrain(
            id: 2, stationIds: new[] { 1, 2 }, stationConnectionIds: new[] { 1 },
            stopTimes: new[] { (false, -1, 0), (true, 80, -1) });

        var index = BaseRunTimeIndexBuilder.Build(
            new[] { trainStopStop, trainPassStop }, connections, segments);

        Assert.Equal(2, index.Count);

        var keyStopStop = new BaseRunTimeIndexBuilder.SelectionKey(
            new StationConnectionSegmentId(1), true, true, null);
        var keyPassStop = new BaseRunTimeIndexBuilder.SelectionKey(
            new StationConnectionSegmentId(1), false, true, null);

        Assert.Equal(100, index[keyStopStop]);
        Assert.Equal(80, index[keyPassStop]);
    }

    [Fact]
    public void VehicleTypeIdが異なれば別キーとして共存する()
    {
        var segments = new[] { MakeSegment(1, from: 1, to: 2) };
        var connections = new[] { MakeConnection(1, mainRouteId: 1, StationConnectionDirection.Down, 1) };

        var vtA = new VehicleTypeId(1);
        var vtB = new VehicleTypeId(2);

        var trainA = MakeTrain(
            id: 1, stationIds: new[] { 1, 2 }, stationConnectionIds: new[] { 1 },
            stopTimes: new[] { (true, -1, 0), (true, 100, -1) }, defaultVehicleTypeId: vtA);

        var trainB = MakeTrain(
            id: 2, stationIds: new[] { 1, 2 }, stationConnectionIds: new[] { 1 },
            stopTimes: new[] { (true, -1, 0), (true, 130, -1) }, defaultVehicleTypeId: vtB);

        var index = BaseRunTimeIndexBuilder.Build(new[] { trainA, trainB }, connections, segments);

        Assert.Equal(2, index.Count);
        Assert.Equal(100, index[new(new StationConnectionSegmentId(1), true, true, vtA)]);
        Assert.Equal(130, index[new(new StationConnectionSegmentId(1), true, true, vtB)]);
    }

    [Fact]
    public void 出発秒数未設定のホップは索引化されない()
    {
        var segments = new[] { MakeSegment(1, from: 1, to: 2) };
        var connections = new[] { MakeConnection(1, mainRouteId: 1, StationConnectionDirection.Down, 1) };

        var train = MakeTrain(
            id: 1, stationIds: new[] { 1, 2 }, stationConnectionIds: new[] { 1 },
            stopTimes: new[] { (true, -1, -1), (true, 100, -1) }); // DepartureSeconds未設定

        var index = BaseRunTimeIndexBuilder.Build(new[] { train }, connections, segments);

        Assert.Empty(index);
    }

    [Fact]
    public void 到着基準秒数未設定のホップは索引化されない()
    {
        var segments = new[] { MakeSegment(1, from: 1, to: 2) };
        var connections = new[] { MakeConnection(1, mainRouteId: 1, StationConnectionDirection.Down, 1) };

        var train = MakeTrain(
            id: 1, stationIds: new[] { 1, 2 }, stationConnectionIds: new[] { 1 },
            stopTimes: new[] { (true, -1, 0), (true, -1, -1) }); // 終着駅ArrivalSeconds未設定（IsStop=true）

        var index = BaseRunTimeIndexBuilder.Build(new[] { train }, connections, segments);

        Assert.Empty(index);
    }

    [Fact]
    public void 時刻が逆転している不整合データは索引化されない()
    {
        var segments = new[] { MakeSegment(1, from: 1, to: 2) };
        var connections = new[] { MakeConnection(1, mainRouteId: 1, StationConnectionDirection.Down, 1) };

        var train = MakeTrain(
            id: 1, stationIds: new[] { 1, 2 }, stationConnectionIds: new[] { 1 },
            stopTimes: new[] { (true, -1, 200), (true, 100, -1) }); // 到着(100) < 出発(200)

        var index = BaseRunTimeIndexBuilder.Build(new[] { train }, connections, segments);

        Assert.Empty(index);
    }

    [Fact]
    public void StationConnectionのSegmentsにFromTo一致が0件のホップは索引化されない()
    {
        // hopはStation1->2だが、参照先StationConnectionのSegmentsはStation3->4のみ（不整合データ）
        var segments = new[] { MakeSegment(1, from: 3, to: 4) };
        var connections = new[] { MakeConnection(1, mainRouteId: 1, StationConnectionDirection.Down, 1) };

        var train = MakeTrain(
            id: 1, stationIds: new[] { 1, 2 }, stationConnectionIds: new[] { 1 },
            stopTimes: new[] { (true, -1, 0), (true, 100, -1) });

        var index = BaseRunTimeIndexBuilder.Build(new[] { train }, connections, segments);

        Assert.Empty(index);
    }

    [Fact]
    public void StationConnectionのSegmentsにFromTo一致が複数件のホップは索引化されない()
    {
        // 同一StationConnectionのSegmentsに、同じFrom/Toを持つSCSが2件紛れ込んだ不整合データ
        var segments = new[]
        {
            MakeSegment(1, from: 1, to: 2),
            MakeSegment(2, from: 1, to: 2),
        };
        var connections = new[] { MakeConnection(1, mainRouteId: 1, StationConnectionDirection.Down, 1, 2) };

        var train = MakeTrain(
            id: 1, stationIds: new[] { 1, 2 }, stationConnectionIds: new[] { 1 },
            stopTimes: new[] { (true, -1, 0), (true, 100, -1) });

        var index = BaseRunTimeIndexBuilder.Build(new[] { train }, connections, segments);

        Assert.Empty(index);
    }

    [Fact]
    public void 同一選定キーが複数Trainに該当する場合は後勝ちで上書きされる()
    {
        // 一意性の強制自体はBaseTimeTableSetTrainDuplicationCrossValidatorの責務であり、
        // 本Builderは防御的に「後勝ち」で上書きするだけであることを確認する回帰テスト。
        var segments = new[] { MakeSegment(1, from: 1, to: 2) };
        var connections = new[] { MakeConnection(1, mainRouteId: 1, StationConnectionDirection.Down, 1) };

        var trainFirst = MakeTrain(
            id: 1, stationIds: new[] { 1, 2 }, stationConnectionIds: new[] { 1 },
            stopTimes: new[] { (true, -1, 0), (true, 100, -1) });

        var trainSecond = MakeTrain(
            id: 2, stationIds: new[] { 1, 2 }, stationConnectionIds: new[] { 1 },
            stopTimes: new[] { (true, -1, 0), (true, 999, -1) });

        var index = BaseRunTimeIndexBuilder.Build(
            new[] { trainFirst, trainSecond }, connections, segments);

        var key = new BaseRunTimeIndexBuilder.SelectionKey(
            new StationConnectionSegmentId(1), true, true, null);

        Assert.Equal(999, index[key]); // 後から走査されたtrainSecondの値で上書きされる
    }

    [Fact]
    public void RunSegmentsが空のTrainは無視される()
    {
        var segments = new[] { MakeSegment(1, from: 1, to: 2) };
        var connections = new[] { MakeConnection(1, mainRouteId: 1, StationConnectionDirection.Down, 1) };

        var emptyTrain = new Train
        {
            Id = new TrainId(1),
            TimeTableSetId = new TimeTableSetId(1),
            TrainNumber = "T1",
            ServiceRouteId = new ServiceRouteId(1),
            TrainTypeId = new TrainTypeId(1),
            TrainTypeName = new DisplayName { Name = "test" },
            Nickname = new DisplayName { Name = "" },
            RunSegments = new List<TrainRunSegment>(),
        };

        var index = BaseRunTimeIndexBuilder.Build(new[] { emptyTrain }, connections, segments);

        Assert.Empty(index);
    }

    [Fact]
    public void 参照先StationConnectionが存在しないホップは索引化されない()
    {
        var segments = new[] { MakeSegment(1, from: 1, to: 2) };
        var connections = Array.Empty<StationConnection>(); // StationConnectionId=1が存在しない

        var train = MakeTrain(
            id: 1, stationIds: new[] { 1, 2 }, stationConnectionIds: new[] { 1 },
            stopTimes: new[] { (true, -1, 0), (true, 100, -1) });

        var index = BaseRunTimeIndexBuilder.Build(new[] { train }, connections, segments);

        Assert.Empty(index);
    }
}
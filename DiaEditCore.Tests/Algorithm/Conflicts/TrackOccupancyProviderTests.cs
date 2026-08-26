using DiaEditCore.Algorithm;
using DiaEditCore.Algorithm.Conflicts;
using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.TimeTable.Trains;

using Xunit;

namespace DiaEditCore.Tests.Algorithm.Conflicts;

public class TrackOccupancyProviderTests
{
    // -----------------------------
    // 共通フィクスチャ：A - B - C の3駅、B駅に1本のTrack(trackB)を持つ簡易路線
    // -----------------------------

    private static readonly StationId StA = new(1);
    private static readonly StationId StB = new(2);
    private static readonly StationId StC = new(3);

    private static readonly EntryPointId EpB = new(20); // B駅側で唯一使うEntryPoint（A方向・C方向で共用）
    private static readonly BoundaryPointId BpB = new(21);
    private static readonly RailId TrackB = new(30);

    private static readonly StationConnectionId ScAB = new(1);
    private static readonly StationConnectionId ScBC = new(2);
    private static readonly StationConnectionSegmentId ScsAB = new(1);
    private static readonly StationConnectionSegmentId ScsBC = new(2);

    private static ProjectSettings MakeSettings(int diagramBasedTimeSec = 14400, int? minTurnaroundSec = 0) => new(
        new ValidationRules(
            MinDwellTimeSec: 30,
            MinHeadwaySec: 120,
            MinTurnaroundSec: minTurnaroundSec,
            TrackEntryMarginSec: 60,
            TrackPassMarginSec: 10,
            EnableConflictDetection: true,
            EnableCarLengthCheck: true),
        diagramBasedTimeSec);

    private static Rail MakeTrackRail() => new()
    {
        Id = TrackB,
        LengthM = 200,
        SpeedLimitKph = 25,
        Role = RailRole.Track,
        EndpointA = new EntryPointEndpointRef(EpB),
        EndpointB = new BoundaryPointEndpointRef(BpB),
    };

    private static StationPath MakeArrivalPath(int adjustmentSec) => new()
    {
        Id = new StationPathId(101),
        FloorUnitId = new FloorUnitId(1),
        Name = "B到着",
        Direction = StationPathDirection.Arrival,
        Waypoints = [new EntryPointWaypoint(EpB), new BoundaryPointWaypoint(BpB)],
        AdjustmentSec = adjustmentSec,
    };

    private static StationPath MakeDeparturePath(int adjustmentSec) => new()
    {
        Id = new StationPathId(102),
        FloorUnitId = new FloorUnitId(1),
        Name = "B出発",
        Direction = StationPathDirection.Departure,
        Waypoints = [new BoundaryPointWaypoint(BpB), new EntryPointWaypoint(EpB)],
        AdjustmentSec = adjustmentSec,
    };

    private static StationPath MakeShuntingPath() => new()
    {
        Id = new StationPathId(103),
        FloorUnitId = new FloorUnitId(1),
        Name = "B入換",
        Direction = StationPathDirection.Shunting,
        Waypoints = [new EntryPointWaypoint(EpB), new BoundaryPointWaypoint(BpB)],
    };

    private static Train NewTrain(int id, string trainNumber) => new()
    {
        Id = new TrainId(id),
        TimeTableSetId = new TimeTableSetId(1),
        TrainNumber = trainNumber,
        ServiceRouteId = new ServiceRouteId(1),
        TrainTypeId = new TrainTypeId(1),
        TrainTypeName = new DisplayName { Name = "普通" },
        Nickname = new DisplayName { Name = "" },
        DefaultVehicleTypeId = new VehicleTypeId(1),
    };

    /// <summary>A駅からB駅へ到着し、そこで運転を終える(終着)Trainを1本作る。</summary>
    private static Train MakeArrivingTrain(int id, int arrivalSeconds, string trainNumber = "arr")
    {
        var train = NewTrain(id, trainNumber);
        train.RunSegments.Add(new TrainRunSegment { FromStationId = StA, ToStationId = StB, StationConnectionId = ScAB });
        train.StopTimesInternal[new StopKey(StB, 0)] = new StopTime
        {
            IsStop = true,
            ArrivalSeconds = arrivalSeconds,
            DepartureSeconds = -1,
            TrackRailId = TrackB,
        };
        return train;
    }

    /// <summary>B駅からC駅へ、そこで運転を開始する(始発)Trainを1本作る。</summary>
    private static Train MakeDepartingTrain(int id, int departureSeconds, string trainNumber = "dep")
    {
        var train = NewTrain(id, trainNumber);
        train.RunSegments.Add(new TrainRunSegment { FromStationId = StB, ToStationId = StC, StationConnectionId = ScBC });
        train.StopTimesInternal[new StopKey(StB, 0)] = new StopTime
        {
            IsStop = true,
            ArrivalSeconds = -1,
            DepartureSeconds = departureSeconds,
            TrackRailId = TrackB,
        };
        return train;
    }

    private static (
        IReadOnlyList<StationConnection> Scs,
        IReadOnlyList<StationConnectionSegment> Segs,
        IReadOnlyList<MainRoute> AllMainRoutes,
        IReadOnlyDictionary<StationPathId, StationPath> PathsById,
        IReadOnlyDictionary<(EntryPointId, RailId), StationPathId> ArrivalIndex,
        IReadOnlyDictionary<(RailId, EntryPointId), StationPathId> DepartureIndex,
        IReadOnlyList<Rail> Rails,
        StationPath ShuntingPath
    ) BuildTopology(int arrivalAdjustmentSec = 30, int departureAdjustmentSec = 20)
    {
        var segs = new List<StationConnectionSegment>
        {
            new() { Id = ScsAB, StationIdA = StA, StationIdB = StB, EntryPointIdA = new EntryPointId(10), EntryPointIdB = EpB, MainRouteId = new MainRouteId(1) },
            new() { Id = ScsBC, StationIdA = StB, StationIdB = StC, EntryPointIdA = EpB, EntryPointIdB = new EntryPointId(40), MainRouteId = new MainRouteId(1) },
        };
        var scs = new List<StationConnection>
        {
            new() { Id = ScAB, Name = "AB", MainRouteId = new MainRouteId(1), Direction = StationConnectionDirection.Down, Segments = [ScsAB] },
            new() { Id = ScBC, Name = "BC", MainRouteId = new MainRouteId(1), Direction = StationConnectionDirection.Down, Segments = [ScsBC] },
        };

        var arrivalPath = MakeArrivalPath(arrivalAdjustmentSec);
        var departurePath = MakeDeparturePath(departureAdjustmentSec);
        var shuntingPath = MakeShuntingPath();
        var pathsById = new Dictionary<StationPathId, StationPath>
        {
            [arrivalPath.Id] = arrivalPath,
            [departurePath.Id] = departurePath,
            [shuntingPath.Id] = shuntingPath,
        };
        var rails = new List<Rail> { MakeTrackRail() };

        var (arrivalIndex, departureIndex) = StationPathTrackIndexBuilder.Build([arrivalPath, departurePath], rails);
        var allMainRoutes = new List<MainRoute> { new() { Id = new MainRouteId(1), Name = new DisplayName { Name = "Route1" }, StationOrder = new List<StationId> { StA, StB, StC } } };

        return (scs, segs, allMainRoutes, pathsById, arrivalIndex, departureIndex, rails, shuntingPath);
    }

    private static List<ConflictChecker.Occupancy> GetTrackBOccupancy(Dictionary<RailId, List<ConflictChecker.Occupancy>> result)
        => result.TryGetValue(TrackB, out var list) ? list : new List<ConflictChecker.Occupancy>();

    // -----------------------------
    // テスト
    // -----------------------------

    [Fact]
    public void PrevTrainNextTrainがともに解決できるとき双方の占有区間が接続点をまたいで意図的に重複する()
    {
        // 6.5節での確定方針(案(a))：占有構築側は両側とも延長する。
        // A・B間の重複除外は後段フィルタ(別タスク)の責務であり、本Providerはあえて重複させたまま返す。
        var (scs, segs, allMainRoutes, pathsById, arrivalIndex, departureIndex, rails, _) = BuildTopology();
        var arriving = MakeArrivingTrain(1, arrivalSeconds: 1000);   // arrStart=970, arrEnd=1000
        var departing = MakeDepartingTrain(2, departureSeconds: 1100); // depStart=1100, depEnd=1120
        var trains = new[] { arriving, departing };
        var settings = MakeSettings();

        var result = TrackOccupancyProvider.BuildOccupancy(trains, scs, segs, allMainRoutes, pathsById, arrivalIndex, departureIndex, rails, settings);

        var occ = GetTrackBOccupancy(result);
        Assert.Contains(occ, o => o.TrainId == arriving.Id && o.StartSeconds == 970 && o.EndSeconds == 1100);
        Assert.Contains(occ, o => o.TrainId == departing.Id && o.StartSeconds == 1000 && o.EndSeconds == 1120);
    }

    [Fact]
    public void PrevTrainが存在しない始発列車はDiagramBasedTimeSecで占有開始が打ち切られる()
    {
        var (scs, segs, allMainRoutes, pathsById, arrivalIndex, departureIndex, rails, _) = BuildTopology();
        var departing = MakeDepartingTrain(1, departureSeconds: 20000);
        var settings = MakeSettings(diagramBasedTimeSec: 14400);

        var result = TrackOccupancyProvider.BuildOccupancy([departing], scs, segs, allMainRoutes, pathsById, arrivalIndex, departureIndex, rails, settings);

        var occ = GetTrackBOccupancy(result);
        var entry = Assert.Single(occ);
        Assert.Equal(departing.Id, entry.TrainId);
        Assert.Equal(14400, entry.StartSeconds);
        Assert.Equal(20020, entry.EndSeconds); // departureBasis(20000) + AdjustmentSec(20)
    }

    [Fact]
    public void NextTrainが存在しない終着列車は自身の到着占有終了で打ち切られる()
    {
        var (scs, segs, allMainRoutes, pathsById, arrivalIndex, departureIndex, rails, _) = BuildTopology();
        var arriving = MakeArrivingTrain(1, arrivalSeconds: 1000);
        var settings = MakeSettings();

        var result = TrackOccupancyProvider.BuildOccupancy([arriving], scs, segs, allMainRoutes, pathsById, arrivalIndex, departureIndex, rails, settings);

        var occ = GetTrackBOccupancy(result);
        var entry = Assert.Single(occ);
        Assert.Equal(arriving.Id, entry.TrainId);
        Assert.Equal(970, entry.StartSeconds);  // arrivalBasis(1000) - AdjustmentSec(30)
        Assert.Equal(1000, entry.EndSeconds);   // NextTrainなし＝自身のArrivalEndで打ち切り
    }

    [Fact]
    public void MinTurnaroundSec未満で接続候補にならない場合はPrevTrainNextTrainとも見なされずそれぞれ独立に扱われる()
    {
        var (scs, segs, allMainRoutes, pathsById, arrivalIndex, departureIndex, rails, _) = BuildTopology();
        var arriving = MakeArrivingTrain(1, arrivalSeconds: 1000);
        var departing = MakeDepartingTrain(2, departureSeconds: 1010); // 余裕10秒
        var trains = new[] { arriving, departing };
        var settings = MakeSettings(minTurnaroundSec: 300); // 300秒未満は接続候補にならない

        var result = TrackOccupancyProvider.BuildOccupancy(trains, scs, segs, allMainRoutes, pathsById, arrivalIndex, departureIndex, rails, settings);

        var occ = GetTrackBOccupancy(result);
        Assert.Contains(occ, o => o.TrainId == arriving.Id && o.StartSeconds == 970 && o.EndSeconds == 1000); // NextTrainなし扱い
        Assert.Contains(occ, o => o.TrainId == departing.Id && o.StartSeconds == 14400 && o.EndSeconds == 1030); // PrevTrainなし扱い(DiagramBasedTimeSec)
    }

    [Fact]
    public void Shunting作業の占有区間はTrack占有に追加される()
    {
        var (scs, segs, allMainRoutes, pathsById, arrivalIndex, departureIndex, rails, shuntingPath) = BuildTopology();
        var train = MakeArrivingTrain(1, arrivalSeconds: 1000);
        train.StopTimesInternal[new StopKey(StB, 0)].Works.Add(new StationWork
        {
            Type = StationWorkType.Shunting,
            StationPathId = shuntingPath.Id,
            StartOpSeconds = 5000,
            EndOpSeconds = 5200,
        });
        var settings = MakeSettings();

        var result = TrackOccupancyProvider.BuildOccupancy([train], scs, segs, allMainRoutes, pathsById, arrivalIndex, departureIndex, rails, settings);

        var occ = GetTrackBOccupancy(result);
        Assert.Contains(occ, o => o.TrainId == train.Id && o.StartSeconds == 5000 && o.EndSeconds == 5200);
        // 通常のTrack占有(970-1000)も別途含まれていること
        Assert.Contains(occ, o => o.TrainId == train.Id && o.StartSeconds == 970 && o.EndSeconds == 1000);
    }

    [Fact]
    public void StartOpSecondsまたはEndOpSecondsが未設定のShunting作業は占有に加算されない()
    {
        var (scs, segs, allMainRoutes, pathsById, arrivalIndex, departureIndex, rails, shuntingPath) = BuildTopology();
        var train = MakeArrivingTrain(1, arrivalSeconds: 1000);
        train.StopTimesInternal[new StopKey(StB, 0)].Works.Add(new StationWork
        {
            Type = StationWorkType.Shunting,
            StationPathId = shuntingPath.Id,
            StartOpSeconds = -1, // 未設定
            EndOpSeconds = 5200,
        });
        var settings = MakeSettings();

        var result = TrackOccupancyProvider.BuildOccupancy([train], scs, segs, allMainRoutes, pathsById, arrivalIndex, departureIndex, rails, settings);

        var occ = GetTrackBOccupancy(result);
        Assert.DoesNotContain(occ, o => o.StartSeconds == -1);
        Assert.Single(occ); // 通常のTrack占有(970-1000)のみ
    }

    [Fact]
    public void RunSegmentsが空のTrainはスキップされ例外を投げない()
    {
        var (scs, segs, allMainRoutes, pathsById, arrivalIndex, departureIndex, rails, _) = BuildTopology();
        var emptyTrain = NewTrain(1, "empty");
        var settings = MakeSettings();

        var exception = Record.Exception(() =>
            TrackOccupancyProvider.BuildOccupancy([emptyTrain], scs, segs, allMainRoutes, pathsById, arrivalIndex, departureIndex, rails, settings));

        Assert.Null(exception);
    }
}

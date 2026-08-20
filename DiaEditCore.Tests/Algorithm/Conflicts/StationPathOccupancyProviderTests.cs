using DiaEditCore.Algorithm;
using DiaEditCore.Algorithm.Conflicts;
using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.TimeTable.Trains;

using Xunit;

namespace DiaEditCore.Tests.Algorithm.Conflicts;

public class StationPathOccupancyProviderTests
{
    // -----------------------------
    // 共通フィクスチャ：A - B - C の3駅、B駅に1本のTrack(trackB)を持つ簡易路線
    // （TrackOccupancyProviderTestsと同一構成。到着用StationPath・出発用StationPathは別オブジェクト）
    // -----------------------------

    private static readonly StationId StA = new(1);
    private static readonly StationId StB = new(2);
    private static readonly StationId StC = new(3);

    private static readonly EntryPointId EpB = new(20);
    private static readonly BoundaryPointId BpB = new(21);
    private static readonly RailId TrackB = new(30);

    private static readonly StationConnectionId ScAB = new(1);
    private static readonly StationConnectionId ScBC = new(2);
    private static readonly StationConnectionSegmentId ScsAB = new(1);
    private static readonly StationConnectionSegmentId ScsBC = new(2);

    private static readonly StationPathId ArrivalPathId = new(101);
    private static readonly StationPathId DeparturePathId = new(102);

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
        Id = ArrivalPathId,
        FloorUnitId = new FloorUnitId(1),
        Name = "B到着",
        Direction = StationPathDirection.Arrival,
        Waypoints = [new EntryPointWaypoint(EpB), new BoundaryPointWaypoint(BpB)],
        AdjustmentSec = adjustmentSec,
    };

    private static StationPath MakeDeparturePath(int adjustmentSec) => new()
    {
        Id = DeparturePathId,
        FloorUnitId = new FloorUnitId(1),
        Name = "B出発",
        Direction = StationPathDirection.Departure,
        Waypoints = [new BoundaryPointWaypoint(BpB), new EntryPointWaypoint(EpB)],
        AdjustmentSec = adjustmentSec,
    };

    private static Train NewTrain(int id, string trainNumber, bool isProvisional = false) => new()
    {
        Id = new TrainId(id),
        TimeTableSetId = new TimeTableSetId(1),
        TrainNumber = trainNumber,
        ServiceRouteId = new ServiceRouteId(1),
        TrainTypeId = new TrainTypeId(1),
        TrainTypeName = new DisplayName { Name = "普通" },
        Nickname = new DisplayName { Name = "" },
        DefaultVehicleTypeId = new VehicleTypeId(1),
        IsProvisional = isProvisional,
    };

    /// <summary>A駅からB駅へ到着し、そこで運転を終える(終着)Trainを1本作る。</summary>
    private static Train MakeArrivingTrain(int id, int arrivalSeconds, RailId? trackRailId, bool isProvisional = false, string trainNumber = "arr")
    {
        var train = NewTrain(id, trainNumber, isProvisional);
        train.RunSegments.Add(new TrainRunSegment { FromStationId = StA, ToStationId = StB, StationConnectionId = ScAB });
        train.StopTimesInternal[new StopKey(StB, 0)] = new StopTime
        {
            IsStop = true,
            ArrivalSeconds = arrivalSeconds,
            DepartureSeconds = -1,
            TrackRailId = trackRailId,
        };
        return train;
    }

    /// <summary>B駅からC駅へ、そこで運転を開始する(始発)Trainを1本作る。</summary>
    private static Train MakeDepartingTrain(int id, int departureSeconds, RailId? trackRailId, string trainNumber = "dep")
    {
        var train = NewTrain(id, trainNumber);
        train.RunSegments.Add(new TrainRunSegment { FromStationId = StB, ToStationId = StC, StationConnectionId = ScBC });
        train.StopTimesInternal[new StopKey(StB, 0)] = new StopTime
        {
            IsStop = true,
            ArrivalSeconds = -1,
            DepartureSeconds = departureSeconds,
            TrackRailId = trackRailId,
        };
        return train;
    }

    /// <summary>A駅発C駅行きでB駅に停車する、中間駅通過型のTrainを1本作る(B駅で到着・出発の両方を持つ)。</summary>
    private static Train MakeThroughTrainStoppingAtB(int id, int arrivalAtB, int departureAtB, string trainNumber = "through")
    {
        var train = NewTrain(id, trainNumber);
        train.RunSegments.Add(new TrainRunSegment { FromStationId = StA, ToStationId = StB, StationConnectionId = ScAB });
        train.RunSegments.Add(new TrainRunSegment { FromStationId = StB, ToStationId = StC, StationConnectionId = ScBC });
        train.StopTimesInternal[new StopKey(StB, 0)] = new StopTime
        {
            IsStop = true,
            ArrivalSeconds = arrivalAtB,
            DepartureSeconds = departureAtB,
            TrackRailId = TrackB,
        };
        return train;
    }

    private static (
        IReadOnlyList<StationConnection> Scs,
        IReadOnlyList<StationConnectionSegment> Segs,
        IReadOnlyDictionary<StationPathId, StationPath> PathsById,
        IReadOnlyDictionary<(EntryPointId, RailId), StationPathId> ArrivalIndex,
        IReadOnlyDictionary<(RailId, EntryPointId), StationPathId> DepartureIndex
    ) BuildTopology(int arrivalAdjustmentSec = 30, int departureAdjustmentSec = 20)
    {
        var segs = new List<StationConnectionSegment>
        {
            new() { Id = ScsAB, FromStationId = StA, ToStationId = StB, FromEntryPointId = new EntryPointId(10), ToEntryPointId = EpB, MainRouteId = new MainRouteId(1), },
            new() { Id = ScsBC, FromStationId = StB, ToStationId = StC, FromEntryPointId = EpB, ToEntryPointId = new EntryPointId(40), MainRouteId = new MainRouteId(1), },
        };
        var scs = new List<StationConnection>
        {
            new() { Id = ScAB, Name = "AB", MainRouteId = new MainRouteId(1), Direction = StationConnectionDirection.Down, Segments = [ScsAB] },
            new() { Id = ScBC, Name = "BC", MainRouteId = new MainRouteId(1), Direction = StationConnectionDirection.Down, Segments = [ScsBC] },
        };

        var arrivalPath = MakeArrivalPath(arrivalAdjustmentSec);
        var departurePath = MakeDeparturePath(departureAdjustmentSec);
        var pathsById = new Dictionary<StationPathId, StationPath>
        {
            [arrivalPath.Id] = arrivalPath,
            [departurePath.Id] = departurePath,
        };
        var rails = new List<Rail> { MakeTrackRail() };

        var (arrivalIndex, departureIndex) = StationPathTrackIndexBuilder.Build([arrivalPath, departurePath], rails);

        return (scs, segs, pathsById, arrivalIndex, departureIndex);
    }

    private static List<ConflictChecker.Occupancy> Get(
        Dictionary<StationPathId, List<ConflictChecker.Occupancy>> result, StationPathId id)
        => result.TryGetValue(id, out var list) ? list : new List<ConflictChecker.Occupancy>();

    // -----------------------------
    // テスト
    // -----------------------------

    [Fact]
    public void 終着列車の到着訪問は到着用StationPathの占有として登録される()
    {
        var (scs, segs, pathsById, arrivalIndex, departureIndex) = BuildTopology(arrivalAdjustmentSec: 30);
        var train = MakeArrivingTrain(1, arrivalSeconds: 1000, TrackB);

        var result = StationPathOccupancyProvider.BuildOccupancy([train], scs, segs, pathsById, arrivalIndex, departureIndex);

        var occ = Get(result, ArrivalPathId);
        var entry = Assert.Single(occ);
        Assert.Equal(train.Id, entry.TrainId);
        Assert.Equal(970, entry.StartSeconds); // arrivalBasis(1000) - AdjustmentSec(30)
        Assert.Equal(1000, entry.EndSeconds);
        Assert.False(result.ContainsKey(DeparturePathId));
    }

    [Fact]
    public void 始発列車の出発訪問は出発用StationPathの占有として登録される()
    {
        var (scs, segs, pathsById, arrivalIndex, departureIndex) = BuildTopology(departureAdjustmentSec: 20);
        var train = MakeDepartingTrain(1, departureSeconds: 1100, TrackB);

        var result = StationPathOccupancyProvider.BuildOccupancy([train], scs, segs, pathsById, arrivalIndex, departureIndex);

        var occ = Get(result, DeparturePathId);
        var entry = Assert.Single(occ);
        Assert.Equal(train.Id, entry.TrainId);
        Assert.Equal(1100, entry.StartSeconds);
        Assert.Equal(1120, entry.EndSeconds); // departureBasis(1100) + AdjustmentSec(20)
        Assert.False(result.ContainsKey(ArrivalPathId));
    }

    [Fact]
    public void 中間駅で停車するTrainは到着用出発用の両方のStationPathに占有が登録される()
    {
        var (scs, segs, pathsById, arrivalIndex, departureIndex) = BuildTopology(arrivalAdjustmentSec: 30, departureAdjustmentSec: 20);
        var train = MakeThroughTrainStoppingAtB(1, arrivalAtB: 1000, departureAtB: 1100);

        var result = StationPathOccupancyProvider.BuildOccupancy([train], scs, segs, pathsById, arrivalIndex, departureIndex);

        var arrOcc = Assert.Single(Get(result, ArrivalPathId));
        Assert.Equal(970, arrOcc.StartSeconds);
        Assert.Equal(1000, arrOcc.EndSeconds);

        var depOcc = Assert.Single(Get(result, DeparturePathId));
        Assert.Equal(1100, depOcc.StartSeconds);
        Assert.Equal(1120, depOcc.EndSeconds);
    }

    [Fact]
    public void IsProvisionalな仮列車も占有対象に含まれる()
    {
        var (scs, segs, pathsById, arrivalIndex, departureIndex) = BuildTopology();
        var provisionalTrain = MakeArrivingTrain(1, arrivalSeconds: 1000, TrackB, isProvisional: true);

        var result = StationPathOccupancyProvider.BuildOccupancy([provisionalTrain], scs, segs, pathsById, arrivalIndex, departureIndex);

        Assert.Single(Get(result, ArrivalPathId));
    }

    [Fact]
    public void TrackRailIdが未設定の訪問は占有に追加されない()
    {
        var (scs, segs, pathsById, arrivalIndex, departureIndex) = BuildTopology();
        var train = MakeArrivingTrain(1, arrivalSeconds: 1000, trackRailId: null);

        var result = StationPathOccupancyProvider.BuildOccupancy([train], scs, segs, pathsById, arrivalIndex, departureIndex);

        Assert.Empty(result);
    }

    [Fact]
    public void 複数Trainが同一到着用StationPathを重複する時間帯で使用する場合ConflictCheckerで検出できる()
    {
        var (scs, segs, pathsById, arrivalIndex, departureIndex) = BuildTopology(arrivalAdjustmentSec: 30);
        var train1 = MakeArrivingTrain(1, arrivalSeconds: 1000, TrackB, trainNumber: "1001M");
        var train2 = MakeArrivingTrain(2, arrivalSeconds: 1010, TrackB, trainNumber: "1002M"); // 970-1000 と 980-1010 で重複
        var trains = new[] { train1, train2 };

        var result = StationPathOccupancyProvider.BuildOccupancy(trains, scs, segs, pathsById, arrivalIndex, departureIndex);
        var occ = Get(result, ArrivalPathId);

        var checker = new ConflictChecker(new BoundaryPointObjectId(BpB), occ); // 対象オブジェクトIDは任意のダミーでよい(占有区間のみが検証対象)
        var overlaps = checker.CheckOverlap();

        var pair = Assert.Single(overlaps);
        var ids = new[] { pair.A.Value, pair.B.Value };
        Assert.Contains(1, ids);
        Assert.Contains(2, ids);
    }

    [Fact]
    public void RunSegmentsが空のTrainはスキップされ例外を投げない()
    {
        var (scs, segs, pathsById, arrivalIndex, departureIndex) = BuildTopology();
        var emptyTrain = NewTrain(1, "empty");

        var exception = Record.Exception(() =>
            StationPathOccupancyProvider.BuildOccupancy([emptyTrain], scs, segs, pathsById, arrivalIndex, departureIndex));

        Assert.Null(exception);
    }
}

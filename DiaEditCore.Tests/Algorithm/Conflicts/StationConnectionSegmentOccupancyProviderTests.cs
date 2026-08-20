using DiaEditCore.Algorithm;
using DiaEditCore.Algorithm.Conflicts;
using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.TimeTable.Trains;

using Xunit;

namespace DiaEditCore.Tests.Algorithm.Conflicts;

public class StationConnectionSegmentOccupancyProviderTests
{
    // -----------------------------
    // 共通フィクスチャ：A - B の2駅、駅間に1本のSCS(scsAB)を持つ簡易路線
    // -----------------------------

    private static readonly StationId StA = new(1);
    private static readonly StationId StB = new(2);

    private static readonly EntryPointId EpA = new(10); // A駅側のEntryPoint(出発用)
    private static readonly EntryPointId EpB = new(20); // B駅側のEntryPoint(到着用)
    private static readonly BoundaryPointId BpA = new(11);
    private static readonly BoundaryPointId BpB = new(21);
    private static readonly RailId TrackA = new(30); // A駅の出発用Track
    private static readonly RailId TrackB = new(31); // B駅の到着用Track

    private static readonly StationConnectionId ScAB = new(1);
    private static readonly StationConnectionSegmentId ScsAB = new(1);

    private static Rail MakeTrackRail(RailId id, EntryPointId ep, BoundaryPointId bp) => new()
    {
        Id = id,
        LengthM = 200,
        SpeedLimitKph = 25,
        Role = RailRole.Track,
        EndpointA = new EntryPointEndpointRef(ep),
        EndpointB = new BoundaryPointEndpointRef(bp),
    };

    private static StationPath MakeDeparturePath(int id, EntryPointId ep, BoundaryPointId bp, int adjustmentSec) => new()
    {
        Id = new StationPathId(id),
        FloorUnitId = new FloorUnitId(1),
        Name = "出発",
        Direction = StationPathDirection.Departure,
        Waypoints = [new BoundaryPointWaypoint(bp), new EntryPointWaypoint(ep)],
        AdjustmentSec = adjustmentSec,
    };

    private static StationPath MakeArrivalPath(int id, EntryPointId ep, BoundaryPointId bp, int adjustmentSec) => new()
    {
        Id = new StationPathId(id),
        FloorUnitId = new FloorUnitId(1),
        Name = "到着",
        Direction = StationPathDirection.Arrival,
        Waypoints = [new EntryPointWaypoint(ep), new BoundaryPointWaypoint(bp)],
        AdjustmentSec = adjustmentSec,
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

    /// <summary>A駅をdepartureSecondsに出発し、B駅にarrivalSecondsに到着する1区間のTrainを作る。</summary>
    private static Train MakeThroughTrain(int id, int departureSeconds, int arrivalSeconds, string trainNumber = "1000M")
    {
        var train = NewTrain(id, trainNumber);
        train.RunSegments.Add(new TrainRunSegment { FromStationId = StA, ToStationId = StB, StationConnectionId = ScAB });
        train.StopTimesInternal[new StopKey(StA, 0)] = new StopTime
        {
            IsStop = true,
            ArrivalSeconds = -1,
            DepartureSeconds = departureSeconds,
            TrackRailId = TrackA,
        };
        train.StopTimesInternal[new StopKey(StB, 0)] = new StopTime
        {
            IsStop = true,
            ArrivalSeconds = arrivalSeconds,
            DepartureSeconds = -1,
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
    ) BuildTopology(int departureAdjustmentSec = 20, int arrivalAdjustmentSec = 30)
    {
        var segs = new List<StationConnectionSegment>
        {
            new() { Id = ScsAB, FromStationId = StA, ToStationId = StB, FromEntryPointId = EpA, ToEntryPointId = EpB, MainRouteId = new MainRouteId(1), },
        };
        var scs = new List<StationConnection>
        {
            new() { Id = ScAB, Name = "AB", MainRouteId = new MainRouteId(1), Direction = StationConnectionDirection.Down, Segments = [ScsAB] },
        };

        var departurePath = MakeDeparturePath(201, EpA, BpA, departureAdjustmentSec);
        var arrivalPath = MakeArrivalPath(202, EpB, BpB, arrivalAdjustmentSec);
        var pathsById = new Dictionary<StationPathId, StationPath>
        {
            [departurePath.Id] = departurePath,
            [arrivalPath.Id] = arrivalPath,
        };
        var rails = new List<Rail> { MakeTrackRail(TrackA, EpA, BpA), MakeTrackRail(TrackB, EpB, BpB) };

        var (arrivalIndex, departureIndex) = StationPathTrackIndexBuilder.Build([departurePath, arrivalPath], rails);

        return (scs, segs, pathsById, arrivalIndex, departureIndex);
    }

    private static List<ConflictChecker.Occupancy> GetScsAbOccupancy(
        Dictionary<StationConnectionSegmentId, List<ConflictChecker.Occupancy>> result)
        => result.TryGetValue(ScsAB, out var list) ? list : new List<ConflictChecker.Occupancy>();

    // -----------------------------
    // テスト
    // -----------------------------

    [Fact]
    public void 占有区間は出発StationPathの占有終了から到着StationPathの占有開始までとなる()
    {
        var (scs, segs, pathsById, arrivalIndex, departureIndex) = BuildTopology(departureAdjustmentSec: 20, arrivalAdjustmentSec: 30);
        var train = MakeThroughTrain(1, departureSeconds: 1000, arrivalSeconds: 1500);

        var result = StationConnectionSegmentOccupancyProvider.BuildOccupancy([train], scs, segs, pathsById, arrivalIndex, departureIndex);

        var occ = GetScsAbOccupancy(result);
        var entry = Assert.Single(occ);
        Assert.Equal(train.Id, entry.TrainId);
        Assert.Equal(1020, entry.StartSeconds); // departureBasis(1000) + AdjustmentSec(20)
        Assert.Equal(1470, entry.EndSeconds);   // arrivalBasis(1500) - AdjustmentSec(30)
    }

    [Fact]
    public void 複数Trainが同じSCSを重複する時間帯で使用する場合ConflictCheckerで検出できる()
    {
        var (scs, segs, pathsById, arrivalIndex, departureIndex) = BuildTopology();
        var train1 = MakeThroughTrain(1, departureSeconds: 1000, arrivalSeconds: 1500, "1001M");
        var train2 = MakeThroughTrain(2, departureSeconds: 1100, arrivalSeconds: 1600, "1002M"); // train1と重複区間あり
        var trains = new[] { train1, train2 };

        var result = StationConnectionSegmentOccupancyProvider.BuildOccupancy(trains, scs, segs, pathsById, arrivalIndex, departureIndex);
        var occ = GetScsAbOccupancy(result);

        var checker = new ConflictChecker(new StationConnectionSegmentObjectId(ScsAB), occ);
        var overlaps = checker.CheckOverlap();

        var pair = Assert.Single(overlaps);
        var ids = new[] { pair.A.Value, pair.B.Value };
        Assert.Contains(1, ids);
        Assert.Contains(2, ids);
    }

    [Fact]
    public void 分離した時間帯のTrain同士はConflictCheckerで重複検出されない()
    {
        var (scs, segs, pathsById, arrivalIndex, departureIndex) = BuildTopology();
        var train1 = MakeThroughTrain(1, departureSeconds: 1000, arrivalSeconds: 1500, "1001M");
        var train2 = MakeThroughTrain(2, departureSeconds: 2000, arrivalSeconds: 2500, "1002M"); // 重複なし
        var trains = new[] { train1, train2 };

        var result = StationConnectionSegmentOccupancyProvider.BuildOccupancy(trains, scs, segs, pathsById, arrivalIndex, departureIndex);
        var occ = GetScsAbOccupancy(result);

        var checker = new ConflictChecker(new StationConnectionSegmentObjectId(ScsAB), occ);
        var overlaps = checker.CheckOverlap();

        Assert.Empty(overlaps);
    }

    [Fact]
    public void 出発または到着のStopTimeが欠けている場合は占有として追加されない()
    {
        var (scs, segs, pathsById, arrivalIndex, departureIndex) = BuildTopology();
        var train = NewTrain(1, "1000M");
        train.RunSegments.Add(new TrainRunSegment { FromStationId = StA, ToStationId = StB, StationConnectionId = ScAB });
        // 出発StopTimeのみ設定、到着StopTimeは未設定のまま
        train.StopTimesInternal[new StopKey(StA, 0)] = new StopTime
        {
            IsStop = true,
            DepartureSeconds = 1000,
            TrackRailId = TrackA,
        };

        var result = StationConnectionSegmentOccupancyProvider.BuildOccupancy([train], scs, segs, pathsById, arrivalIndex, departureIndex);

        Assert.Empty(GetScsAbOccupancy(result));
    }

    [Fact]
    public void RunSegmentsが空のTrainはスキップされ例外を投げない()
    {
        var (scs, segs, pathsById, arrivalIndex, departureIndex) = BuildTopology();
        var emptyTrain = NewTrain(1, "empty");

        var exception = Record.Exception(() =>
            StationConnectionSegmentOccupancyProvider.BuildOccupancy([emptyTrain], scs, segs, pathsById, arrivalIndex, departureIndex));

        Assert.Null(exception);
    }

    [Fact]
    public void SegmentsがStationConnection中に複数登録されていても先頭のSegmentIdが採用される()
    {
        // 「1RunSegment=1SCS」前提の確認：StationConnection.Segmentsが仮に複数要素を持っていても
        // (通常運用では起こらないが)、実装はSegments[0]を機械的に採用することを明示的に検証する。
        var secondScsId = new StationConnectionSegmentId(2);
        var secondSeg = new StationConnectionSegment
        {
            Id = secondScsId, FromStationId = StA, ToStationId = StB,
            FromEntryPointId = EpA, ToEntryPointId = EpB, MainRouteId = new MainRouteId(1),
        };
        var (baseScs, segs, pathsById, arrivalIndex, departureIndex) = BuildTopology();
        var scWithTwoSegments = new StationConnection
        {
            Id = ScAB, Name = "AB", MainRouteId = new MainRouteId(1),
            Direction = StationConnectionDirection.Down, Segments = [ScsAB, secondScsId],
        };
        var allSegs = segs.Append(secondSeg).ToList();
        var train = MakeThroughTrain(1, departureSeconds: 1000, arrivalSeconds: 1500);

        var result = StationConnectionSegmentOccupancyProvider.BuildOccupancy(
            [train], [scWithTwoSegments], allSegs, pathsById, arrivalIndex, departureIndex);

        Assert.True(result.ContainsKey(ScsAB));
        Assert.False(result.ContainsKey(secondScsId));
    }
}

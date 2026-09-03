namespace DiaEditCore.Tests.Algorithm.Conflicts;

using DiaEditCore.Algorithm;
using DiaEditCore.Algorithm.Conflicts;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.TimeTable.Trains;

using Xunit;

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
        IReadOnlyList<MainRoute> AllMainRoutes,
        IReadOnlyDictionary<StationPathId, StationPath> PathsById,
        IReadOnlyDictionary<(EntryPointId, RailId), StationPathId> ArrivalIndex,
        IReadOnlyDictionary<(RailId, EntryPointId), StationPathId> DepartureIndex
    ) BuildTopology(int departureAdjustmentSec = 20, int arrivalAdjustmentSec = 30)
    {
        var segs = new List<StationConnectionSegment>
        {
            new() { Id = ScsAB, StationIdA = StA, StationIdB = StB, EntryPointIdA = EpA, EntryPointIdB = EpB, MainRouteId = new MainRouteId(1), },
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
        var allMainRoutes = new List<MainRoute> { new() { Id = new MainRouteId(1), Name = new DisplayName { Name = "Route1" }, StationOrder = new List<StationId> { StA, StB } } };

        return (scs, segs, allMainRoutes, pathsById, arrivalIndex, departureIndex);
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
        var (scs, segs, allMainRoutes, pathsById, arrivalIndex, departureIndex) = BuildTopology(departureAdjustmentSec: 20, arrivalAdjustmentSec: 30);
        var train = MakeThroughTrain(1, departureSeconds: 1000, arrivalSeconds: 1500);

        var result = StationConnectionSegmentOccupancyProvider.BuildOccupancy([train], scs, segs, allMainRoutes, pathsById, arrivalIndex, departureIndex);

        var occ = GetScsAbOccupancy(result);
        var entry = Assert.Single(occ);
        Assert.Equal(train.Id, entry.TrainId);
        Assert.Equal(1020, entry.StartSeconds); // departureBasis(1000) + AdjustmentSec(20)
        Assert.Equal(1470, entry.EndSeconds);   // arrivalBasis(1500) - AdjustmentSec(30)
    }

    [Fact]
    public void 複数Trainが同じSCSを重複する時間帯で使用する場合ConflictCheckerで検出できる()
    {
        var (scs, segs, allMainRoutes, pathsById, arrivalIndex, departureIndex) = BuildTopology();
        var train1 = MakeThroughTrain(1, departureSeconds: 1000, arrivalSeconds: 1500, "1001M");
        var train2 = MakeThroughTrain(2, departureSeconds: 1100, arrivalSeconds: 1600, "1002M"); // train1と重複区間あり
        var trains = new[] { train1, train2 };

        var result = StationConnectionSegmentOccupancyProvider.BuildOccupancy(trains, scs, segs, allMainRoutes, pathsById, arrivalIndex, departureIndex);
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
        var (scs, segs, allMainRoutes, pathsById, arrivalIndex, departureIndex) = BuildTopology();
        var train1 = MakeThroughTrain(1, departureSeconds: 1000, arrivalSeconds: 1500, "1001M");
        var train2 = MakeThroughTrain(2, departureSeconds: 2000, arrivalSeconds: 2500, "1002M"); // 重複なし
        var trains = new[] { train1, train2 };

        var result = StationConnectionSegmentOccupancyProvider.BuildOccupancy(trains, scs, segs, allMainRoutes, pathsById, arrivalIndex, departureIndex);
        var occ = GetScsAbOccupancy(result);

        var checker = new ConflictChecker(new StationConnectionSegmentObjectId(ScsAB), occ);
        var overlaps = checker.CheckOverlap();

        Assert.Empty(overlaps);
    }

    [Fact]
    public void 出発または到着のStopTimeが欠けている場合は占有として追加されない()
    {
        var (scs, segs, allMainRoutes, pathsById, arrivalIndex, departureIndex) = BuildTopology();
        var train = NewTrain(1, "1000M");
        train.RunSegments.Add(new TrainRunSegment { FromStationId = StA, ToStationId = StB, StationConnectionId = ScAB });
        // 出発StopTimeのみ設定、到着StopTimeは未設定のまま
        train.StopTimesInternal[new StopKey(StA, 0)] = new StopTime
        {
            IsStop = true,
            DepartureSeconds = 1000,
            TrackRailId = TrackA,
        };

        var result = StationConnectionSegmentOccupancyProvider.BuildOccupancy([train], scs, segs, allMainRoutes, pathsById, arrivalIndex, departureIndex);

        Assert.Empty(GetScsAbOccupancy(result));
    }

    [Fact]
    public void RunSegmentsが空のTrainはスキップされ例外を投げない()
    {
        var (scs, segs, allMainRoutes, pathsById, arrivalIndex, departureIndex) = BuildTopology();
        var emptyTrain = NewTrain(1, "empty");

        var exception = Record.Exception(() =>
            StationConnectionSegmentOccupancyProvider.BuildOccupancy([emptyTrain], scs, segs, allMainRoutes, pathsById, arrivalIndex, departureIndex));

        Assert.Null(exception);
    }

    [Fact]
    public void ホップの発着駅と一致するSCSが採用される_Segmentsが複数でも順序に依存しない()
    {
        // 「Segments[0]固定」ではなく「ホップの発着駅と一致するSCSが選ばれる」ことを検証する。
        // あえてSegments配列の並びを「一致しないSCSを先頭」にして、先頭固定ロジックが
        // 復活していないことを積極的に検知できるようにする。
        var unrelatedStA = new StationId(100);
        var unrelatedStB = new StationId(101);
        var unrelatedSegId = new StationConnectionSegmentId(2);
        var unrelatedSeg = new StationConnectionSegment
        {
            Id = unrelatedSegId, StationIdA = unrelatedStA, StationIdB = unrelatedStB,
            EntryPointIdA = new EntryPointId(910), EntryPointIdB = new EntryPointId(920),
            MainRouteId = new MainRouteId(1),
        };
        var (baseScs, segs, allMainRoutes, pathsById, arrivalIndex, departureIndex) = BuildTopology();
        // Segments配列の先頭を「無関係なSCS」にする
        var scWithUnrelatedFirst = new StationConnection
        {
            Id = ScAB, Name = "AB", MainRouteId = new MainRouteId(1),
            Direction = StationConnectionDirection.Down, Segments = [unrelatedSegId, ScsAB],
        };
        var allSegs = segs.Append(unrelatedSeg).ToList();
        var train = MakeThroughTrain(1, departureSeconds: 1000, arrivalSeconds: 1500);
    
        var result = StationConnectionSegmentOccupancyProvider.BuildOccupancy(
            [train], [scWithUnrelatedFirst], allSegs, allMainRoutes, pathsById, arrivalIndex, departureIndex);
    
        // 先頭固定なら誤ってunrelatedSegIdに計上されるはずが、正しくScsABに計上される
        Assert.True(result.ContainsKey(ScsAB));
        Assert.False(result.ContainsKey(unrelatedSegId));
    }
    
    [Fact]
    public void 広域SCが複数ホップをカバーする場合各ホップが対応するSCSへ個別に占有計上される()
    {
        // v12.29回帰テスト：ServiceRouteToRunSegmentsResolverが正式サポートする「広域SC」
        // （1つのStationConnectionが複数ホップをカバーし、複数のTrainRunSegmentから同一
        // StationConnectionIdとして参照される構成）に対して、A→BホップはSCS(seg1)へ、
        // B→CホップはSCS(seg2)へ、それぞれ正しく分離して占有計上されることを検証する。
        // 修正前は両ホップともSegments[0]（seg1固定）に誤って計上されていた。
        //
        // B駅の到着・出発は同一EntryPoint／同一Track（スルー運転の一般的な構成、
        // 他の既存テストフィクスチャと同じ流儀）を使う。Train.StopTime.TrackRailIdは
        // 1駅訪問につき1つしか持てないため、到着・出発で別々のTrackを割り当てると
        // 出発側インデックスの検索キーが一致せず占有情報が欠落してしまう。
        var stA = new StationId(1);
        var stB = new StationId(2);
        var stC = new StationId(3);
        var epA = new EntryPointId(10);
        var epB = new EntryPointId(20); // B駅側：到着・出発で共用
        var epC = new EntryPointId(30);
    
        var seg1 = new StationConnectionSegment
        {
            Id = new StationConnectionSegmentId(10), StationIdA = stA, StationIdB = stB,
            EntryPointIdA = epA, EntryPointIdB = epB, MainRouteId = new MainRouteId(1),
        };
        var seg2 = new StationConnectionSegment
        {
            Id = new StationConnectionSegmentId(11), StationIdA = stB, StationIdB = stC,
            EntryPointIdA = epB, EntryPointIdB = epC, MainRouteId = new MainRouteId(1),
        };
        var wideScId = new StationConnectionId(50);
        var wideSc = new StationConnection
        {
            Id = wideScId, Name = "wide", MainRouteId = new MainRouteId(1),
            Direction = StationConnectionDirection.Down, Segments = [seg1.Id, seg2.Id],
        };
    
        var trackA = new RailId(60);
        var trackB = new RailId(61); // B駅：到着・出発共用の単一Track
        var trackC = new RailId(62);
        var bpA = new BoundaryPointId(70);
        var bpB = new BoundaryPointId(71);
        var bpC = new BoundaryPointId(73);
    
        var departurePathA = new StationPath
        {
            Id = new StationPathId(201), FloorUnitId = new FloorUnitId(1), Name = "A出発",
            Direction = StationPathDirection.Departure,
            Waypoints = [new BoundaryPointWaypoint(bpA), new EntryPointWaypoint(epA)],
            AdjustmentSec = 20,
        };
        var arrivalPathB = new StationPath
        {
            Id = new StationPathId(202), FloorUnitId = new FloorUnitId(1), Name = "B到着",
            Direction = StationPathDirection.Arrival,
            Waypoints = [new EntryPointWaypoint(epB), new BoundaryPointWaypoint(bpB)],
            AdjustmentSec = 30,
        };
        var departurePathB = new StationPath
        {
            Id = new StationPathId(203), FloorUnitId = new FloorUnitId(1), Name = "B出発",
            Direction = StationPathDirection.Departure,
            Waypoints = [new BoundaryPointWaypoint(bpB), new EntryPointWaypoint(epB)],
            AdjustmentSec = 20,
        };
        var arrivalPathC = new StationPath
        {
            Id = new StationPathId(204), FloorUnitId = new FloorUnitId(1), Name = "C到着",
            Direction = StationPathDirection.Arrival,
            Waypoints = [new EntryPointWaypoint(epC), new BoundaryPointWaypoint(bpC)],
            AdjustmentSec = 30,
        };
    
        var pathsById = new Dictionary<StationPathId, StationPath>
        {
            [departurePathA.Id] = departurePathA,
            [arrivalPathB.Id] = arrivalPathB,
            [departurePathB.Id] = departurePathB,
            [arrivalPathC.Id] = arrivalPathC,
        };
        var rails = new List<Rail>
        {
            new() { Id = trackA, LengthM = 200, SpeedLimitKph = 25, Role = RailRole.Track, EndpointA = new EntryPointEndpointRef(epA), EndpointB = new BoundaryPointEndpointRef(bpA) },
            new() { Id = trackB, LengthM = 200, SpeedLimitKph = 25, Role = RailRole.Track, EndpointA = new EntryPointEndpointRef(epB), EndpointB = new BoundaryPointEndpointRef(bpB) },
            new() { Id = trackC, LengthM = 200, SpeedLimitKph = 25, Role = RailRole.Track, EndpointA = new EntryPointEndpointRef(epC), EndpointB = new BoundaryPointEndpointRef(bpC) },
        };
        var (arrivalIndex, departureIndex) = StationPathTrackIndexBuilder.Build(
            [departurePathA, arrivalPathB, departurePathB, arrivalPathC], rails);
    
        var allMainRoutes = new List<MainRoute>
        {
            new() { Id = new MainRouteId(1), Name = new DisplayName { Name = "Route1" }, StationOrder = [stA, stB, stC] },
        };
    
        var train = NewTrain(1, "1000M");
        train.RunSegments.Add(new TrainRunSegment { FromStationId = stA, ToStationId = stB, StationConnectionId = wideScId });
        train.RunSegments.Add(new TrainRunSegment { FromStationId = stB, ToStationId = stC, StationConnectionId = wideScId });
        train.StopTimesInternal[new StopKey(stA, 0)] = new StopTime { IsStop = true, ArrivalSeconds = -1, DepartureSeconds = 1000, TrackRailId = trackA };
        train.StopTimesInternal[new StopKey(stB, 0)] = new StopTime { IsStop = true, ArrivalSeconds = 1100, DepartureSeconds = 1200, TrackRailId = trackB };
        train.StopTimesInternal[new StopKey(stC, 0)] = new StopTime { IsStop = true, ArrivalSeconds = 1400, DepartureSeconds = -1, TrackRailId = trackC };
    
        var result = StationConnectionSegmentOccupancyProvider.BuildOccupancy(
            [train], [wideSc], [seg1, seg2], allMainRoutes, pathsById, arrivalIndex, departureIndex);
    
        // A→BホップはSCS(seg1)へ、B→CホップはSCS(seg2)へ、それぞれ独立して計上されること
        Assert.True(result.ContainsKey(seg1.Id));
        Assert.True(result.ContainsKey(seg2.Id));
        Assert.Single(result[seg1.Id]);
        Assert.Single(result[seg2.Id]);
    
        var occ1 = result[seg1.Id][0];
        Assert.Equal(1020, occ1.StartSeconds); // A出発1000 + adj20
        Assert.Equal(1070, occ1.EndSeconds);   // B到着1100 - adj30
    
        var occ2 = result[seg2.Id][0];
        Assert.Equal(1220, occ2.StartSeconds); // B出発1200 + adj20
        Assert.Equal(1370, occ2.EndSeconds);   // C到着1400 - adj30
    }
}
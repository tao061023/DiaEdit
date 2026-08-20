using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.Routes;

using DiaEditCore.Serialization.Validation;
using DiaEditCore.Serialization.Validation.Routes;

using Xunit;

namespace DiaEditCore.Tests.Validation.Routes;
public class StationConnectionValidatorTests
{
    private static (Station s1, Station s2, MainRoute route, EntryPoint depEp, EntryPoint arrEp, StationConnectionSegment seg) MakeSimpleTwoStationSetup()
    {
        var s1 = new Station { Id = new StationId(1), DisplayName = new DisplayName { Name = "A駅" }, Type = StationType.Standard };
        var s2 = new Station { Id = new StationId(2), DisplayName = new DisplayName { Name = "B駅" }, Type = StationType.Standard };
        var route = new MainRoute { Id = new MainRouteId(1), Name = new DisplayName { Name = "テスト線" }, StationOrder = [s1.Id, s2.Id] };

        var depEp = new EntryPoint
        {
            Id = new EntryPointId(1),
            Base = new FloorUnitObjectBase { FloorUnitId = new FloorUnitId(1), Position = new Point(0, 0) },
            Type = EntryPointType.Departure,
        };
        var arrEp = new EntryPoint
        {
            Id = new EntryPointId(2),
            Base = new FloorUnitObjectBase { FloorUnitId = new FloorUnitId(2), Position = new Point(0, 0) },
            Type = EntryPointType.Arrival,
        };

        var seg = new StationConnectionSegment
        {
            Id = new StationConnectionSegmentId(1),
            FromStationId = s1.Id,
            ToStationId = s2.Id,
            FromEntryPointId = depEp.Id,
            ToEntryPointId = arrEp.Id,
            MainRouteId = route.Id,
        };

        return (s1, s2, route, depEp, arrEp, seg);
    }

    [Fact]
    public void 正常なDown方向StationConnectionは合格()
    {
        var (s1, s2, route, depEp, arrEp, seg) = MakeSimpleTwoStationSetup();
        var sc = new StationConnection { Id = new StationConnectionId(1), Name = "下り本線", MainRouteId = route.Id, Direction = StationConnectionDirection.Down, Segments = [seg.Id] };
        var context = new ValidationContext
        {
            Stations = [s1, s2],
            MainRoutes = [route],
            EntryPoints = [depEp, arrEp],
            StationConnectionSegments = [seg],
        };

        var issues = new StationConnectionValidator().Validate(sc, context);

        Assert.Empty(issues);
    }

    [Fact]
    public void 出発側EPのtypeがArrivalだと不合格()
    {
        var (s1, s2, route, depEp, arrEp, seg) = MakeSimpleTwoStationSetup();
        depEp.Type = EntryPointType.Arrival; // 出発側なのにArrival専用に変更 → 不整合

        var sc = new StationConnection { Id = new StationConnectionId(1), Name = "下り本線", MainRouteId = route.Id, Direction = StationConnectionDirection.Down, Segments = [seg.Id] };
        var context = new ValidationContext
        {
            Stations = [s1, s2],
            MainRoutes = [route],
            EntryPoints = [depEp, arrEp],
            StationConnectionSegments = [seg],
        };

        var issues = new StationConnectionValidator().Validate(sc, context);

        Assert.Contains(issues, i => i.Message.Contains("出発側EP"));
    }

    [Fact]
    public void SegmentsとStationOrderの順序が矛盾すると不合格()
    {
        var (s1, s2, route, depEp, arrEp, seg) = MakeSimpleTwoStationSetup();
        // Directionを逆にする（Up方向として登録）が、segはs1→s2のまま（Downの並び）→矛盾
        var sc = new StationConnection { Id = new StationConnectionId(1), Name = "上り本線", MainRouteId = route.Id, Direction = StationConnectionDirection.Up, Segments = [seg.Id] };
        var context = new ValidationContext
        {
            Stations = [s1, s2],
            MainRoutes = [route],
            EntryPoints = [depEp, arrEp],
            StationConnectionSegments = [seg],
        };

        var issues = new StationConnectionValidator().Validate(sc, context);

        Assert.Contains(issues, i => i.Message.Contains("期待値"));
    }
    
    private static Rail TrackRail(int id, RailEndpointRef a, RailEndpointRef b) => new()
    {
        Id = new RailId(id),
        LengthM = 100,
        SpeedLimitKph = 60,
        Role = RailRole.Track,
        EndpointA = a,
        EndpointB = b,
    };

    private static StationPath Path(int id, StationPathDirection dir, params StationPathWaypoint[] wps) => new()
    {
        Id = new StationPathId(id),
        FloorUnitId = new FloorUnitId(1),
        Name = $"path{id}",
        Direction = dir,
        Waypoints = wps.ToList(),
    };

    /// <summary>A-B-Cの3駅、SCS[0]=A→B、SCS[1]=B→C、B駅で到着線・出発線が同一Trackに接続する
    /// 正常系トポロジ一式（StationConnection・StationPathともに構築）を返す。</summary>
    private static (
        StationConnection sc, List<StationConnectionSegment> segs, MainRoute route,
        List<StationPath> paths, List<Rail> rails
    ) BuildConnectedThreeStationTopology(bool isLoop = false)
    {
        var stA = new StationId(1);
        var stB = new StationId(2);
        var stC = new StationId(3);
        var epA = new EntryPointId(1);
        var epBArr = new EntryPointId(2);
        var epBDep = new EntryPointId(3);
        var epC = new EntryPointId(4);
        var bp = new BoundaryPointId(10);
        var mainRouteId = new MainRouteId(1);

        var seg0 = new StationConnectionSegment
        {
            Id = new StationConnectionSegmentId(1), FromStationId = stA, ToStationId = stB,
            FromEntryPointId = epA, ToEntryPointId = epBArr, MainRouteId = mainRouteId,
        };
        var seg1 = new StationConnectionSegment
        {
            Id = new StationConnectionSegmentId(2), FromStationId = stB, ToStationId = stC,
            FromEntryPointId = epBDep, ToEntryPointId = epC, MainRouteId = mainRouteId,
        };

        var sc = new StationConnection
        {
            Id = new StationConnectionId(1), Name = "test-sc", MainRouteId = mainRouteId,
            Direction = StationConnectionDirection.Down, Segments = new List<StationConnectionSegmentId> { seg0.Id, seg1.Id },
        };

        var route = new MainRoute
        {
            Id = mainRouteId, Name = new DisplayName { Name = "test-route" },
            StationOrder = new List<StationId> { stA, stB, stC }, IsLoop = isLoop,
        };

        var trackB = TrackRail(1, new EntryPointEndpointRef(epBArr), new BoundaryPointEndpointRef(bp));
        var arrivalPath = Path(1, StationPathDirection.Arrival, new EntryPointWaypoint(epBArr), new BoundaryPointWaypoint(bp));
        var departurePath = Path(2, StationPathDirection.Departure, new BoundaryPointWaypoint(bp), new EntryPointWaypoint(epBDep));

        return (sc, new List<StationConnectionSegment> { seg0, seg1 }, route,
            new List<StationPath> { arrivalPath, departurePath }, new List<Rail> { trackB });
    }

    private static ValidationContext MakeContext(
        StationConnection sc, List<StationConnectionSegment> segs, MainRoute route,
        List<StationPath> paths, List<Rail> rails) => new()
    {
        StationConnections = new[] { sc },
        StationConnectionSegments = segs,
        MainRoutes = new[] { route },
        StationPaths = paths,
        Rails = rails,
        EntryPoints = new List<EntryPoint>
        {
            new() { Id = new EntryPointId(1), Base = new FloorUnitObjectBase { FloorUnitId = new FloorUnitId(1), Position = default }, Type = EntryPointType.Departure },
            new() { Id = new EntryPointId(2), Base = new FloorUnitObjectBase { FloorUnitId = new FloorUnitId(1), Position = default }, Type = EntryPointType.Arrival },
            new() { Id = new EntryPointId(3), Base = new FloorUnitObjectBase { FloorUnitId = new FloorUnitId(1), Position = default }, Type = EntryPointType.Departure },
            new() { Id = new EntryPointId(4), Base = new FloorUnitObjectBase { FloorUnitId = new FloorUnitId(1), Position = default }, Type = EntryPointType.Arrival },
        },
    };

    [Fact]
    public void MainRoute整合性_到着出発のTrack集合が重複する境界ではエラーが出ない()
    {
        var (sc, segs, route, paths, rails) = BuildConnectedThreeStationTopology();
        var context = MakeContext(sc, segs, route, paths, rails);
        var validator = new StationConnectionValidator();

        var issues = validator.Validate(sc, context);

        Assert.DoesNotContain(issues, i => i.Message.Contains("MainRoute整合性"));
    }

    [Fact]
    public void MainRoute整合性_Track集合が重複しない境界はエラーになる()
    {
        var (sc, segs, route, _, _) = BuildConnectedThreeStationTopology();
        // StationPath/Railを一切与えない＝到着・出発とも候補Trackが無く、重複しようがない
        var context = MakeContext(sc, segs, route, new List<StationPath>(), new List<Rail>());
        var validator = new StationConnectionValidator();

        var issues = validator.Validate(sc, context);

        Assert.Contains(issues, i => i.Message.Contains("MainRoute整合性"));
    }

    [Fact]
    public void MainRoute整合性_IsLoopがtrueで先頭末尾の境界が不整合ならエラーになる()
    {
        var (sc, segs, route, paths, rails) = BuildConnectedThreeStationTopology(isLoop: true);
        var context = MakeContext(sc, segs, route, paths, rails);
        var validator = new StationConnectionValidator();

        var issues = validator.Validate(sc, context);

        // 内部境界(A-B/B-C間)は整合するが、ループ境界(epA発・epC着)には対応するStationPathがないため不整合
        Assert.Contains(issues, i => i.Message.Contains("MainRoute整合性"));
    }

    [Fact]
    public void MainRoute整合性_SCSが1件のみでIsLoopがfalseなら境界チェック自体が発生しない()
    {
        var stA = new StationId(1);
        var stB = new StationId(2);
        var mainRouteId = new MainRouteId(1);
        var epA = new EntryPointId(1);
        var epB = new EntryPointId(2);

        var seg = new StationConnectionSegment
        {
            Id = new StationConnectionSegmentId(1), FromStationId = stA, ToStationId = stB,
            FromEntryPointId = epA, ToEntryPointId = epB, MainRouteId = mainRouteId,
        };
        var sc = new StationConnection
        {
            Id = new StationConnectionId(1), Name = "single-seg", MainRouteId = mainRouteId,
            Direction = StationConnectionDirection.Down, Segments = new List<StationConnectionSegmentId> { seg.Id },
        };
        var route = new MainRoute
        {
            Id = mainRouteId, Name = new DisplayName { Name = "r" },
            StationOrder = new List<StationId> { stA, stB }, IsLoop = false,
        };
        var context = MakeContext(sc, new List<StationConnectionSegment> { seg }, route, new List<StationPath>(), new List<Rail>());
        var validator = new StationConnectionValidator();

        var issues = validator.Validate(sc, context);

        Assert.DoesNotContain(issues, i => i.Message.Contains("MainRoute整合性"));
    }
}
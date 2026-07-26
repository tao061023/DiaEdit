using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.Stations;

using DiaEditCore.Algorithm;

using Xunit;

namespace DiaEditCore.Tests.Algorithm;

public class ReversalResolverTests
{
    // -----------------------------
    // ヘルパー
    // -----------------------------

    private static MainRoute MakeMainRoute(int id, params int[] stationIds) => new()
    {
        Id = new MainRouteId(id),
        Name = new DisplayName { Name = $"MainRoute{id}" },
        StationOrder = stationIds.Select(s => new StationId(s)).ToList(),
    };

    private static StationConnectionSegment MakeSegment(
        int id, int fromStation, int toStation, int fromEp, int toEp, int mainRouteId) => new()
    {
        Id = new StationConnectionSegmentId(id),
        FromStationId = new StationId(fromStation),
        ToStationId = new StationId(toStation),
        FromEntryPointId = new EntryPointId(fromEp),
        ToEntryPointId = new EntryPointId(toEp),
        MainRouteId = new MainRouteId(mainRouteId),
        BaseRunTimeSec = 60,
    };

    private static StationConnection MakeConnection(
        int id, int mainRouteId, StationConnectionDirection direction, params StationConnectionSegmentId[] segmentIds) => new()
    {
        Id = new StationConnectionId(id),
        Name = "test-sc",
        MainRouteId = new MainRouteId(mainRouteId),
        Direction = direction,
        Segments = segmentIds.ToList(),
    };

    private static Rail MakeTrackRail(int id, RailEndpointRef a, RailEndpointRef b) => new()
    {
        Id = new RailId(id),
        LengthM = 100,
        SpeedLimitKph = 60,
        Roll = RailRoll.Track,
        EndpointA = a,
        EndpointB = b,
    };

    private static StationPath MakePath(int id, StationPathDirection direction, int floorUnitId, params StationPathWaypoint[] waypoints) => new()
    {
        Id = new StationPathId(id),
        FloorUnitId = new FloorUnitId(floorUnitId),
        Name = $"path{id}",
        Direction = direction,
        Waypoints = waypoints.ToList(),
    };

    // -----------------------------
    // ResolveDirectionReversalStations
    // -----------------------------

    [Fact]
    public void 進入と進出が同一のTrack端点を共有すれば折り返し必須と判定される()
    {
        // MainRoute: 1 - 2 - 3 、駅2で判定
        var mainRoute = MakeMainRoute(1, 1, 2, 3);

        var segAB = MakeSegment(1, 1, 2, fromEp: 1, toEp: 2, mainRouteId: 1); // 到着EP=2
        var segBC = MakeSegment(2, 2, 3, fromEp: 3, toEp: 4, mainRouteId: 1); // 出発EP=3
        var scAB = MakeConnection(1, 1, StationConnectionDirection.Down, segAB.Id);
        var scBC = MakeConnection(2, 1, StationConnectionDirection.Down, segBC.Id);

        // 到着EP(2)を含むStationPath(Arrival)と出発EP(3)を含むStationPath(Departure)が
        // 同一のBufferStop(100)側で行き止まりを共有する（デッドエンド構造）
        var arrivalRail = MakeTrackRail(1,
            new EntryPointEndpointRef(new EntryPointId(2)),
            new BufferStopEndpointRef(new BufferStopId(100)));
        var departureRail = MakeTrackRail(2,
            new BufferStopEndpointRef(new BufferStopId(100)),
            new EntryPointEndpointRef(new EntryPointId(3)));

        var arrivalPath = MakePath(1, StationPathDirection.Arrival, floorUnitId: 2,
            new EntryPointWaypoint(new EntryPointId(2)), new BufferStopWaypoint(new BufferStopId(100)));
        var departurePath = MakePath(2, StationPathDirection.Departure, floorUnitId: 2,
            new BufferStopWaypoint(new BufferStopId(100)), new EntryPointWaypoint(new EntryPointId(3)));

        var pathsByStation = new Dictionary<StationId, IReadOnlyList<StationPath>>
        {
            [new StationId(2)] = [arrivalPath, departurePath],
        };

        var result = ReversalResolver.ResolveDirectionReversalStations(
            mainRoute,
            [mainRoute],
            [scAB, scBC],
            [segAB, segBC],
            [arrivalRail, departureRail],
            pathsByStation);

        Assert.True(result[new StationId(2)]);
    }

    [Fact]
    public void 進入と進出が別々のTrack端点なら折り返し不要と判定される()
    {
        var mainRoute = MakeMainRoute(1, 1, 2, 3);

        var segAB = MakeSegment(1, 1, 2, fromEp: 1, toEp: 2, mainRouteId: 1);
        var segBC = MakeSegment(2, 2, 3, fromEp: 3, toEp: 4, mainRouteId: 1);
        var scAB = MakeConnection(1, 1, StationConnectionDirection.Down, segAB.Id);
        var scBC = MakeConnection(2, 1, StationConnectionDirection.Down, segBC.Id);

        var arrivalRail = MakeTrackRail(1,
            new EntryPointEndpointRef(new EntryPointId(2)),
            new BufferStopEndpointRef(new BufferStopId(100)));
        // 出発側は別のSwitcher経由（BufferStop100を共有しない＝通過構造）
        var departureRail = MakeTrackRail(2,
            new SwitcherEndpointRef(new SwitcherId(5), 0),
            new EntryPointEndpointRef(new EntryPointId(3)));

        var arrivalPath = MakePath(1, StationPathDirection.Arrival, floorUnitId: 2,
            new EntryPointWaypoint(new EntryPointId(2)), new BufferStopWaypoint(new BufferStopId(100)));
        var departurePath = MakePath(2, StationPathDirection.Departure, floorUnitId: 2,
            new SwitcherWaypoint(new SwitcherId(5)), new EntryPointWaypoint(new EntryPointId(3)));

        var pathsByStation = new Dictionary<StationId, IReadOnlyList<StationPath>>
        {
            [new StationId(2)] = [arrivalPath, departurePath],
        };

        var result = ReversalResolver.ResolveDirectionReversalStations(
            mainRoute,
            [mainRoute],
            [scAB, scBC],
            [segAB, segBC],
            [arrivalRail, departureRail],
            pathsByStation);

        Assert.False(result[new StationId(2)]);
    }

    [Fact]
    public void 該当駅のStationPathが渡されていなければ結果に含まれない()
    {
        var mainRoute = MakeMainRoute(1, 1, 2, 3);

        var segAB = MakeSegment(1, 1, 2, fromEp: 1, toEp: 2, mainRouteId: 1);
        var segBC = MakeSegment(2, 2, 3, fromEp: 3, toEp: 4, mainRouteId: 1);
        var scAB = MakeConnection(1, 1, StationConnectionDirection.Down, segAB.Id);
        var scBC = MakeConnection(2, 1, StationConnectionDirection.Down, segBC.Id);

        var pathsByStation = new Dictionary<StationId, IReadOnlyList<StationPath>>(); // 空

        var result = ReversalResolver.ResolveDirectionReversalStations(
            mainRoute,
            [mainRoute],
            [scAB, scBC],
            [segAB, segBC],
            [],
            pathsByStation);

        Assert.False(result.ContainsKey(new StationId(2)));
    }

    [Fact]
    public void 対応するStationConnectionが存在しなければ判定不能で結果に含まれない()
    {
        var mainRoute = MakeMainRoute(1, 1, 2, 3);
        // StationConnectionを一切渡さない
        var arrivalPath = MakePath(1, StationPathDirection.Arrival, floorUnitId: 2,
            new EntryPointWaypoint(new EntryPointId(2)), new BufferStopWaypoint(new BufferStopId(100)));
        var pathsByStation = new Dictionary<StationId, IReadOnlyList<StationPath>>
        {
            [new StationId(2)] = [arrivalPath],
        };

        var result = ReversalResolver.ResolveDirectionReversalStations(
            mainRoute,
            [mainRoute],
            [],
            [],
            [],
            pathsByStation);

        Assert.False(result.ContainsKey(new StationId(2)));
    }

    // -----------------------------
    // ResolveReversesAtBoundary
    // -----------------------------

    [Fact]
    public void 境界駅が一致しなければnullを返す()
    {
        var mainRouteA = MakeMainRoute(1, 1, 2);
        var mainRouteB = MakeMainRoute(2, 3, 4);

        var prevSegment = new ServiceRouteSegment { MainRouteId = new MainRouteId(1), FromStationIndex = 0, ToStationIndex = 1 };
        var nextSegment = new ServiceRouteSegment { MainRouteId = new MainRouteId(2), FromStationIndex = 1, ToStationIndex = 0 }; // 境界駅が食い違う

        var result = ReversalResolver.ResolveReversesAtBoundary(
            prevSegment, nextSegment,
            [mainRouteA, mainRouteB], [], [], [], []);

        Assert.Null(result);
    }

    [Fact]
    public void 境界駅で進入出発が同一Track端点を共有すれば折り返し必須と判定される()
    {
        // MainRouteA: 1 - 2（境界駅=2）、MainRouteB: 2 - 3
        var mainRouteA = MakeMainRoute(1, 1, 2);
        var mainRouteB = MakeMainRoute(2, 2, 3);

        var segA = MakeSegment(1, 1, 2, fromEp: 1, toEp: 2, mainRouteId: 1); // 境界駅到着EP=2
        var segB = MakeSegment(2, 2, 3, fromEp: 3, toEp: 4, mainRouteId: 2); // 境界駅出発EP=3
        var scA = MakeConnection(1, 1, StationConnectionDirection.Down, segA.Id);
        var scB = MakeConnection(2, 2, StationConnectionDirection.Down, segB.Id);

        var arrivalRail = MakeTrackRail(1,
            new EntryPointEndpointRef(new EntryPointId(2)),
            new BufferStopEndpointRef(new BufferStopId(100)));
        var departureRail = MakeTrackRail(2,
            new BufferStopEndpointRef(new BufferStopId(100)),
            new EntryPointEndpointRef(new EntryPointId(3)));

        var arrivalPath = MakePath(1, StationPathDirection.Arrival, floorUnitId: 2,
            new EntryPointWaypoint(new EntryPointId(2)), new BufferStopWaypoint(new BufferStopId(100)));
        var departurePath = MakePath(2, StationPathDirection.Departure, floorUnitId: 2,
            new BufferStopWaypoint(new BufferStopId(100)), new EntryPointWaypoint(new EntryPointId(3)));

        var prevSegment = new ServiceRouteSegment { MainRouteId = new MainRouteId(1), FromStationIndex = 0, ToStationIndex = 1 };
        var nextSegment = new ServiceRouteSegment { MainRouteId = new MainRouteId(2), FromStationIndex = 0, ToStationIndex = 1 };

        var result = ReversalResolver.ResolveReversesAtBoundary(
            prevSegment, nextSegment,
            [mainRouteA, mainRouteB],
            [scA, scB],
            [segA, segB],
            [arrivalRail, departureRail],
            [arrivalPath, departurePath]);

        Assert.True(result);
    }

    [Fact]
    public void 複数StationPath候補のいずれかが重複すればOR判定でtrueになる()
    {
        var mainRoute = MakeMainRoute(1, 1, 2, 3);

        var segAB = MakeSegment(1, 1, 2, fromEp: 1, toEp: 2, mainRouteId: 1);
        var segBC = MakeSegment(2, 2, 3, fromEp: 3, toEp: 4, mainRouteId: 1);
        var scAB = MakeConnection(1, 1, StationConnectionDirection.Down, segAB.Id);
        var scBC = MakeConnection(2, 1, StationConnectionDirection.Down, segBC.Id);

        // 到着経路の候補は1つだが、出発経路の候補が2つあり、うち1つだけが重複する
        var arrivalRail = MakeTrackRail(1,
            new EntryPointEndpointRef(new EntryPointId(2)),
            new BufferStopEndpointRef(new BufferStopId(100)));
        var departureRailNonOverlap = MakeTrackRail(2,
            new SwitcherEndpointRef(new SwitcherId(5), 0),
            new EntryPointEndpointRef(new EntryPointId(3)));
        var departureRailOverlap = MakeTrackRail(3,
            new BufferStopEndpointRef(new BufferStopId(100)),
            new EntryPointEndpointRef(new EntryPointId(3)));

        var arrivalPath = MakePath(1, StationPathDirection.Arrival, floorUnitId: 2,
            new EntryPointWaypoint(new EntryPointId(2)), new BufferStopWaypoint(new BufferStopId(100)));
        var departurePathA = MakePath(2, StationPathDirection.Departure, floorUnitId: 2,
            new SwitcherWaypoint(new SwitcherId(5)), new EntryPointWaypoint(new EntryPointId(3)));
        var departurePathB = MakePath(3, StationPathDirection.Departure, floorUnitId: 2,
            new BufferStopWaypoint(new BufferStopId(100)), new EntryPointWaypoint(new EntryPointId(3)));

        var pathsByStation = new Dictionary<StationId, IReadOnlyList<StationPath>>
        {
            [new StationId(2)] = [arrivalPath, departurePathA, departurePathB],
        };

        var result = ReversalResolver.ResolveDirectionReversalStations(
            mainRoute,
            [mainRoute],
            [scAB, scBC],
            [segAB, segBC],
            [arrivalRail, departureRailNonOverlap, departureRailOverlap],
            pathsByStation);

        Assert.True(result[new StationId(2)]);
    }
}

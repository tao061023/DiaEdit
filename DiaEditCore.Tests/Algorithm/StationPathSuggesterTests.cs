using DiaEditCore.Algorithm;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations;

using Xunit;

namespace DiaEditCore.Tests.Algorithm;

public class StationPathSuggesterTests
{
    private static readonly StationId StTarget = new(1);
    private static readonly StationId StOther = new(2);
    private static readonly FloorUnitId FuTarget = new(1);
    private static readonly FloorUnitId FuOther = new(2);

    private static FloorUnitObjectBase Base(FloorUnitId fu) => new() { FloorUnitId = fu, Position = new Point(0, 0) };

    private static Dictionary<FloorUnitId, StationId> FloorUnitToStation() => new()
    {
        [FuTarget] = StTarget,
        [FuOther] = StOther,
    };

    /// <summary>EntryPoint(始点) - Rail - BoundaryPoint(終端) の単純な直線経路を持つトポロジーを作る。</summary>
    private static (
        Dictionary<RailId, Rail> Rails,
        Dictionary<BoundaryPointId, BoundaryPoint> Bps,
        Dictionary<EntryPointId, EntryPoint> Eps,
        Dictionary<BufferStopId, BufferStop> BufferStops,
        Dictionary<SwitcherId, Switcher> Switchers,
        EntryPointId StartEp,
        BoundaryPointId EndBp
    ) BuildSimpleLine()
    {
        var ep = new EntryPointId(1);
        var bp = new BoundaryPointId(1);
        var rail = new Rail
        {
            Id = new RailId(1), LengthM = 100, SpeedLimitKph = 25, Roll = RailRoll.Track,
            EndpointA = new EntryPointEndpointRef(ep), EndpointB = new BoundaryPointEndpointRef(bp),
        };

        var eps = new Dictionary<EntryPointId, EntryPoint>
        {
            [ep] = new() { Id = ep, Base = Base(FuTarget), Type = EntryPointType.Arrival },
        };
        var bps = new Dictionary<BoundaryPointId, BoundaryPoint>
        {
            [bp] = new() { Id = bp, Base = Base(FuTarget) },
        };
        var rails = new Dictionary<RailId, Rail> { [rail.Id] = rail };

        return (rails, bps, eps, new(), new(), ep, bp);
    }

    private static StationPathSuggester MakeSuggester(
        Dictionary<RailId, Rail> rails,
        Dictionary<BoundaryPointId, BoundaryPoint> bps,
        Dictionary<EntryPointId, EntryPoint> eps,
        Dictionary<BufferStopId, BufferStop> bufferStops,
        Dictionary<SwitcherId, Switcher> switchers) =>
        new(StTarget, rails, bps, eps, bufferStops, switchers, FloorUnitToStation());

    [Fact]
    public void EntryPointからBoundaryPointまでの単純な直線経路が1件候補として得られる()
    {
        var (rails, bps, eps, bufferStops, switchers, startEp, endBp) = BuildSimpleLine();
        var suggester = MakeSuggester(rails, bps, eps, bufferStops, switchers);

        var results = suggester.Suggest(new EntryPointEndpointRef(startEp));

        var candidate = Assert.Single(results);
        var wp = Assert.Single(candidate);
        Assert.Equal(new BoundaryPointWaypoint(endBp), wp);
    }

    [Fact]
    public void 隣駅のRailには進入しない()
    {
        var ep = new EntryPointId(1);
        var bpOther = new BoundaryPointId(2); // 隣駅（FuOther）
        var railToOther = new Rail
        {
            Id = new RailId(1), LengthM = 100, SpeedLimitKph = 25, Roll = RailRoll.Track,
            EndpointA = new EntryPointEndpointRef(ep), EndpointB = new BoundaryPointEndpointRef(bpOther),
        };

        var eps = new Dictionary<EntryPointId, EntryPoint> { [ep] = new() { Id = ep, Base = Base(FuTarget), Type = EntryPointType.Arrival } };
        var bps = new Dictionary<BoundaryPointId, BoundaryPoint>
        {
            [bpOther] = new() { Id = bpOther, Base = Base(FuOther) }, // 隣駅所属
        };
        var rails = new Dictionary<RailId, Rail> { [railToOther.Id] = railToOther };
        var suggester = MakeSuggester(rails, bps, eps, new(), new());

        var results = suggester.Suggest(new EntryPointEndpointRef(ep));

        // 開始点(ep)自体は自駅だが、その先のBoundaryPointが隣駅所属のため経路として確定しない
        Assert.Empty(results);
    }

    [Fact]
    public void ループ構造でも無限探索にならず停止する()
    {
        var epA = new EntryPointId(1);
        var swId = new SwitcherId(1);
        var bp = new BoundaryPointId(1);

        var rail1 = new Rail
        {
            Id = new RailId(1), LengthM = 50, SpeedLimitKph = 25, Roll = RailRoll.Track,
            EndpointA = new EntryPointEndpointRef(epA), EndpointB = new SwitcherEndpointRef(swId, 0),
        };
        var rail2 = new Rail
        {
            Id = new RailId(2), LengthM = 50, SpeedLimitKph = 25, Roll = RailRoll.Track,
            EndpointA = new SwitcherEndpointRef(swId, 1), EndpointB = new BoundaryPointEndpointRef(bp),
        };

        var switcher = new Switcher
        {
            Id = swId, Base = Base(FuTarget), PortCount = 3,
            Mechanism = new SwitchMechanism { RootPortIndex = 0, NormalPortIndex = 1, ReversePortIndex = 2 },
        };

        var eps = new Dictionary<EntryPointId, EntryPoint> { [epA] = new() { Id = epA, Base = Base(FuTarget), Type = EntryPointType.Arrival } };
        var bps = new Dictionary<BoundaryPointId, BoundaryPoint> { [bp] = new() { Id = bp, Base = Base(FuTarget) } };
        var switchers = new Dictionary<SwitcherId, Switcher> { [swId] = switcher };
        var rails = new Dictionary<RailId, Rail> { [rail1.Id] = rail1, [rail2.Id] = rail2 };
        var suggester = MakeSuggester(rails, bps, eps, new(), switchers);

        var exception = Record.Exception(() => suggester.Suggest(new EntryPointEndpointRef(epA)));

        Assert.Null(exception);
    }

    [Fact]
    public void SwitcherのRoot_Normal経路とRoot_Reverse経路がそれぞれ別候補として得られる()
    {
        var epA = new EntryPointId(1);
        var swId = new SwitcherId(1);
        var bpNormal = new BoundaryPointId(1);
        var bpReverse = new BoundaryPointId(2);

        var railIn = new Rail
        {
            Id = new RailId(1), LengthM = 50, SpeedLimitKph = 25, Roll = RailRoll.Track,
            EndpointA = new EntryPointEndpointRef(epA), EndpointB = new SwitcherEndpointRef(swId, 0), // root
        };
        var railNormal = new Rail
        {
            Id = new RailId(2), LengthM = 50, SpeedLimitKph = 25, Roll = RailRoll.Track,
            EndpointA = new SwitcherEndpointRef(swId, 1), EndpointB = new BoundaryPointEndpointRef(bpNormal),
        };
        var railReverse = new Rail
        {
            Id = new RailId(3), LengthM = 50, SpeedLimitKph = 25, Roll = RailRoll.Track,
            EndpointA = new SwitcherEndpointRef(swId, 2), EndpointB = new BoundaryPointEndpointRef(bpReverse),
        };

        var switcher = new Switcher
        {
            Id = swId, Base = Base(FuTarget), PortCount = 3,
            Mechanism = new SwitchMechanism { RootPortIndex = 0, NormalPortIndex = 1, ReversePortIndex = 2 },
        };

        var eps = new Dictionary<EntryPointId, EntryPoint> { [epA] = new() { Id = epA, Base = Base(FuTarget), Type = EntryPointType.Arrival } };
        var bps = new Dictionary<BoundaryPointId, BoundaryPoint>
        {
            [bpNormal] = new() { Id = bpNormal, Base = Base(FuTarget) },
            [bpReverse] = new() { Id = bpReverse, Base = Base(FuTarget) },
        };
        var switchers = new Dictionary<SwitcherId, Switcher> { [swId] = switcher };
        var rails = new Dictionary<RailId, Rail> { [railIn.Id] = railIn, [railNormal.Id] = railNormal, [railReverse.Id] = railReverse };
        var suggester = MakeSuggester(rails, bps, eps, new(), switchers);

        var results = suggester.Suggest(new EntryPointEndpointRef(epA));

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r[^1] == new BoundaryPointWaypoint(bpNormal));
        Assert.Contains(results, r => r[^1] == new BoundaryPointWaypoint(bpReverse));
    }

    [Fact]
    public void BufferStopで終端すると当該waypoint自体は候補配列に含まれない()
    {
        var ep = new EntryPointId(1);
        var bsId = new BufferStopId(1);
        var rail = new Rail
        {
            Id = new RailId(1), LengthM = 50, SpeedLimitKph = 25, Roll = RailRoll.Track,
            EndpointA = new EntryPointEndpointRef(ep), EndpointB = new BufferStopEndpointRef(bsId),
        };

        var eps = new Dictionary<EntryPointId, EntryPoint> { [ep] = new() { Id = ep, Base = Base(FuTarget), Type = EntryPointType.Arrival } };
        var bufferStops = new Dictionary<BufferStopId, BufferStop> { [bsId] = new() { Id = bsId, Base = Base(FuTarget) } };
        var rails = new Dictionary<RailId, Rail> { [rail.Id] = rail };
        var suggester = MakeSuggester(rails, new(), eps, bufferStops, new());

        var results = suggester.Suggest(new EntryPointEndpointRef(ep));

        var candidate = Assert.Single(results);
        Assert.Empty(candidate); // BufferStop自体はToWaypointがnullを返すため、waypoints配列は空
    }
}

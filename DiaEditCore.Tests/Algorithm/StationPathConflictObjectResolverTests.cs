using DiaEditCore.Algorithm;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations;

using Xunit;

namespace DiaEditCore.Tests.Algorithm;

public class StationPathConflictObjectResolverTests
{
    private static readonly EntryPointId Ep = new(1);
    private static readonly BoundaryPointId Bp = new(1);
    private static readonly SwitcherId Sw = new(1);
    private static readonly RailId Rail1 = new(1);
    private static readonly RailId Rail2 = new(2);

    /// <summary>EntryPoint - Rail1 - Switcher - Rail2 - BoundaryPoint という単純な経路のRail定義一式を作る。</summary>
    private static List<Rail> BuildRails() =>
    [
        new() { Id = Rail1, LengthM = 50, SpeedLimitKph = 25, Role = RailRole.Track,
                EndpointA = new EntryPointEndpointRef(Ep), EndpointB = new SwitcherEndpointRef(Sw, 0) },
        new() { Id = Rail2, LengthM = 50, SpeedLimitKph = 25, Role = RailRole.Track,
                EndpointA = new SwitcherEndpointRef(Sw, 1), EndpointB = new BoundaryPointEndpointRef(Bp) },
    ];

    private static StationPath MakePath(
        int id, List<StationPathWaypoint> waypoints, List<VirtualConflictObjectId>? manualIds = null) => new()
    {
        Id = new StationPathId(id),
        FloorUnitId = new FloorUnitId(1),
        Name = $"path{id}",
        Direction = StationPathDirection.Arrival,
        Waypoints = waypoints,
        ManualConflictObjectIds = manualIds ?? new(),
    };

    [Fact]
    public void RailSequence上のRail群がRailObjectIdとして返る()
    {
        var resolver = new RailSequenceResolver(BuildRails());
        var path = MakePath(1, [new EntryPointWaypoint(Ep), new SwitcherWaypoint(Sw), new BoundaryPointWaypoint(Bp)]);

        var result = StationPathConflictObjectResolver.Resolve(path, resolver);

        Assert.Contains(result, o => o is RailObjectId r && r.Id == Rail1);
        Assert.Contains(result, o => o is RailObjectId r && r.Id == Rail2);
    }

    [Fact]
    public void waypoints中のSwitcherWaypointはSwitcherObjectIdとしても追加される()
    {
        var resolver = new RailSequenceResolver(BuildRails());
        var path = MakePath(1, [new EntryPointWaypoint(Ep), new SwitcherWaypoint(Sw), new BoundaryPointWaypoint(Bp)]);

        var result = StationPathConflictObjectResolver.Resolve(path, resolver);

        Assert.Contains(result, o => o is SwitcherObjectId s && s.Id == Sw);
    }

    [Fact]
    public void ManualConflictObjectIdsはVirtualConflictObjectIdObjectとして追加される()
    {
        var vcoId = new VirtualConflictObjectId(1);
        var resolver = new RailSequenceResolver(BuildRails());
        var path = MakePath(1, [new EntryPointWaypoint(Ep), new SwitcherWaypoint(Sw), new BoundaryPointWaypoint(Bp)], [vcoId]);

        var result = StationPathConflictObjectResolver.Resolve(path, resolver);

        Assert.Contains(result, o => o is VirtualConflictObjectIdObject v && v.Id == vcoId);
    }

    [Fact]
    public void Rail_Switcher_手動指定が同時に存在すれば全て結合されて返る()
    {
        var vcoId = new VirtualConflictObjectId(1);
        var resolver = new RailSequenceResolver(BuildRails());
        var path = MakePath(1, [new EntryPointWaypoint(Ep), new SwitcherWaypoint(Sw), new BoundaryPointWaypoint(Bp)], [vcoId]);

        var result = StationPathConflictObjectResolver.Resolve(path, resolver);

        // Rail1, Rail2, Switcher, VirtualConflictObjectの4件
        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void ManualConflictObjectIdsが空でwaypointsにSwitcherが無ければRailObjectIdのみが返る()
    {
        var rails = new List<Rail>
        {
            new() { Id = Rail1, LengthM = 50, SpeedLimitKph = 25, Role = RailRole.Track,
                    EndpointA = new EntryPointEndpointRef(Ep), EndpointB = new BoundaryPointEndpointRef(Bp) },
        };
        var resolver = new RailSequenceResolver(rails);
        var path = MakePath(1, [new EntryPointWaypoint(Ep), new BoundaryPointWaypoint(Bp)]);

        var result = StationPathConflictObjectResolver.Resolve(path, resolver);

        var single = Assert.Single(result);
        Assert.IsType<RailObjectId>(single);
    }

    [Fact]
    public void GroupAll_同じRailを共有する2つのStationPathは同一グループに入る()
    {
        var rails = BuildRails();
        var resolver = new RailSequenceResolver(rails);
        var pathA = MakePath(1, [new EntryPointWaypoint(Ep), new SwitcherWaypoint(Sw), new BoundaryPointWaypoint(Bp)]);
        var pathB = MakePath(2, [new EntryPointWaypoint(Ep), new SwitcherWaypoint(Sw), new BoundaryPointWaypoint(Bp)]);

        var grouping = StationPathConflictObjectResolver.GroupAll([pathA, pathB], resolver);

        var rail1Group = grouping[new RailObjectId(Rail1)];
        Assert.Contains(pathA.Id, rail1Group);
        Assert.Contains(pathB.Id, rail1Group);
    }

    [Fact]
    public void GroupAll_共有オブジェクトが無い2つのStationPathは別グループになる()
    {
        var epOther = new EntryPointId(2);
        var bpOther = new BoundaryPointId(2);
        var railOther = new RailId(3);

        var rails = new List<Rail>
        {
            new() { Id = Rail1, LengthM = 50, SpeedLimitKph = 25, Role = RailRole.Track,
                    EndpointA = new EntryPointEndpointRef(Ep), EndpointB = new BoundaryPointEndpointRef(Bp) },
            new() { Id = railOther, LengthM = 50, SpeedLimitKph = 25, Role = RailRole.Track,
                    EndpointA = new EntryPointEndpointRef(epOther), EndpointB = new BoundaryPointEndpointRef(bpOther) },
        };
        var resolver = new RailSequenceResolver(rails);
        var pathA = MakePath(1, [new EntryPointWaypoint(Ep), new BoundaryPointWaypoint(Bp)]);
        var pathB = MakePath(2, [new EntryPointWaypoint(epOther), new BoundaryPointWaypoint(bpOther)]);

        var grouping = StationPathConflictObjectResolver.GroupAll([pathA, pathB], resolver);

        Assert.DoesNotContain(pathB.Id, grouping[new RailObjectId(Rail1)]);
        Assert.DoesNotContain(pathA.Id, grouping[new RailObjectId(railOther)]);
    }

    [Fact]
    public void GroupAll_対象StationPathが0件なら空の辞書を返す()
    {
        var resolver = new RailSequenceResolver(BuildRails());

        var grouping = StationPathConflictObjectResolver.GroupAll([], resolver);

        Assert.Empty(grouping);
    }
}

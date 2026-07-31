using DiaEditCore.Algorithm;

using DiaEditCore.Model;
using DiaEditCore.Model.Stations;

using Xunit;

namespace DiaEditCore.Tests.Algorithm;

public sealed class RailSequenceResolverTests
{
    // ---------------------------
    // 1. 正常系：2点 → Rail 1 本
    // ---------------------------
    [Fact]
    public void Resolve_TwoWaypoints_ReturnsSingleRail()
    {
        var rails = new[]
        {
            new Rail
            {
                Id = new RailId(1),
                EndpointA = new EntryPointEndpointRef(new EntryPointId(10)),
                EndpointB = new SwitcherEndpointRef(new SwitcherId(5), 0),
                LengthM = 100,
                SpeedLimitKph = 45,
                Role = RailRole.Normal
            }
        };

        var path = new StationPath
        {
            Id = new StationPathId(100),
            FloorUnitId = new FloorUnitId(1),
            Name = "Test",
            Direction = StationPathDirection.Arrival,
            Waypoints = new()
            {
                new EntryPointWaypoint(new EntryPointId(10)),
                new SwitcherWaypoint(new SwitcherId(5))
            }
        };

        var resolver = new RailSequenceResolver(rails);
        var seq = resolver.Resolve(path);

        Assert.Single(seq);
        Assert.Equal(1, seq[0].Value);
    }

    // ---------------------------
    // 2. 正常系：3点 → Rail 2 本
    // ---------------------------
    [Fact]
    public void Resolve_MultipleWaypoints_ReturnsRailSequence()
    {
        var rails = new[]
        {
            new Rail
            {
                Id = new RailId(1),
                EndpointA = new EntryPointEndpointRef(new EntryPointId(10)),
                EndpointB = new SwitcherEndpointRef(new SwitcherId(5), 0),
                LengthM = 100, SpeedLimitKph = 45, Role = RailRole.Normal
            },
            new Rail
            {
                Id = new RailId(2),
                EndpointA = new SwitcherEndpointRef(new SwitcherId(5), 0),
                EndpointB = new BoundaryPointEndpointRef(new BoundaryPointId(3)),
                LengthM = 80, SpeedLimitKph = 45, Role = RailRole.Normal
            }
        };

        var path = new StationPath
        {
            Id = new StationPathId(100),
            FloorUnitId = new FloorUnitId(1),
            Name = "Test",
            Direction = StationPathDirection.Arrival,
            Waypoints = new()
            {
                new EntryPointWaypoint(new EntryPointId(10)),
                new SwitcherWaypoint(new SwitcherId(5)),
                new BoundaryPointWaypoint(new BoundaryPointId(3))
            }
        };

        var resolver = new RailSequenceResolver(rails);
        var seq = resolver.Resolve(path);

        Assert.Equal(new[] { 1, 2 }, seq.Select(x => x.Value));
    }

    // ---------------------------
    // 3. EndpointA/B の順序反転
    // ---------------------------
    [Fact]
    public void Resolve_RailEndpointsReversed_StillMatches()
    {
        var rails = new[]
        {
            new Rail
            {
                Id = new RailId(1),
                EndpointA = new SwitcherEndpointRef(new SwitcherId(5), 0),
                EndpointB = new EntryPointEndpointRef(new EntryPointId(10)),
                LengthM = 100, SpeedLimitKph = 45, Role = RailRole.Normal
            }
        };

        var path = new StationPath
        {
            Id = new StationPathId(100),
            FloorUnitId = new FloorUnitId(1),
            Name = "Test",
            Direction = StationPathDirection.Arrival,
            Waypoints = new()
            {
                new EntryPointWaypoint(new EntryPointId(10)),
                new SwitcherWaypoint(new SwitcherId(5))
            }
        };

        var resolver = new RailSequenceResolver(rails);
        var seq = resolver.Resolve(path);

        Assert.Single(seq);
        Assert.Equal(1, seq[0].Value);
    }

    // ---------------------------
    // 4. Rail が存在しない → 例外
    // ---------------------------
    [Fact]
    public void Resolve_NoMatchingRail_Throws()
    {
        var rails = Array.Empty<Rail>();

        var path = new StationPath
        {
            Id = new StationPathId(100),
            FloorUnitId = new FloorUnitId(1),
            Name = "Test",
            Direction = StationPathDirection.Arrival,
            Waypoints = new()
            {
                new EntryPointWaypoint(new EntryPointId(10)),
                new SwitcherWaypoint(new SwitcherId(5))
            }
        };

        var resolver = new RailSequenceResolver(rails);

        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(path));
    }

    // ---------------------------
    // 5. Waypoints が 1 以下 → 空
    // ---------------------------
    [Fact]
    public void Resolve_OneWaypoint_ReturnsEmpty()
    {
        var rails = Array.Empty<Rail>();

        var path = new StationPath
        {
            Id = new StationPathId(100),
            FloorUnitId = new FloorUnitId(1),
            Name = "Test",
            Direction = StationPathDirection.Arrival,
            Waypoints = new()
            {
                new EntryPointWaypoint(new EntryPointId(10))
            }
        };

        var resolver = new RailSequenceResolver(rails);
        var seq = resolver.Resolve(path);

        Assert.Empty(seq);
    }
}

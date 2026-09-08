namespace DiaEditCore.Tests.Algorithm.Stations.FloorUnitObjects;

using System;
using System.Collections.Generic;

using DiaEditCore.Algorithm.Stations.FloorUnitObjects;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;

using Xunit;

public sealed class RailEndpointConvergenceResolverTests
{
    private static FloorUnitObjectBase MakeBase(Point p) =>
        new() { FloorUnitId = new FloorUnitId(1), Position = p };

    private static Rail MakeRail(int id, RailEndpointRef a, RailEndpointRef b) => new()
    {
        Id = new RailId(id),
        Name = "",
        LengthM = 10,
        SpeedLimitKph = 25,
        Role = RailRole.Normal,
        EndpointA = a,
        EndpointB = b,
    };

    // ================================
    // ResolvePosition
    // ================================

    [Fact]
    public void ResolvePosition_NoneEndpoint_ReturnsItsPosition()
    {
        var none = new NoneEndpoint { Id = new NoneEndpointId(1), Base = MakeBase(new Point(3, 4)) };

        var pos = RailEndpointConvergenceResolver.ResolvePosition(
            new NoneEndpointRef(none.Id),
            new List<NoneEndpoint> { none },
            Array.Empty<BoundaryPoint>(), Array.Empty<EntryPoint>(),
            Array.Empty<BufferStop>(), Array.Empty<Switcher>());

        Assert.Equal(new Point(3, 4), pos);
    }

    [Fact]
    public void ResolvePosition_Switcher_ReturnsItsPosition_RegardlessOfPortIndex()
    {
        var sw = new Switcher { Id = new SwitcherId(1), Base = MakeBase(new Point(9, 9)), PortCount = 3 };

        var pos = RailEndpointConvergenceResolver.ResolvePosition(
            new SwitcherEndpointRef(sw.Id, PortIndex: 2),
            Array.Empty<NoneEndpoint>(), Array.Empty<BoundaryPoint>(), Array.Empty<EntryPoint>(),
            Array.Empty<BufferStop>(), new List<Switcher> { sw });

        Assert.Equal(new Point(9, 9), pos);
    }

    [Fact]
    public void ResolvePosition_MissingObject_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            RailEndpointConvergenceResolver.ResolvePosition(
                new NoneEndpointRef(new NoneEndpointId(99)),
                new List<NoneEndpoint>(),
                Array.Empty<BoundaryPoint>(), Array.Empty<EntryPoint>(),
                Array.Empty<BufferStop>(), Array.Empty<Switcher>()));
    }

    // ================================
    // FindConvergingEndpoints
    // ================================

    [Fact]
    public void FindConvergingEndpoints_FindsBothEndsOfSameRail_WhenBothAtSamePosition()
    {
        // 自己ループ（両端が同一座標）。ドキュメント上「特別扱いしない」とされている通り、
        // 両端とも普通に収束集合に加わることを確認する。
        var pos = new Point(5, 5);
        var noneEntities = new List<NoneEndpoint>
        {
            new() { Id = new NoneEndpointId(1), Base = MakeBase(pos) },
            new() { Id = new NoneEndpointId(2), Base = MakeBase(pos) },
        };
        var rail = MakeRail(1, new NoneEndpointRef(new NoneEndpointId(1)), new NoneEndpointRef(new NoneEndpointId(2)));

        var result = RailEndpointConvergenceResolver.FindConvergingEndpoints(
            pos, new List<Rail> { rail }, noneEntities,
            Array.Empty<BoundaryPoint>(), Array.Empty<EntryPoint>(), Array.Empty<BufferStop>(), Array.Empty<Switcher>());

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.RailId.Equals(new RailId(1)) && r.End == RailEnd.A);
        Assert.Contains(result, r => r.RailId.Equals(new RailId(1)) && r.End == RailEnd.B);
    }

    [Fact]
    public void FindConvergingEndpoints_ExcludesRailsAtOtherPositions()
    {
        var target = new Point(0, 0);
        var elsewhere = new Point(100, 100);

        var noneEntities = new List<NoneEndpoint>
        {
            new() { Id = new NoneEndpointId(1), Base = MakeBase(target) },
            new() { Id = new NoneEndpointId(2), Base = MakeBase(elsewhere) },
            new() { Id = new NoneEndpointId(3), Base = MakeBase(elsewhere) },
        };
        var railAtTarget = MakeRail(1, new NoneEndpointRef(new NoneEndpointId(1)), new NoneEndpointRef(new NoneEndpointId(2)));
        var railElsewhere = MakeRail(2, new NoneEndpointRef(new NoneEndpointId(2)), new NoneEndpointRef(new NoneEndpointId(3)));

        var result = RailEndpointConvergenceResolver.FindConvergingEndpoints(
            target, new List<Rail> { railAtTarget, railElsewhere }, noneEntities,
            Array.Empty<BoundaryPoint>(), Array.Empty<EntryPoint>(), Array.Empty<BufferStop>(), Array.Empty<Switcher>());

        Assert.Single(result);
        Assert.Equal(new RailId(1), result[0].RailId);
        Assert.Equal(RailEnd.A, result[0].End);
    }

    [Fact]
    public void FindConvergingEndpoints_NoRails_ReturnsEmpty()
    {
        var result = RailEndpointConvergenceResolver.FindConvergingEndpoints(
            new Point(0, 0), Array.Empty<Rail>(),
            Array.Empty<NoneEndpoint>(), Array.Empty<BoundaryPoint>(), Array.Empty<EntryPoint>(),
            Array.Empty<BufferStop>(), Array.Empty<Switcher>());

        Assert.Empty(result);
    }

    // ================================
    // Classify
    // ================================

    private static List<RailEndpointLocation> MakeNEntryPointLocations(int n)
    {
        var list = new List<RailEndpointLocation>();
        for (var i = 0; i < n; i++)
            list.Add(new RailEndpointLocation(new RailId(i + 1), RailEnd.A, new EntryPointEndpointRef(new EntryPointId(i + 1))));
        return list;
    }

    [Fact]
    public void Classify_N0_ReturnsVanish()
    {
        var result = RailEndpointConvergenceResolver.Classify(Array.Empty<RailEndpointLocation>());
        Assert.Equal(ConvergenceKind.Vanish, result.Kind);
    }

    [Fact]
    public void Classify_N1_ReturnsKeep()
    {
        var result = RailEndpointConvergenceResolver.Classify(MakeNEntryPointLocations(1));
        Assert.Equal(ConvergenceKind.Keep, result.Kind);
    }

    [Fact]
    public void Classify_N2_ReturnsBoundaryPoint()
    {
        var result = RailEndpointConvergenceResolver.Classify(MakeNEntryPointLocations(2));
        Assert.Equal(ConvergenceKind.BoundaryPoint, result.Kind);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void Classify_3or4WithoutExistingSwitcher_ReturnsSwitcherNew(int n)
    {
        var result = RailEndpointConvergenceResolver.Classify(MakeNEntryPointLocations(n));
        Assert.Equal(ConvergenceKind.SwitcherNew, result.Kind);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void Classify_3or4WithExistingSwitcherRef_ReturnsSwitcherExpand(int n)
    {
        var list = MakeNEntryPointLocations(n - 1);
        list.Add(new RailEndpointLocation(new RailId(999), RailEnd.B, new SwitcherEndpointRef(new SwitcherId(1), 0)));

        var result = RailEndpointConvergenceResolver.Classify(list);
        Assert.Equal(ConvergenceKind.SwitcherExpand, result.Kind);
    }

    [Fact]
    public void Classify_MultiplePortsOfSameSwitcher_StillReturnsSwitcherExpand()
    {
        // 同一Switcherの複数ポートが収束集合に混在していても拡張と判定できることの確認
        // （ドキュメント記載の前提の直接検証）。
        var list = new List<RailEndpointLocation>
        {
            new(new RailId(1), RailEnd.A, new SwitcherEndpointRef(new SwitcherId(7), 0)),
            new(new RailId(2), RailEnd.A, new SwitcherEndpointRef(new SwitcherId(7), 1)),
            new(new RailId(3), RailEnd.A, new EntryPointEndpointRef(new EntryPointId(1))),
        };

        var result = RailEndpointConvergenceResolver.Classify(list);
        Assert.Equal(ConvergenceKind.SwitcherExpand, result.Kind);
    }

    [Fact]
    public void Classify_N5_ReturnsError_WithMessage()
    {
        var result = RailEndpointConvergenceResolver.Classify(MakeNEntryPointLocations(5));
        Assert.Equal(ConvergenceKind.Error, result.Kind);
        Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
    }

    [Fact]
    public void Classify_N6_ReturnsError()
    {
        var result = RailEndpointConvergenceResolver.Classify(MakeNEntryPointLocations(6));
        Assert.Equal(ConvergenceKind.Error, result.Kind);
    }

    // ================================
    // AssignSwitcherPorts
    // ================================

    [Fact]
    public void AssignSwitcherPorts_OrdersByRailIdThenEnd_AndAssigns0BasedIndices()
    {
        var list = new List<RailEndpointLocation>
        {
            new(new RailId(3), RailEnd.B, new EntryPointEndpointRef(new EntryPointId(1))),
            new(new RailId(1), RailEnd.B, new EntryPointEndpointRef(new EntryPointId(2))),
            new(new RailId(1), RailEnd.A, new EntryPointEndpointRef(new EntryPointId(3))),
            new(new RailId(2), RailEnd.A, new EntryPointEndpointRef(new EntryPointId(4))),
        };

        var result = RailEndpointConvergenceResolver.AssignSwitcherPorts(list);

        Assert.Equal(4, result.Count);

        Assert.Equal(new RailId(1), result[0].Location.RailId);
        Assert.Equal(RailEnd.A, result[0].Location.End);
        Assert.Equal(0, result[0].PortIndex);

        Assert.Equal(new RailId(1), result[1].Location.RailId);
        Assert.Equal(RailEnd.B, result[1].Location.End);
        Assert.Equal(1, result[1].PortIndex);

        Assert.Equal(new RailId(2), result[2].Location.RailId);
        Assert.Equal(2, result[2].PortIndex);

        Assert.Equal(new RailId(3), result[3].Location.RailId);
        Assert.Equal(3, result[3].PortIndex);
    }

    [Fact]
    public void AssignSwitcherPorts_IsDeterministic_RegardlessOfInputOrder()
    {
        var a = new RailEndpointLocation(new RailId(1), RailEnd.A, new EntryPointEndpointRef(new EntryPointId(1)));
        var b = new RailEndpointLocation(new RailId(2), RailEnd.A, new EntryPointEndpointRef(new EntryPointId(2)));
        var c = new RailEndpointLocation(new RailId(3), RailEnd.A, new EntryPointEndpointRef(new EntryPointId(3)));

        var result1 = RailEndpointConvergenceResolver.AssignSwitcherPorts(new List<RailEndpointLocation> { a, b, c });
        var result2 = RailEndpointConvergenceResolver.AssignSwitcherPorts(new List<RailEndpointLocation> { c, a, b });

        Assert.Equal(
            result1.Select(r => (r.Location.RailId, r.PortIndex)),
            result2.Select(r => (r.Location.RailId, r.PortIndex)));
    }

    // ================================
    // FindBlockingStationPaths
    // ================================

    [Fact]
    public void FindBlockingStationPaths_ReturnsEmpty_WhenNoDisappearingIds()
    {
        var result = RailEndpointConvergenceResolver.FindBlockingStationPaths(
            Array.Empty<ObjectId>(), new List<StationPath>());

        Assert.Empty(result);
    }

    [Fact]
    public void FindBlockingStationPaths_FindsStationPathReferencingDisappearingBoundaryPoint()
    {
        var bpId = new BoundaryPointId(1);
        var sp = new StationPath
        {
            Id = new StationPathId(1),
            FloorUnitId = new FloorUnitId(1),
            Name = "経路1",
            Direction = StationPathDirection.Arrival,
            Waypoints = new List<StationPathWaypoint> { new BoundaryPointWaypoint(bpId) },
        };

        var result = RailEndpointConvergenceResolver.FindBlockingStationPaths(
            new ObjectId[] { new BoundaryPointObjectId(bpId) }, new List<StationPath> { sp });

        Assert.Single(result);
        Assert.Equal(sp.Id, result[0]);
    }

    [Fact]
    public void FindBlockingStationPaths_ExcludesUnrelatedStationPaths()
    {
        var bpId = new BoundaryPointId(1);
        var otherBpId = new BoundaryPointId(2);
        var sp = new StationPath
        {
            Id = new StationPathId(1),
            FloorUnitId = new FloorUnitId(1),
            Name = "経路1",
            Direction = StationPathDirection.Arrival,
            Waypoints = new List<StationPathWaypoint> { new BoundaryPointWaypoint(otherBpId) },
        };

        var result = RailEndpointConvergenceResolver.FindBlockingStationPaths(
            new ObjectId[] { new BoundaryPointObjectId(bpId) }, new List<StationPath> { sp });

        Assert.Empty(result);
    }

    [Fact]
    public void FindBlockingStationPaths_MatchesAnyWaypointInTheList_NotOnlyTheFirst()
    {
        var epId = new EntryPointId(5);
        var sp = new StationPath
        {
            Id = new StationPathId(1),
            FloorUnitId = new FloorUnitId(1),
            Name = "経路1",
            Direction = StationPathDirection.Departure,
            Waypoints = new List<StationPathWaypoint>
            {
                new BoundaryPointWaypoint(new BoundaryPointId(1)),
                new EntryPointWaypoint(epId),
            },
        };

        var result = RailEndpointConvergenceResolver.FindBlockingStationPaths(
            new ObjectId[] { new EntryPointObjectId(epId) }, new List<StationPath> { sp });

        Assert.Single(result);
    }
}
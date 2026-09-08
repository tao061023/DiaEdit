namespace DiaEditCore.Tests.Commands.Stations.FloorUnitObjects;

using System;
using System.Collections.Generic;

using DiaEditCore.Algorithm.Stations.FloorUnitObjects;
using DiaEditCore.Commands.Stations.FloorUnitObjects;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;

using Xunit;

public sealed class RailEndPointRefChangerTests
{
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

    [Fact]
    public void Constructor_Throws_WhenTargetsEmpty()
    {
        var rails = new List<Rail>();

        Assert.Throws<ArgumentException>(() =>
            new RailEndPointRefChanger(
                rails,
                Array.Empty<(RailId, RailEnd)>(),
                new NoneEndpointRef(new NoneEndpointId(1)),
                new HashSet<ObjectId>()));
    }

    [Fact]
    public void Execute_ChangesSpecifiedEndpointsOnly()
    {
        var rail1 = MakeRail(1, new EntryPointEndpointRef(new EntryPointId(1)), new BufferStopEndpointRef(new BufferStopId(1)));
        var rail2 = MakeRail(2, new BufferStopEndpointRef(new BufferStopId(2)), new EntryPointEndpointRef(new EntryPointId(2)));
        var rails = new List<Rail> { rail1, rail2 };

        var newRef = new BoundaryPointEndpointRef(new BoundaryPointId(1));
        var targets = new List<(RailId, RailEnd)> { (new RailId(1), RailEnd.A), (new RailId(2), RailEnd.B) };

        var command = new RailEndPointRefChanger(
            rails, targets, newRef,
            new HashSet<ObjectId> { new RailObjectId(new RailId(1)), new RailObjectId(new RailId(2)) });

        command.Execute();

        Assert.Equal(newRef, rail1.EndpointA);
        Assert.Equal(newRef, rail2.EndpointB);
        // 対象外の端点は変化しない
        Assert.IsType<BufferStopEndpointRef>(rail1.EndpointB);
        Assert.IsType<BufferStopEndpointRef>(rail2.EndpointA);
    }

    [Fact]
    public void Undo_RestoresOriginalEndpoints_PerTarget()
    {
        var originalA1 = new EntryPointEndpointRef(new EntryPointId(1));
        var originalB2 = new EntryPointEndpointRef(new EntryPointId(2));
        var rail1 = MakeRail(1, originalA1, new BufferStopEndpointRef(new BufferStopId(1)));
        var rail2 = MakeRail(2, new BufferStopEndpointRef(new BufferStopId(2)), originalB2);
        var rails = new List<Rail> { rail1, rail2 };

        var newRef = new BoundaryPointEndpointRef(new BoundaryPointId(1));
        var targets = new List<(RailId, RailEnd)> { (new RailId(1), RailEnd.A), (new RailId(2), RailEnd.B) };

        var command = new RailEndPointRefChanger(rails, targets, newRef, new HashSet<ObjectId>());
        command.Execute();
        command.Undo();

        Assert.Equal(originalA1, rail1.EndpointA);
        Assert.Equal(originalB2, rail2.EndpointB);
    }

    [Fact]
    public void Execute_Throws_WhenTargetRailNotFound()
    {
        var rails = new List<Rail>();
        var targets = new List<(RailId, RailEnd)> { (new RailId(99), RailEnd.A) };

        var command = new RailEndPointRefChanger(
            rails, targets, new NoneEndpointRef(new NoneEndpointId(1)), new HashSet<ObjectId>());

        Assert.Throws<InvalidOperationException>(() => command.Execute());
    }

    [Fact]
    public void AffectedIds_ReturnsConstructorSuppliedValue()
    {
        var rail = MakeRail(1, new EntryPointEndpointRef(new EntryPointId(1)), new EntryPointEndpointRef(new EntryPointId(2)));
        var rails = new List<Rail> { rail };
        var affected = new HashSet<ObjectId> { new RailObjectId(new RailId(1)) };

        var command = new RailEndPointRefChanger(
            rails,
            new List<(RailId, RailEnd)> { (new RailId(1), RailEnd.A) },
            new NoneEndpointRef(new NoneEndpointId(9)),
            affected);

        Assert.Equal(affected, command.AffectedIds);
    }

    [Fact]
    public void SingleTarget_ChangesOnlySpecifiedEnd_OfSpecifiedRail()
    {
        // SwitcherNew/SwitcherExpandケースで1本ずつ個別呼び出しされる用法（Reconcile内、
        // ports.foreach経路）を直接模した最小ケース。
        var rail = MakeRail(1, new EntryPointEndpointRef(new EntryPointId(1)), new BufferStopEndpointRef(new BufferStopId(1)));
        var rails = new List<Rail> { rail };

        var newRef = new SwitcherEndpointRef(new SwitcherId(1), PortIndex: 3);
        var command = new RailEndPointRefChanger(
            rails, new List<(RailId, RailEnd)> { (new RailId(1), RailEnd.A) }, newRef, new HashSet<ObjectId>());

        command.Execute();

        Assert.Equal(newRef, rail.EndpointA);
        Assert.IsType<BufferStopEndpointRef>(rail.EndpointB);
    }
}
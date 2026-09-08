namespace DiaEditCore.Tests.Commands.Stations.FloorUnitObjects;

using System;
using System.Collections.Generic;
using System.Linq;

using DiaEditCore.Commands.Stations.FloorUnitObjects;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;

using Xunit;

public sealed class ChangeSwitcherAttributesCommandTests
{
    private static Switcher MakeSwitcher() => new()
    {
        Id = new SwitcherId(1),
        Base = new FloorUnitObjectBase { FloorUnitId = new FloorUnitId(1), Position = new Point(0, 0) },
        PortCount = 3,
        Mechanism = new SwitchMechanism { RootPortIndex = 0, NormalPortIndex = 1, ReversePortIndex = 2 },
        ValidRoutes = new List<PortPair> { new(0, 1) },
    };

    [Fact]
    public void Execute_AppliesAllThreeFields()
    {
        var switcher = MakeSwitcher();
        var list = new List<Switcher> { switcher };
        var newRoutes = new List<PortPair> { new(0, 2), new(1, 3) };

        var command = new ChangeSwitcherAttributesCommand(
            list, switcher.Id,
            newPortCount: 4,
            newMechanism: null,
            newValidRoutes: newRoutes,
            affectedIds: new HashSet<ObjectId>());

        command.Execute();

        Assert.Equal(4, switcher.PortCount);
        Assert.Null(switcher.Mechanism);
        Assert.Equal(newRoutes, switcher.ValidRoutes);
    }

    [Fact]
    public void Undo_RestoresOriginalValues()
    {
        var switcher = MakeSwitcher();
        var originalMechanism = switcher.Mechanism;
        var originalRoutes = switcher.ValidRoutes.ToList();
        var list = new List<Switcher> { switcher };

        var command = new ChangeSwitcherAttributesCommand(
            list, switcher.Id,
            newPortCount: 4,
            newMechanism: null,
            newValidRoutes: Array.Empty<PortPair>(),
            affectedIds: new HashSet<ObjectId>());

        command.Execute();
        command.Undo();

        Assert.Equal(3, switcher.PortCount);
        Assert.Equal(originalMechanism, switcher.Mechanism);
        Assert.Equal(originalRoutes, switcher.ValidRoutes);
    }

    [Fact]
    public void Execute_DefensivelyCopiesValidRoutes_CallerListMutationDoesNotLeak()
    {
        var switcher = MakeSwitcher();
        var list = new List<Switcher> { switcher };
        var callerRoutes = new List<PortPair> { new(0, 1) };

        var command = new ChangeSwitcherAttributesCommand(
            list, switcher.Id,
            newPortCount: 2,
            newMechanism: null,
            newValidRoutes: callerRoutes,
            affectedIds: new HashSet<ObjectId>());

        command.Execute();
        callerRoutes.Add(new PortPair(5, 6)); // 呼び出し元リストを事後変更

        Assert.Single(switcher.ValidRoutes); // Switcher側は影響を受けない
    }

    [Fact]
    public void Undo_RestoresValidRoutesAsIndependentCopy()
    {
        var switcher = MakeSwitcher();
        var originalRoutesRef = switcher.ValidRoutes;
        var list = new List<Switcher> { switcher };

        var command = new ChangeSwitcherAttributesCommand(
            list, switcher.Id,
            newPortCount: 5,
            newMechanism: null,
            newValidRoutes: new List<PortPair> { new(2, 3) },
            affectedIds: new HashSet<ObjectId>());

        command.Execute();
        command.Undo();

        Assert.Equal(originalRoutesRef, switcher.ValidRoutes); // 内容は一致
        Assert.NotSame(originalRoutesRef, switcher.ValidRoutes); // だが別インスタンス（CaptureSnapshotでToArray済み）
    }

    [Fact]
    public void Throws_WhenSwitcherNotFound()
    {
        var list = new List<Switcher>();

        var command = new ChangeSwitcherAttributesCommand(
            list, new SwitcherId(99),
            newPortCount: 2,
            newMechanism: null,
            newValidRoutes: Array.Empty<PortPair>(),
            affectedIds: new HashSet<ObjectId>());

        Assert.Throws<InvalidOperationException>(() => command.Execute());
    }

    [Fact]
    public void AffectedIds_ReturnsConstructorSuppliedValue()
    {
        var switcher = MakeSwitcher();
        var list = new List<Switcher> { switcher };
        var affected = new HashSet<ObjectId> { new SwitcherObjectId(switcher.Id) };

        var command = new ChangeSwitcherAttributesCommand(
            list, switcher.Id, 3, null, Array.Empty<PortPair>(), affected);

        Assert.Equal(affected, command.AffectedIds);
    }
}
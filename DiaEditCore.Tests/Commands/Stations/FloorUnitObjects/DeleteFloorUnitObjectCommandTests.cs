namespace DiaEditCore.Tests.Commands.Stations.FloorUnitObjects;

using System.Collections.Generic;

using DiaEditCore.Commands.Stations.FloorUnitObjects;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;

using Xunit;

public sealed class DeleteFloorUnitObjectCommandTests
{
    private static FloorUnitObjectBase MakeBase() =>
        new() { FloorUnitId = new FloorUnitId(1), Position = new Point(0, 0) };

    [Fact]
    public void Execute_RemovesTargetFromList_LeavesOthersIntact()
    {
        var target = new NoneEndpoint { Id = new NoneEndpointId(1), Base = MakeBase() };
        var other = new NoneEndpoint { Id = new NoneEndpointId(2), Base = MakeBase() };
        var list = new List<NoneEndpoint> { target, other };

        var command = new DeleteFloorUnitObjectCommand<NoneEndpointId, NoneEndpoint>(
            list, target, new HashSet<ObjectId> { new NoneEndpointObjectId(target.Id) });

        command.Execute();

        Assert.Single(list);
        Assert.Same(other, list[0]);
    }

    [Fact]
    public void Undo_RestoresDeletedObject_AsSameInstance()
    {
        var target = new BoundaryPoint { Id = new BoundaryPointId(1), Base = MakeBase() };
        var list = new List<BoundaryPoint> { target };

        var command = new DeleteFloorUnitObjectCommand<BoundaryPointId, BoundaryPoint>(
            list, target, new HashSet<ObjectId>());

        command.Execute();
        Assert.Empty(list);

        command.Undo();
        Assert.Single(list);
        Assert.Same(target, list[0]);
    }

    [Fact]
    public void AffectedIds_ReturnsConstructorSuppliedValue()
    {
        var target = new Switcher { Id = new SwitcherId(1), Base = MakeBase(), PortCount = 3 };
        var list = new List<Switcher> { target };
        var affected = new HashSet<ObjectId> { new SwitcherObjectId(target.Id) };

        var command = new DeleteFloorUnitObjectCommand<SwitcherId, Switcher>(list, target, affected);

        Assert.Equal(affected, command.AffectedIds);
    }

    [Fact]
    public void ExecuteUndoExecute_IsIdempotentAcrossCycles()
    {
        // CaptureSnapshotが_toDeleteをそのまま返す単純設計であるため、
        // Execute→Undo→Executeの2回目でも同一インスタンスの参照同一性が保たれることを確認する。
        var target = new EntryPoint { Id = new EntryPointId(1), Base = MakeBase(), Type = EntryPointType.Both };
        var list = new List<EntryPoint> { target };

        var command = new DeleteFloorUnitObjectCommand<EntryPointId, EntryPoint>(list, target, new HashSet<ObjectId>());

        command.Execute();
        command.Undo();
        command.Execute();

        Assert.Empty(list);

        command.Undo();
        Assert.Single(list);
        Assert.Same(target, list[0]);
    }

    [Fact]
    public void Execute_DoesNotThrow_WhenAffectedIdsIsEmpty()
    {
        // 無条件削除であり参照チェックを行わない設計のため、AffectedIdsが空でも問題なく実行できる。
        var target = new BufferStop { Id = new BufferStopId(1), Base = MakeBase() };
        var list = new List<BufferStop> { target };

        var command = new DeleteFloorUnitObjectCommand<BufferStopId, BufferStop>(
            list, target, new HashSet<ObjectId>());

        var exception = Record.Exception(() => command.Execute());
        Assert.Null(exception);
    }
}
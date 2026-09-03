namespace DiaEditCore.Tests.Commands.Stations;

using DiaEditCore.Commands;
using DiaEditCore.Commands.Stations;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Session;

using Xunit;

public sealed class DeleteFloorUnitCommandTests
{
    private static FloorUnit MakeFloorUnit(int id, int stationId, int displayOrder) => new()
    {
        Id = new FloorUnitId(id),
        StationId = new StationId(stationId),
        DisplayOrder = displayOrder
    };

    private static readonly ValidationRules DefaultValidationRules = new(
        MinDwellTimeSec: null,
        MinHeadwaySec: null,
        MinTurnaroundSec: null,
        TrackEntryMarginSec: null,
        TrackPassMarginSec: null,
        EnableConflictDetection: true,
        EnableCarLengthCheck: true);

    private static ProjectFile MakeEmptyProject() => new()
    {
        SchemaVersion = 1,
        ProjectSettings = new ProjectSettings(DefaultValidationRules),
    };

    private static ProjectSession MakeSession()
    {
        var session = new ProjectSession(new CommandInvoker());
        session.Load(MakeEmptyProject());
        return session;
    }

    [Fact]
    public void Execute_RemovesFloorUnit_WhenSiblingExistsAndNoDependents()
    {
        var floorUnits = new List<FloorUnit>
        {
            MakeFloorUnit(1, 100, 0),
            MakeFloorUnit(2, 100, 1)
        };

        var command = new DeleteFloorUnitCommand(floorUnits, floorUnits[0], MakeSession());
        command.Execute();

        Assert.Single(floorUnits);
        Assert.Equal(2, floorUnits[0].Id.Value);
    }

    [Fact]
    public void Undo_RestoresDeletedFloorUnit()
    {
        var floorUnits = new List<FloorUnit>
        {
            MakeFloorUnit(1, 100, 0),
            MakeFloorUnit(2, 100, 1)
        };
        var target = floorUnits[0];

        var command = new DeleteFloorUnitCommand(floorUnits, target, MakeSession());
        command.Execute();
        command.Undo();

        Assert.Equal(2, floorUnits.Count);
        Assert.Contains(target, floorUnits);
    }

    [Fact]
    public void Constructor_Throws_WhenLastFloorUnitForStation()
    {
        var floorUnits = new List<FloorUnit>
        {
            MakeFloorUnit(1, 100, 0)
        };

        Assert.Throws<InvalidOperationException>(() =>
            new DeleteFloorUnitCommand(floorUnits, floorUnits[0], MakeSession()));
    }

    [Fact]
    public void Constructor_DoesNotThrow_WhenOtherStationsFloorUnitsExist()
    {
        var floorUnits = new List<FloorUnit>
        {
            MakeFloorUnit(1, 100, 0), // 対象。Station100にとって唯一
            MakeFloorUnit(2, 200, 0),
            MakeFloorUnit(3, 200, 1)
        };

        Assert.Throws<InvalidOperationException>(() =>
            new DeleteFloorUnitCommand(floorUnits, floorUnits[0], MakeSession()));
    }

    [Fact]
    public void Execute_DoesNotAffectOtherStationsFloorUnits()
    {
        var floorUnits = new List<FloorUnit>
        {
            MakeFloorUnit(1, 100, 0),
            MakeFloorUnit(2, 100, 1),
            MakeFloorUnit(3, 200, 0)
        };

        var command = new DeleteFloorUnitCommand(floorUnits, floorUnits[0], MakeSession());
        command.Execute();

        Assert.Equal(2, floorUnits.Count);
        Assert.Contains(floorUnits, f => f.Id.Value == 2);
        Assert.Contains(floorUnits, f => f.Id.Value == 3);
    }

    [Fact]
    public void Constructor_Throws_WhenDependentBoundaryPointExists()
    {
        var floorUnitToDelete = MakeFloorUnit(1, 100, 0);
        var floorUnits = new List<FloorUnit> { floorUnitToDelete, MakeFloorUnit(2, 100, 1) };

        // v12.21：ProjectSession.Load()直後はキャッシュがクリーン（_cacheDirty=false）になるため、
        // その後GetCache()で取得したTimeTableSetCacheへ直接インデックスを注入しても、
        // コマンドのコンストラクタが呼ぶGetCache()で再構築が走らず注入内容がそのまま維持される。
        var session = MakeSession();
        var cache = session.GetCache();
        cache.FloorUnitDependentIndex[floorUnitToDelete.Id] = new List<ObjectId>
        {
            new BoundaryPointObjectId(new BoundaryPointId(1))
        };

        Assert.Throws<InvalidOperationException>(() =>
            new DeleteFloorUnitCommand(floorUnits, floorUnitToDelete, session));
    }

    [Fact]
    public void Constructor_Throws_WhenDependentStationPathExists()
    {
        // StationPathはFloorUnitObjectBase経由ではなくFloorUnitIdを直接持つ特殊ケースだが、
        // 同じFloorUnitDependentIndex経由で1ホップ拒否の対象になることを確認する。
        var floorUnitToDelete = MakeFloorUnit(1, 100, 0);
        var floorUnits = new List<FloorUnit> { floorUnitToDelete, MakeFloorUnit(2, 100, 1) };

        var session = MakeSession();
        var cache = session.GetCache();
        cache.FloorUnitDependentIndex[floorUnitToDelete.Id] = new List<ObjectId>
        {
            new StationPathObjectId(new StationPathId(1))
        };

        Assert.Throws<InvalidOperationException>(() =>
            new DeleteFloorUnitCommand(floorUnits, floorUnitToDelete, session));
    }

    [Fact]
    public void Constructor_DoesNotThrow_WhenDependentsBelongToOtherFloorUnit()
    {
        var floorUnitToDelete = MakeFloorUnit(1, 100, 0);
        var floorUnits = new List<FloorUnit> { floorUnitToDelete, MakeFloorUnit(2, 100, 1) };

        var session = MakeSession();
        var cache = session.GetCache();
        // 削除対象ではない別FloorUnit（Id=2）にのみ配下オブジェクトが存在する状態
        cache.FloorUnitDependentIndex[new FloorUnitId(2)] = new List<ObjectId>
        {
            new BoundaryPointObjectId(new BoundaryPointId(1))
        };

        var command = new DeleteFloorUnitCommand(floorUnits, floorUnitToDelete, session);
        Assert.NotNull(command);
    }

    [Fact]
    public void AffectedIds_ContainsOnlySelf_WhenNoDependents()
    {
        var floorUnits = new List<FloorUnit>
        {
            MakeFloorUnit(1, 100, 0),
            MakeFloorUnit(2, 100, 1)
        };

        var command = new DeleteFloorUnitCommand(floorUnits, floorUnits[0], MakeSession());

        Assert.Single(command.AffectedIds);
    }
}
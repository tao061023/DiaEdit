namespace DiaEditCore.Tests.Commands.Stations;

using DiaEditCore.Commands;
using DiaEditCore.Commands.Stations;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Session;
using Xunit;

public sealed class DeleteFloorUnitCommandTests
{
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

    private static FloorUnit MakeFloorUnit(int id, int stationId, int displayOrder) => new()
    {
        Id = new FloorUnitId(id),
        StationId = new StationId(stationId),
        DisplayOrder = displayOrder
    };

    private static FloorUnitObjectBase MakeBase(int floorUnitId) => new()
    {
        FloorUnitId = new FloorUnitId(floorUnitId),
        Position = new Point(0, 0)
    };

    // v12.21：TimeTableSetCacheを直接newする方式からProjectSession経由へ移行（§9.1項目5）。
    // 旧版は cache.FloorUnitDependentIndex[...] = ... のようにキャッシュへ直接値を注入できたが、
    // ProjectSession経由では_cacheが非公開のためこの手法は使えない。代わりに実際のモデル
    // オブジェクト（BoundaryPoint／StationPath）をProjectFileへ追加してLoad()し、
    // FloorUnitDependentIndexBuilderに実際にインデックスを構築させる形に統一する。
    private static ProjectSession MakeSession(ProjectFile project)
    {
        var session = new ProjectSession(new CommandInvoker());
        session.Load(project);
        return session;
    }

    private static ProjectSession EmptySession() => MakeSession(MakeEmptyProject());

    [Fact]
    public void Execute_RemovesFloorUnit_WhenSiblingExistsAndNoDependents()
    {
        var floorUnits = new List<FloorUnit>
        {
            MakeFloorUnit(1, 100, 0),
            MakeFloorUnit(2, 100, 1)
        };

        var command = new DeleteFloorUnitCommand(floorUnits, floorUnits[0], EmptySession());
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

        var command = new DeleteFloorUnitCommand(floorUnits, target, EmptySession());
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
            new DeleteFloorUnitCommand(floorUnits, floorUnits[0], EmptySession()));
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
            new DeleteFloorUnitCommand(floorUnits, floorUnits[0], EmptySession()));
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

        var command = new DeleteFloorUnitCommand(floorUnits, floorUnits[0], EmptySession());
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

        var project = MakeEmptyProject();
        project.BoundaryPoints.Add(new BoundaryPoint
        {
            Id = new BoundaryPointId(1),
            Base = MakeBase(floorUnitToDelete.Id.Value)
        });

        Assert.Throws<InvalidOperationException>(() =>
            new DeleteFloorUnitCommand(floorUnits, floorUnitToDelete, MakeSession(project)));
    }

    [Fact]
    public void Constructor_Throws_WhenDependentStationPathExists()
    {
        // StationPathはFloorUnitObjectBase経由ではなくFloorUnitIdを直接持つ特殊ケースだが、
        // 同じFloorUnitDependentIndex経由で1ホップ拒否の対象になることを確認する。
        var floorUnitToDelete = MakeFloorUnit(1, 100, 0);
        var floorUnits = new List<FloorUnit> { floorUnitToDelete, MakeFloorUnit(2, 100, 1) };

        var project = MakeEmptyProject();
        project.StationPaths.Add(new StationPath
        {
            Id = new StationPathId(1),
            FloorUnitId = floorUnitToDelete.Id,
            Name = "test",
            Direction = StationPathDirection.Arrival,
            Waypoints = new List<StationPathWaypoint>()
        });

        Assert.Throws<InvalidOperationException>(() =>
            new DeleteFloorUnitCommand(floorUnits, floorUnitToDelete, MakeSession(project)));
    }

    [Fact]
    public void Constructor_DoesNotThrow_WhenDependentsBelongToOtherFloorUnit()
    {
        var floorUnitToDelete = MakeFloorUnit(1, 100, 0);
        var floorUnits = new List<FloorUnit> { floorUnitToDelete, MakeFloorUnit(2, 100, 1) };

        var project = MakeEmptyProject();
        // 削除対象ではない別FloorUnit（Id=2）にのみ配下オブジェクトが存在する状態
        project.BoundaryPoints.Add(new BoundaryPoint
        {
            Id = new BoundaryPointId(1),
            Base = MakeBase(2)
        });

        var command = new DeleteFloorUnitCommand(floorUnits, floorUnitToDelete, MakeSession(project));
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

        var command = new DeleteFloorUnitCommand(floorUnits, floorUnits[0], EmptySession());

        Assert.Single(command.AffectedIds);
    }
}
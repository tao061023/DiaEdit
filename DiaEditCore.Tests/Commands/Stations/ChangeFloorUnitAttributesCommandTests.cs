namespace DiaEditCore.Tests.Commands.Stations;

using DiaEditCore.Commands;
using DiaEditCore.Commands.Stations;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Session;

using Xunit;

public sealed class ChangeFloorUnitAttributesCommandTests
{
    private static FloorUnit MakeFloorUnit() => new()
    {
        Id = new FloorUnitId(1),
        StationId = new StationId(1),
        Name = "旧名称",
        DisplayOrder = 0
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
    public void Execute_ChangesName()
    {
        var floorUnit = MakeFloorUnit();
        var command = new ChangeFloorUnitAttributesCommand(floorUnit, new FloorUnitSnapshot("新名称"), MakeSession());

        command.Execute();

        Assert.Equal("新名称", floorUnit.Name);
    }

    [Fact]
    public void Execute_DoesNotAffectDisplayOrder()
    {
        var floorUnit = MakeFloorUnit();
        var command = new ChangeFloorUnitAttributesCommand(floorUnit, new FloorUnitSnapshot("新名称"), MakeSession());

        command.Execute();

        Assert.Equal(0, floorUnit.DisplayOrder);
    }

    [Fact]
    public void Undo_RestoresOriginalName()
    {
        var floorUnit = MakeFloorUnit();
        var command = new ChangeFloorUnitAttributesCommand(floorUnit, new FloorUnitSnapshot("新名称"), MakeSession());

        command.Execute();
        command.Undo();

        Assert.Equal("旧名称", floorUnit.Name);
    }

    [Fact]
    public void AffectedIds_ContainsOnlySelf_WhenCacheIsEmpty()
    {
        var floorUnit = MakeFloorUnit();
        var command = new ChangeFloorUnitAttributesCommand(floorUnit, new FloorUnitSnapshot("新名称"), MakeSession());

        Assert.Single(command.AffectedIds);
    }
}
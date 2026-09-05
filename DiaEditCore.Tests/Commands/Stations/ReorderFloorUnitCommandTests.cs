namespace DiaEditCore.Tests.Commands.Stations;

using System.Collections.Generic;
using System.Linq;

using DiaEditCore.Commands;
using DiaEditCore.Commands.Stations;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Session;

using Xunit;

public sealed class ReorderFloorUnitsCommandTests
{
    private static List<FloorUnit> MakeThreeFloorUnits(StationId stationId) => new()
    {
        new FloorUnit { Id = new FloorUnitId(1), StationId = stationId, Name = "1F", DisplayOrder = 0 },
        new FloorUnit { Id = new FloorUnitId(2), StationId = stationId, Name = "2F", DisplayOrder = 1 },
        new FloorUnit { Id = new FloorUnitId(3), StationId = stationId, Name = "3F", DisplayOrder = 2 },
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
    public void Execute_AssignsDisplayOrderAccordingToNewOrder()
    {
        var stationId = new StationId(1);
        var floorUnits = MakeThreeFloorUnits(stationId);
        var newOrder = new List<FloorUnitId> { new(3), new(1), new(2) };

        var command = new ReorderFloorUnitsCommand(floorUnits, stationId, newOrder, MakeSession());
        command.Execute();

        Assert.Equal(0, floorUnits.Single(f => f.Id == new FloorUnitId(3)).DisplayOrder);
        Assert.Equal(1, floorUnits.Single(f => f.Id == new FloorUnitId(1)).DisplayOrder);
        Assert.Equal(2, floorUnits.Single(f => f.Id == new FloorUnitId(2)).DisplayOrder);
    }

    [Fact]
    public void Undo_RestoresOriginalDisplayOrder()
    {
        var stationId = new StationId(1);
        var floorUnits = MakeThreeFloorUnits(stationId);
        var newOrder = new List<FloorUnitId> { new(3), new(1), new(2) };

        var command = new ReorderFloorUnitsCommand(floorUnits, stationId, newOrder, MakeSession());
        command.Execute();
        command.Undo();

        Assert.Equal(0, floorUnits.Single(f => f.Id == new FloorUnitId(1)).DisplayOrder);
        Assert.Equal(1, floorUnits.Single(f => f.Id == new FloorUnitId(2)).DisplayOrder);
        Assert.Equal(2, floorUnits.Single(f => f.Id == new FloorUnitId(3)).DisplayOrder);
    }

    [Fact]
    public void Constructor_Throws_WhenNewOrderIsMissingAnExistingFloorUnit()
    {
        var stationId = new StationId(1);
        var floorUnits = MakeThreeFloorUnits(stationId);
        var incompleteOrder = new List<FloorUnitId> { new(1), new(2) }; // Id=3が抜けている

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ReorderFloorUnitsCommand(floorUnits, stationId, incompleteOrder, MakeSession()));

        Assert.Contains("過不足", ex.Message);
    }

    [Fact]
    public void Constructor_Throws_WhenNewOrderContainsIdFromAnotherStation()
    {
        var stationId = new StationId(1);
        var floorUnits = MakeThreeFloorUnits(stationId);
        floorUnits.Add(new FloorUnit { Id = new FloorUnitId(99), StationId = new StationId(2), Name = "他駅", DisplayOrder = 0 });

        var invalidOrder = new List<FloorUnitId> { new(1), new(2), new(99) }; // 他StationのFloorUnitを混入

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ReorderFloorUnitsCommand(floorUnits, stationId, invalidOrder, MakeSession()));

        Assert.Contains("過不足", ex.Message);
    }

    [Fact]
    public void Execute_DoesNotAffectFloorUnitsOfOtherStations()
    {
        var stationId = new StationId(1);
        var floorUnits = MakeThreeFloorUnits(stationId);
        var otherStationFloorUnit = new FloorUnit { Id = new FloorUnitId(99), StationId = new StationId(2), Name = "他駅", DisplayOrder = 5 };
        floorUnits.Add(otherStationFloorUnit);

        var newOrder = new List<FloorUnitId> { new(3), new(1), new(2) };
        var command = new ReorderFloorUnitsCommand(floorUnits, stationId, newOrder, MakeSession());
        command.Execute();

        Assert.Equal(5, otherStationFloorUnit.DisplayOrder);
    }

    [Fact]
    public void AffectedIds_ContainsAllReorderedFloorUnits()
    {
        var stationId = new StationId(1);
        var floorUnits = MakeThreeFloorUnits(stationId);
        var newOrder = new List<FloorUnitId> { new(3), new(1), new(2) };

        var command = new ReorderFloorUnitsCommand(floorUnits, stationId, newOrder, MakeSession());

        Assert.Equal(3, command.AffectedIds.Count);
    }
}
namespace DiaEditCore.Tests.Commands;

using System.Linq;
using DiaEditCore.Commands;
using DiaEditCore.Commands.Stations;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Session;
using Xunit;

public sealed class TransactionCommandTests
{
    // ...RecordingCommand・Execute_RunsAllFactoriesInOrder・Undo_RunsInReverseOrder・
    //    Undo_BeforeExecute_Throws・Constructor_EmptyFactories_Throwsは変更なし...

    [Fact]
    public void LaterFactory_CanDependOnEarlierCommandResult()
    {
        // CreateFloorUnitCommandがCreateStationCommand.Created.Idに依存するケースの直接的な検証。
        var stations = new List<Station>();
        var floorUnits = new List<FloorUnit>();
        var stationIdAllocator = new IdAllocator<StationId>(v => new StationId(v), stations.Select(s => s.Id.Value));
        var floorUnitIdAllocator = new IdAllocator<FloorUnitId>(v => new FloorUnitId(v), floorUnits.Select(f => f.Id.Value));
        var displayName = new DisplayName { Name = "テスト駅" };

        var createStation = new CreateStationCommand(stations, stationIdAllocator, displayName, StationType.Standard);
        var transaction = new TransactionCommand(new List<Func<IUndoableCommand>>
        {
            () => createStation,
            () => new CreateFloorUnitCommand(floorUnits, floorUnitIdAllocator, createStation.Created!.Id)
        });

        transaction.Execute();

        Assert.Single(stations);
        Assert.Single(floorUnits);
        Assert.Equal(stations[0].Id, floorUnits[0].StationId);
    }
}

public sealed class CreateFloorUnitCommandTests
{
    [Fact]
    public void Execute_AddsFloorUnitToList_WithAllocatedId()
    {
        var floorUnits = new List<FloorUnit>();
        var idAllocator = new IdAllocator<FloorUnitId>(v => new FloorUnitId(v), floorUnits.Select(f => f.Id.Value));
        var stationId = new StationId(1);

        var command = new CreateFloorUnitCommand(floorUnits, idAllocator, stationId, "1階", 0);
        command.Execute();

        Assert.Single(floorUnits);
        Assert.Equal(1, floorUnits[0].Id.Value);
        Assert.Equal(stationId, floorUnits[0].StationId);
        Assert.Equal("1階", floorUnits[0].Name);
        Assert.Equal(0, floorUnits[0].DisplayOrder);
    }

    [Fact]
    public void Undo_RemovesCreatedFloorUnit()
    {
        var floorUnits = new List<FloorUnit>();
        var idAllocator = new IdAllocator<FloorUnitId>(v => new FloorUnitId(v), floorUnits.Select(f => f.Id.Value));
        var stationId = new StationId(1);

        var command = new CreateFloorUnitCommand(floorUnits, idAllocator, stationId);
        command.Execute();
        command.Undo();

        Assert.Empty(floorUnits);
    }

    [Fact]
    public void AffectedIds_IsEmpty()
    {
        var floorUnits = new List<FloorUnit>();
        var idAllocator = new IdAllocator<FloorUnitId>(v => new FloorUnitId(v), floorUnits.Select(f => f.Id.Value));
        var command = new CreateFloorUnitCommand(floorUnits, idAllocator, new StationId(1));

        Assert.Empty(command.AffectedIds);
    }
}

public sealed class StationCreationWorkflowTests
{
    [Fact]
    public void CreateStationWithDefaultFloorUnit_CreatesBothOnExecute()
    {
        var stations = new List<Station>();
        var floorUnits = new List<FloorUnit>();
        var stationIdAllocator = new IdAllocator<StationId>(v => new StationId(v), stations.Select(s => s.Id.Value));
        var floorUnitIdAllocator = new IdAllocator<FloorUnitId>(v => new FloorUnitId(v), floorUnits.Select(f => f.Id.Value));
        var displayName = new DisplayName { Name = "新駅" };

        var workflow = StationCreationWorkflow.CreateStationWithDefaultFloorUnit(
            stations, floorUnits, stationIdAllocator, floorUnitIdAllocator, displayName, StationType.Standard, "OP1", "ﾂ1");
        workflow.Execute();

        Assert.Single(stations);
        Assert.Single(floorUnits);
        Assert.Equal(stations[0].Id, floorUnits[0].StationId);
    }

    [Fact]
    public void CreateStationWithDefaultFloorUnit_Undo_RemovesBoth()
    {
        var stations = new List<Station>();
        var floorUnits = new List<FloorUnit>();
        var stationIdAllocator = new IdAllocator<StationId>(v => new StationId(v), stations.Select(s => s.Id.Value));
        var floorUnitIdAllocator = new IdAllocator<FloorUnitId>(v => new FloorUnitId(v), floorUnits.Select(f => f.Id.Value));
        var displayName = new DisplayName { Name = "新駅" };

        var workflow = StationCreationWorkflow.CreateStationWithDefaultFloorUnit(
            stations, floorUnits, stationIdAllocator, floorUnitIdAllocator, displayName, StationType.Standard);
        workflow.Execute();
        workflow.Undo();

        Assert.Empty(stations);
        Assert.Empty(floorUnits);
    }

    [Fact]
    public void CreateStationWithDefaultFloorUnit_NeverLeavesStationWithoutFloorUnit()
    {
        var stations = new List<Station>();
        var floorUnits = new List<FloorUnit>();
        var stationIdAllocator = new IdAllocator<StationId>(v => new StationId(v), stations.Select(s => s.Id.Value));
        var floorUnitIdAllocator = new IdAllocator<FloorUnitId>(v => new FloorUnitId(v), floorUnits.Select(f => f.Id.Value));
        var displayName = new DisplayName { Name = "新駅" };

        var workflow = StationCreationWorkflow.CreateStationWithDefaultFloorUnit(
            stations, floorUnits, stationIdAllocator, floorUnitIdAllocator, displayName, StationType.Standard);
        workflow.Execute();

        Assert.Equal(stations.Count, floorUnits.Select(f => f.StationId).Distinct().Count());
    }

    [Fact]
    public void CreateStationWithDefaultFloorUnit_UndoThenRedo_ReusesameFloorUnitInstance()
    {
        var stations = new List<Station>();
        var floorUnits = new List<FloorUnit>();
        var stationIdAllocator = new IdAllocator<StationId>(v => new StationId(v), stations.Select(s => s.Id.Value));
        var floorUnitIdAllocator = new IdAllocator<FloorUnitId>(v => new FloorUnitId(v), floorUnits.Select(f => f.Id.Value));
        var displayName = new DisplayName { Name = "新駅" };

        var workflow = StationCreationWorkflow.CreateStationWithDefaultFloorUnit(
            stations, floorUnits, stationIdAllocator, floorUnitIdAllocator, displayName, StationType.Standard);

        workflow.Execute();
        var firstFloorUnit = floorUnits.Single();

        workflow.Undo();
        Assert.Empty(floorUnits);

        workflow.Execute(); // CommandInvoker.Redo()と同じパス

        Assert.Single(floorUnits);
        Assert.Same(firstFloorUnit, floorUnits[0]); // 参照同一性
        Assert.Equal(firstFloorUnit.Id, floorUnits[0].Id); // Idも不変
    }

    [Fact]
    public void Undo後に別ワークフローで再作成してもStationとFloorUnitのIdが重複しない()
    {
        // §9.2項目27の統合回帰テスト：TransactionCommand経由の複合生成でも
        // Undo後の別インスタンスによる再作成でId重複が起きないことを確認する。
        var stations = new List<Station>();
        var floorUnits = new List<FloorUnit>();
        var stationIdAllocator = new IdAllocator<StationId>(v => new StationId(v), stations.Select(s => s.Id.Value));
        var floorUnitIdAllocator = new IdAllocator<FloorUnitId>(v => new FloorUnitId(v), floorUnits.Select(f => f.Id.Value));

        var first = StationCreationWorkflow.CreateStationWithDefaultFloorUnit(
            stations, floorUnits, stationIdAllocator, floorUnitIdAllocator,
            new DisplayName { Name = "駅A" }, StationType.Standard);
        first.Execute();
        var firstStationId = stations[0].Id;
        var firstFloorUnitId = floorUnits[0].Id;
        first.Undo();

        var second = StationCreationWorkflow.CreateStationWithDefaultFloorUnit(
            stations, floorUnits, stationIdAllocator, floorUnitIdAllocator,
            new DisplayName { Name = "駅B" }, StationType.Standard);
        second.Execute();

        Assert.NotEqual(firstStationId, stations[0].Id);
        Assert.NotEqual(firstFloorUnitId, floorUnits[0].Id);
    }
}
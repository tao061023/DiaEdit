namespace DiaEditCore.Tests.Commands;

using DiaEditCore.Commands;
using DiaEditCore.Commands.Stations;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using Xunit;

public sealed class TransactionCommandTests
{
    private sealed class RecordingCommand : IUndoableCommand
    {
        public bool Executed { get; private set; }
        public bool UndoCalled { get; private set; }
        public int ExecuteOrder { get; set; }
        private readonly List<int> _log;

        public RecordingCommand(List<int> log, int id)
        {
            _log = log;
            ExecuteOrder = id;
        }

        public IReadOnlySet<ObjectId> Execute()
        {
            Executed = true;
            _log.Add(ExecuteOrder);
            return new HashSet<ObjectId>();
        }

        public IReadOnlySet<ObjectId> Undo()
        {
            UndoCalled = true;
            _log.Add(-ExecuteOrder);
            return new HashSet<ObjectId>();
        }
    }

    [Fact]
    public void Execute_RunsAllFactoriesInOrder()
    {
        var log = new List<int>();
        var transaction = new TransactionCommand(new List<Func<IUndoableCommand>>
        {
            () => new RecordingCommand(log, 1),
            () => new RecordingCommand(log, 2),
            () => new RecordingCommand(log, 3)
        });

        transaction.Execute();

        Assert.Equal(new[] { 1, 2, 3 }, log);
    }

    [Fact]
    public void Undo_RunsInReverseOrder()
    {
        var log = new List<int>();
        var transaction = new TransactionCommand(new List<Func<IUndoableCommand>>
        {
            () => new RecordingCommand(log, 1),
            () => new RecordingCommand(log, 2),
            () => new RecordingCommand(log, 3)
        });

        transaction.Execute();
        log.Clear();
        transaction.Undo();

        Assert.Equal(new[] { -3, -2, -1 }, log);
    }

    [Fact]
    public void Undo_BeforeExecute_Throws()
    {
        var transaction = new TransactionCommand(new List<Func<IUndoableCommand>>
        {
            () => new RecordingCommand(new List<int>(), 1)
        });

        Assert.Throws<InvalidOperationException>(() => transaction.Undo());
    }

    [Fact]
    public void Constructor_EmptyFactories_Throws()
    {
        Assert.Throws<ArgumentException>(() => new TransactionCommand(new List<Func<IUndoableCommand>>()));
    }

    [Fact]
    public void LaterFactory_CanDependOnEarlierCommandResult()
    {
        // CreateFloorUnitCommandがCreateStationCommand.Created.Idに依存するケースの直接的な検証。
        var stations = new List<Station>();
        var floorUnits = new List<FloorUnit>();
        var displayName = new DisplayName { Name = "テスト駅" };

        var createStation = new CreateStationCommand(stations, displayName, StationType.Standard);
        var transaction = new TransactionCommand(new List<Func<IUndoableCommand>>
        {
            () => createStation,
            () => new CreateFloorUnitCommand(floorUnits, createStation.Created!.Id)
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
        var stationId = new StationId(1);

        var command = new CreateFloorUnitCommand(floorUnits, stationId, "1階", 0);
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
        var stationId = new StationId(1);

        var command = new CreateFloorUnitCommand(floorUnits, stationId);
        command.Execute();
        command.Undo();

        Assert.Empty(floorUnits);
    }

    [Fact]
    public void AffectedIds_IsEmpty()
    {
        var floorUnits = new List<FloorUnit>();
        var command = new CreateFloorUnitCommand(floorUnits, new StationId(1));

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
        var displayName = new DisplayName { Name = "新駅" };

        var workflow = StationCreationWorkflow.CreateStationWithDefaultFloorUnit(
            stations, floorUnits, displayName, StationType.Standard, "OP1", "ﾂ1");
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
        var displayName = new DisplayName { Name = "新駅" };

        var workflow = StationCreationWorkflow.CreateStationWithDefaultFloorUnit(
            stations, floorUnits, displayName, StationType.Standard);
        workflow.Execute();
        workflow.Undo();

        Assert.Empty(stations);
        Assert.Empty(floorUnits);
    }

    [Fact]
    public void CreateStationWithDefaultFloorUnit_NeverLeavesStationWithoutFloorUnit()
    {
        // n≥1制約（4.2節）を意識した検証：Execute()完了後は必ずStation・FloorUnit両方が
        // 揃っている状態になっていること（片方だけ存在する中間状態が外部から観測できないこと）。
        var stations = new List<Station>();
        var floorUnits = new List<FloorUnit>();
        var displayName = new DisplayName { Name = "新駅" };

        var workflow = StationCreationWorkflow.CreateStationWithDefaultFloorUnit(
            stations, floorUnits, displayName, StationType.Standard);
        workflow.Execute();

        Assert.Equal(stations.Count, floorUnits.Select(f => f.StationId).Distinct().Count());
    }
}
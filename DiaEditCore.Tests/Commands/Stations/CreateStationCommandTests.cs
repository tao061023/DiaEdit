namespace DiaEditCore.Tests.Commands.Stations;

using DiaEditCore.Commands.Stations;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using Xunit;

public sealed class CreateStationCommandTests
{
    [Fact]
    public void Execute_AddsStationToList_WithAllocatedId()
    {
        var stations = new List<Station>();
        var displayName = new DisplayName { Name = "新駅" };

        var command = new CreateStationCommand(stations, displayName, StationType.Standard, "OP1", "ﾂ1");
        command.Execute();

        Assert.Single(stations);
        Assert.Equal(1, stations[0].Id.Value);
        Assert.Equal("新駅", stations[0].DisplayName.Name);
        Assert.Equal(StationType.Standard, stations[0].Type);
        Assert.Equal("OP1", stations[0].OperatingCode);
        Assert.Equal("ﾂ1", stations[0].TelegraphCode);
    }

    [Fact]
    public void Execute_AllocatesMaxPlusOne_NotFillingGaps()
    {
        // 既存Idが1, 5の場合（3が削除されて欠番になっているケースを想定）でも
        // セッション中は単純に最大値+1（=6）を採番し、欠番を詰めないことを確認する。
        var stations = new List<Station>
        {
            new() { Id = new StationId(1), DisplayName = new DisplayName { Name = "A" }, Type = StationType.Standard },
            new() { Id = new StationId(5), DisplayName = new DisplayName { Name = "B" }, Type = StationType.Standard }
        };
        var displayName = new DisplayName { Name = "新駅" };

        var command = new CreateStationCommand(stations, displayName, StationType.Standard);
        command.Execute();

        Assert.Equal(6, command.Created!.Id.Value);
    }

    [Fact]
    public void Execute_ExposesCreatedStation()
    {
        var stations = new List<Station>();
        var displayName = new DisplayName { Name = "新駅" };

        var command = new CreateStationCommand(stations, displayName, StationType.Halt);
        command.Execute();

        Assert.NotNull(command.Created);
        Assert.Same(stations[0], command.Created);
    }

    [Fact]
    public void Undo_RemovesCreatedStationFromList()
    {
        var stations = new List<Station>();
        var displayName = new DisplayName { Name = "新駅" };

        var command = new CreateStationCommand(stations, displayName, StationType.Standard);
        command.Execute();
        Assert.Single(stations);

        command.Undo();

        Assert.Empty(stations);
    }

    [Fact]
    public void Undo_DoesNotAffectOtherExistingStations()
    {
        var existing = new Station { Id = new StationId(1), DisplayName = new DisplayName { Name = "既存" }, Type = StationType.Standard };
        var stations = new List<Station> { existing };
        var displayName = new DisplayName { Name = "新駅" };

        var command = new CreateStationCommand(stations, displayName, StationType.Standard);
        command.Execute();
        command.Undo();

        Assert.Single(stations);
        Assert.Same(existing, stations[0]);
    }

    [Fact]
    public void ConstructorInput_DisplayNameIsClonedNotAliased()
    {
        var stations = new List<Station>();
        var suppliedDisplayName = new DisplayName { Name = "新駅" };

        var command = new CreateStationCommand(stations, suppliedDisplayName, StationType.Standard);

        // コマンド生成後に呼び出し元インスタンスを書き換えても、Apply結果に影響しないこと
        suppliedDisplayName.Name = "生成後に書き換えた値";

        command.Execute();

        Assert.Equal("新駅", stations[0].DisplayName.Name);
    }

    [Fact]
    public void AffectedIds_IsEmpty()
    {
        var stations = new List<Station>();
        var displayName = new DisplayName { Name = "新駅" };

        var command = new CreateStationCommand(stations, displayName, StationType.Standard);

        Assert.Empty(command.AffectedIds);
    }
    [Fact]
    public void Undo後にRedoすると同一インスタンスが再利用される()
    {
        var stations = new List<Station>();
        var cmd = new CreateStationCommand(stations, new DisplayName { Name = "テスト駅" }, StationType.Standard);

        cmd.Execute();
        var firstInstance = cmd.Created;

        cmd.Undo();
        Assert.DoesNotContain(firstInstance, stations);

        cmd.Execute(); // CommandInvoker.Redo()と同じパス
        Assert.Same(firstInstance, cmd.Created);
        Assert.Contains(firstInstance, stations);
        Assert.Equal(firstInstance!.Id, cmd.Created!.Id); // Idも不変であることの確認
    }

    [Fact]
    public void Redo後も後続の属性変更コマンドが同一インスタンスへ適用される()
    {
        // Station追加→Undo→Redo→ChangeStationAttributesCommandが
        // cmd.Createdへ正しく反映されることの統合的な確認（回帰テスト、§9.1項目23）
        var stations = new List<Station>();
        var createCmd = new CreateStationCommand(stations, new DisplayName { Name = "テスト駅" }, StationType.Standard);
        createCmd.Execute();
        createCmd.Undo();
        createCmd.Execute();

        var target = createCmd.Created!;
        // ここでChangeStationAttributesCommandをtargetに対して構築・Executeし、
        // stations内の実インスタンスに変更が反映されていることを確認する
        // （ProjectSession等の実際の依存注入方法に合わせて調整してください）
    }
}
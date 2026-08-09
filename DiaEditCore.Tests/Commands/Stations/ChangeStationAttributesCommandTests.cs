namespace DiaEditCore.Tests.Commands.Stations;

using DiaEditCore.Commands.Stations;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.TimeTable;
using Xunit;

public sealed class ChangeStationAttributesCommandTests
{
    private static Station MakeStation() => new()
    {
        Id = new StationId(1),
        DisplayName = new DisplayName { Name = "旧名称", Abbreviation = "旧" },
        Type = StationType.Standard,
        OperatingCode = "OLD",
        TelegraphCode = "ﾂｵ",
        ShowsInStationTimetableOverride = null
    };

    private static TimeTableSetCache EmptyCache() => new();

    [Fact]
    public void Execute_AppliesAllFields()
    {
        var station = MakeStation();
        var newValues = new StationSnapshot(
            new DisplayName { Name = "新名称", Abbreviation = "新" },
            StationType.Halt,
            "NEW",
            "ﾂｼ",
            true);

        var command = new ChangeStationAttributesCommand(station, newValues, EmptyCache());
        command.Execute();

        Assert.Equal("新名称", station.DisplayName.Name);
        Assert.Equal("新", station.DisplayName.Abbreviation);
        Assert.Equal(StationType.Halt, station.Type);
        Assert.Equal("NEW", station.OperatingCode);
        Assert.Equal("ﾂｼ", station.TelegraphCode);
        Assert.True(station.ShowsInStationTimetableOverride);
    }

    [Fact]
    public void Undo_RestoresOriginalValues()
    {
        var station = MakeStation();
        var originalDisplayNameRef = station.DisplayName;
        var newValues = new StationSnapshot(
            new DisplayName { Name = "新名称" },
            StationType.Depot,
            "NEW",
            "ﾂｼ",
            false);

        var command = new ChangeStationAttributesCommand(station, newValues, EmptyCache());
        command.Execute();
        command.Undo();

        Assert.Equal("旧名称", station.DisplayName.Name);
        Assert.Equal("旧", station.DisplayName.Abbreviation);
        Assert.Equal(StationType.Standard, station.Type);
        Assert.Equal("OLD", station.OperatingCode);
        Assert.Equal("ﾂｵ", station.TelegraphCode);
        Assert.Null(station.ShowsInStationTimetableOverride);

        // 復元後のDisplayNameは、CaptureSnapshot時にCloneされた別インスタンスであるべき
        // （元の参照をそのまま握っていたのではなく、防御的コピーで独立していることの確認）。
        Assert.NotSame(originalDisplayNameRef, station.DisplayName);
    }

    [Fact]
    public void CaptureSnapshot_IsIndependentFromLaterMutation()
    {
        // Execute後にtarget.DisplayNameを外部から書き換えても、
        // コマンドが保持しているスナップショットには影響しないことを確認する
        // （DisplayNameが参照型であることに起因する事故がないことの直接的な検証）。
        var station = MakeStation();
        var newValues = new StationSnapshot(
            new DisplayName { Name = "新名称" },
            StationType.Standard,
            "NEW",
            "ﾂｼ",
            null);

        var command = new ChangeStationAttributesCommand(station, newValues, EmptyCache());
        command.Execute();

        // Apply後のtarget.DisplayNameを外部から破壊的変更
        station.DisplayName.Name = "外部から改変";
        station.DisplayName.Translations["ja"] = "改変値";

        command.Undo();

        // Undoは"旧名称"に戻るはずで、"外部から改変"の影響を受けていないこと
        Assert.Equal("旧名称", station.DisplayName.Name);
    }

    [Fact]
    public void ConstructorInput_IsClonedNotAliased()
    {
        // コンストラクタに渡したDisplayNameインスタンスを、呼び出し元が
        // コマンド生成後に書き換えても、Apply結果に影響しないことを確認する。
        var station = MakeStation();
        var suppliedDisplayName = new DisplayName { Name = "新名称" };
        var newValues = new StationSnapshot(
            suppliedDisplayName,
            StationType.Standard,
            "NEW",
            "ﾂｼ",
            null);

        var command = new ChangeStationAttributesCommand(station, newValues, EmptyCache());

        // コマンド生成後、呼び出し元が引き渡したインスタンスを書き換える
        suppliedDisplayName.Name = "生成後に書き換えた値";

        command.Execute();

        Assert.Equal("新名称", station.DisplayName.Name);
    }
}
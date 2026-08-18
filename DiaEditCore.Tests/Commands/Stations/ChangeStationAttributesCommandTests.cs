namespace DiaEditCore.Tests.Commands.Stations;

using DiaEditCore.Commands;
using DiaEditCore.Commands.Stations;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Session;
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
    public void Execute_AppliesAllFields()
    {
        var station = MakeStation();
        var newValues = new StationSnapshot(
            new DisplayName { Name = "新名称", Abbreviation = "新" },
            StationType.Halt,
            "NEW",
            "ﾂｼ",
            true);

        var command = new ChangeStationAttributesCommand(station, newValues, MakeSession());
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

        var command = new ChangeStationAttributesCommand(station, newValues, MakeSession());
        command.Execute();
        command.Undo();

        Assert.Equal("旧名称", station.DisplayName.Name);
        Assert.Equal("旧", station.DisplayName.Abbreviation);
        Assert.Equal(StationType.Standard, station.Type);
        Assert.Equal("OLD", station.OperatingCode);
        Assert.Equal("ﾂｵ", station.TelegraphCode);
        Assert.Null(station.ShowsInStationTimetableOverride);

        Assert.NotSame(originalDisplayNameRef, station.DisplayName);
    }

    [Fact]
    public void CaptureSnapshot_IsIndependentFromLaterMutation()
    {
        var station = MakeStation();
        var newValues = new StationSnapshot(
            new DisplayName { Name = "新名称" },
            StationType.Standard,
            "NEW",
            "ﾂｼ",
            null);

        var command = new ChangeStationAttributesCommand(station, newValues, MakeSession());
        command.Execute();

        station.DisplayName.Name = "外部から改変";
        station.DisplayName.Translations["ja"] = "改変値";

        command.Undo();

        Assert.Equal("旧名称", station.DisplayName.Name);
    }

    [Fact]
    public void ConstructorInput_IsClonedNotAliased()
    {
        var station = MakeStation();
        var suppliedDisplayName = new DisplayName { Name = "新名称" };
        var newValues = new StationSnapshot(
            suppliedDisplayName,
            StationType.Standard,
            "NEW",
            "ﾂｼ",
            null);

        var command = new ChangeStationAttributesCommand(station, newValues, MakeSession());

        suppliedDisplayName.Name = "生成後に書き換えた値";

        command.Execute();

        Assert.Equal("新名称", station.DisplayName.Name);
    }
}
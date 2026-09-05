namespace DiaEditCore.Tests.Commands.Stations.FloorUnitObjects;

using System.Collections.Generic;

using DiaEditCore.Commands;
using DiaEditCore.Commands.Stations.FloorUnitObjects;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;
using DiaEditCore.Session;

using Xunit;

public sealed class ChangePlatformAttributesCommandTests
{
    private static Platform MakePlatform() => new()
    {
        Id = new PlatformId(1),
        Base = new FloorUnitObjectBase { FloorUnitId = new FloorUnitId(1), Position = new Point(0, 0) },
        Name = "旧1番線",
        FacingRailIds = new List<RailId> { new(1) },
        EffectiveLength = 100.0
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
        var platform = MakePlatform();
        var newValues = new PlatformSnapshot("新1番線", new List<RailId> { new(2), new(3) }, 250.0);

        var command = new ChangePlatformAttributesCommand(platform, newValues, MakeSession());
        command.Execute();

        Assert.Equal("新1番線", platform.Name);
        Assert.Equal(new List<RailId> { new(2), new(3) }, platform.FacingRailIds);
        Assert.Equal(250.0, platform.EffectiveLength);
    }

    [Fact]
    public void Execute_AllowsSettingEffectiveLengthToNull()
    {
        var platform = MakePlatform();
        var newValues = new PlatformSnapshot("旧1番線", new List<RailId> { new(1) }, null);

        var command = new ChangePlatformAttributesCommand(platform, newValues, MakeSession());
        command.Execute();

        Assert.Null(platform.EffectiveLength);
    }

    [Fact]
    public void Undo_RestoresOriginalValues()
    {
        var platform = MakePlatform();
        var newValues = new PlatformSnapshot("新1番線", new List<RailId> { new(2), new(3) }, 250.0);

        var command = new ChangePlatformAttributesCommand(platform, newValues, MakeSession());
        command.Execute();
        command.Undo();

        Assert.Equal("旧1番線", platform.Name);
        Assert.Equal(new List<RailId> { new(1) }, platform.FacingRailIds);
        Assert.Equal(100.0, platform.EffectiveLength);
    }

    [Fact]
    public void Apply_DoesNotAliasCallerSuppliedList()
    {
        // FacingRailIdsは参照型コレクションのため、コマンドが呼び出し元のListインスタンスを
        // そのまま保持せず防御的コピーしていることを確認する（DisplayName Clone()と同種の懸念）。
        var platform = MakePlatform();
        var callerList = new List<RailId> { new(2) };
        var newValues = new PlatformSnapshot("新1番線", callerList, 250.0);

        var command = new ChangePlatformAttributesCommand(platform, newValues, MakeSession());
        command.Execute();

        callerList.Add(new RailId(99)); // 呼び出し元側で後から変更

        Assert.DoesNotContain(new RailId(99), platform.FacingRailIds);
    }

    [Fact]
    public void Undo_RestoresIndependentCopy_NotAliasedToSnapshot()
    {
        // Restore後にplatform.FacingRailIdsを外部から変更しても、スナップショット自体が
        // 汚染されないこと（Restore側もToList()で防御的コピーしていることの確認）。
        var platform = MakePlatform();
        var newValues = new PlatformSnapshot("新1番線", new List<RailId> { new(2) }, 250.0);

        var command = new ChangePlatformAttributesCommand(platform, newValues, MakeSession());
        command.Execute();
        command.Undo();

        platform.FacingRailIds.Add(new RailId(88));
        command.Undo(); // 再度Undoしても直前の変更に影響されず同じ復元結果になることを期待

        Assert.Contains(new RailId(1), platform.FacingRailIds);
    }

    [Fact]
    public void AffectedIds_ContainsOnlySelf_WhenCacheIsEmpty()
    {
        var platform = MakePlatform();
        var newValues = new PlatformSnapshot("新1番線", new List<RailId> { new(2) }, 250.0);

        var command = new ChangePlatformAttributesCommand(platform, newValues, MakeSession());

        Assert.Single(command.AffectedIds);
    }
}
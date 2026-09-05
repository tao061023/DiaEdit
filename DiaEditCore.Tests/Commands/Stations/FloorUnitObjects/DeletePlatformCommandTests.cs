namespace DiaEditCore.Tests.Commands.Stations.FloorUnitObjects;

using System.Collections.Generic;

using DiaEditCore.Commands;
using DiaEditCore.Commands.Stations.FloorUnitObjects;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;
using DiaEditCore.Session;

using Xunit;

public sealed class DeletePlatformCommandTests
{
    private static Platform MakePlatform(int id) => new()
    {
        Id = new PlatformId(id),
        Base = new FloorUnitObjectBase { FloorUnitId = new FloorUnitId(1), Position = new Point(0, 0) },
        Name = $"{id}番線",
        FacingRailIds = new List<RailId> { new(1) },
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
    public void Execute_RemovesPlatform_WhenNoReferencesExist()
    {
        var platform = MakePlatform(1);
        var platforms = new List<Platform> { platform };

        var command = new DeletePlatformCommand(platforms, platform, MakeSession());
        command.Execute();

        Assert.Empty(platforms);
    }

    [Fact]
    public void Undo_RestoresDeletedPlatform()
    {
        var platform = MakePlatform(1);
        var platforms = new List<Platform> { platform };

        var command = new DeletePlatformCommand(platforms, platform, MakeSession());
        command.Execute();
        command.Undo();

        Assert.Single(platforms);
        Assert.Same(platform, platforms[0]);
    }

    [Fact]
    public void AffectedIds_ContainsOnlySelf_WhenNoReferences()
    {
        var platform = MakePlatform(1);
        var platforms = new List<Platform> { platform };

        var command = new DeletePlatformCommand(platforms, platform, MakeSession());

        Assert.Single(command.AffectedIds);
    }

    [Fact]
    public void Constructor_DoesNotThrow_WhenDependencyResolverReportsNoDependents()
    {
        // 現行実装スコープではPlatformObjectId => []（DependencyResolver）が終端のため、
        // 通常は例外を投げないことを確認する回帰テスト。
        // 将来DependencyResolver側にPlatform参照ルールが追加された場合、この前提が崩れて
        // 本テストが失敗することで気づける設計にしている。
        var platform = MakePlatform(1);
        var platforms = new List<Platform> { platform };

        var exception = Record.Exception(() => new DeletePlatformCommand(platforms, platform, MakeSession()));

        Assert.Null(exception);
    }
}
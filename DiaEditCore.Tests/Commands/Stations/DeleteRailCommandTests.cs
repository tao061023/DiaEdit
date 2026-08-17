namespace DiaEditCore.Tests.Commands.Stations;

using DiaEditCore.Commands;
using DiaEditCore.Commands.Stations;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.TimeTable;
using DiaEditCore.Model.TimeTable.Trains;
using DiaEditCore.Session;
using Xunit;

public sealed class DeleteRailCommandTests
{
    private static readonly ValidationRules DefaultValidationRules = new(
        MinDwellTimeSec: null,
        MinHeadwaySec: null,
        MinTurnaroundSec: null,
        TrackEntryMarginSec: null,
        TrackPassMarginSec: null,
        EnableConflictDetection: true,
        EnableCarLengthCheck: true);

    private static readonly DateRange SampleDateRange = new(
        new DateTime(2026, 1, 1), new DateTime(2026, 1, 2));

    private static ProjectFile MakeEmptyProject() => new()
    {
        SchemaVersion = 1,
        ProjectSettings = new ProjectSettings(DefaultValidationRules),
    };

    private static ProjectSession MakeSession(ProjectFile project)
    {
        var session = new ProjectSession(new CommandInvoker());
        session.Load(project);
        return session;
    }

    private static ProjectSession EmptySession() => MakeSession(MakeEmptyProject());

    private static Rail MakeRail(int id = 1) => new()
    {
        Id = new RailId(id),
        Name = "テスト線路",
        LengthM = 100.0,
        SpeedLimitKph = 60.0,
        Role = RailRole.Track,
        EndpointA = new NoneEndpointRef(),
        EndpointB = new NoneEndpointRef()
    };

    [Fact]
    public void Execute_RemovesRail_WhenNoReferences()
    {
        var rail = MakeRail();
        var rails = new List<Rail> { rail };

        var command = new DeleteRailCommand(
            rails, rail, EmptySession(),
            allPlatforms: Array.Empty<Platform>(),
            allRestrictions: Array.Empty<TemporaryRestriction>(),
            allTrains: Array.Empty<Train>());
        command.Execute();

        Assert.Empty(rails);
    }

    [Fact]
    public void Undo_RestoresDeletedRail()
    {
        var rail = MakeRail();
        var rails = new List<Rail> { rail };

        var command = new DeleteRailCommand(
            rails, rail, EmptySession(),
            Array.Empty<Platform>(), Array.Empty<TemporaryRestriction>(), Array.Empty<Train>());
        command.Execute();
        command.Undo();

        Assert.Single(rails);
        Assert.Same(rail, rails[0]);
    }

    [Fact]
    public void Constructor_Throws_WhenPlatformReferencesRail()
    {
        var rail = MakeRail();
        var rails = new List<Rail> { rail };
        var platforms = new List<Platform>
        {
            new()
            {
                Id = new PlatformId(1),
                Base = new FloorUnitObjectBase { FloorUnitId = new FloorUnitId(10), Position = new Point(0, 0) },
                FacingRailIds = new List<RailId> { rail.Id }
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new DeleteRailCommand(
                rails, rail, EmptySession(),
                platforms, Array.Empty<TemporaryRestriction>(), Array.Empty<Train>()));

        Assert.Contains("Platform", ex.Message);
        Assert.Contains("FacingRailIds", ex.Message);
    }

    [Fact]
    public void Constructor_Throws_WhenTemporaryRestrictionTargetsRail()
    {
        var rail = MakeRail();
        var rails = new List<Rail> { rail };
        var restrictions = new List<TemporaryRestriction>
        {
            new(
                new TemporaryRestrictionId(1),
                new RestrictionTarget.Rail(rail.Id),
                ExtraRunTimeSec: null,
                SpeedLimitKph: 25,
                DateRange: SampleDateRange,
                Note: "")
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new DeleteRailCommand(
                rails, rail, EmptySession(),
                Array.Empty<Platform>(), restrictions, Array.Empty<Train>()));

        Assert.Contains("TemporaryRestriction", ex.Message);
    }

    [Fact]
    public void Constructor_Throws_WhenStopTimeReferencesRailAsTrackRailId()
    {
        var rail = MakeRail();
        var rails = new List<Rail> { rail };

        var train = new Train
        {
            Id = new TrainId(1),
            TimeTableSetId = new TimeTableSetId(1),
            TrainNumber = "1M",
            ServiceRouteId = new ServiceRouteId(1),
            TrainTypeId = new TrainTypeId(1),
            TrainTypeName = new DisplayName { Name = "普通" },
            Nickname = new DisplayName { Name = "" },
        };
        // StopTimesInternalはDiaEditCoreアセンブリ内からのみ書き込み可能
        // （テストのフィクスチャ構築用途として明示的にpublic化されている、Train.cs参照）。
        train.StopTimesInternal[new StopKey(new StationId(1), 0)] = new StopTime
        {
            TrackRailId = rail.Id
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new DeleteRailCommand(
                rails, rail, EmptySession(),
                Array.Empty<Platform>(), Array.Empty<TemporaryRestriction>(), new[] { train }));

        Assert.Contains("Train", ex.Message);
        Assert.Contains("TrackRailId", ex.Message);
    }

    [Fact]
    public void Constructor_Throws_WithAggregatedReasons_WhenMultiplePathsReference()
    {
        // 3経路のうち2つ以上が同時に該当する場合、1回の例外に理由が集約されることを確認する
        // （旧実装の「最初の1件で即throw」から変更した挙動、5.13.4節参照）。
        var rail = MakeRail();
        var rails = new List<Rail> { rail };
        var platforms = new List<Platform>
        {
            new()
            {
                Id = new PlatformId(1),
                Base = new FloorUnitObjectBase { FloorUnitId = new FloorUnitId(10), Position = new Point(0, 0) },
                FacingRailIds = new List<RailId> { rail.Id }
            }
        };
        var restrictions = new List<TemporaryRestriction>
        {
            new(
                new TemporaryRestrictionId(1),
                new RestrictionTarget.Rail(rail.Id),
                ExtraRunTimeSec: null,
                SpeedLimitKph: 25,
                DateRange: SampleDateRange,
                Note: "")
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new DeleteRailCommand(
                rails, rail, EmptySession(),
                platforms, restrictions, Array.Empty<Train>()));

        Assert.Contains("Platform", ex.Message);
        Assert.Contains("TemporaryRestriction", ex.Message);
    }

    [Fact]
    public void Constructor_DoesNotThrow_WhenReferencesBelongToDifferentRail()
    {
        var railToDelete = MakeRail(id: 1);
        var otherRail = MakeRail(id: 2);
        var rails = new List<Rail> { railToDelete, otherRail };
        var platforms = new List<Platform>
        {
            new()
            {
                Id = new PlatformId(1),
                Base = new FloorUnitObjectBase { FloorUnitId = new FloorUnitId(10), Position = new Point(0, 0) },
                FacingRailIds = new List<RailId> { otherRail.Id } // 削除対象ではない方のRailを参照
            }
        };

        var command = new DeleteRailCommand(
            rails, railToDelete, EmptySession(),
            platforms, Array.Empty<TemporaryRestriction>(), Array.Empty<Train>());

        Assert.NotNull(command);
    }

    [Fact]
    public void AffectedIds_ContainsOnlySelf_WhenNoReferences()
    {
        var rail = MakeRail();
        var rails = new List<Rail> { rail };

        var command = new DeleteRailCommand(
            rails, rail, EmptySession(),
            Array.Empty<Platform>(), Array.Empty<TemporaryRestriction>(), Array.Empty<Train>());

        Assert.Single(command.AffectedIds);
    }
}
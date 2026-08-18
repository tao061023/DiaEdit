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
    private static Rail MakeRail(int id) => new()
    {
        Id = new RailId(id),
        Name = $"線路{id}",
        LengthM = 100.0,
        SpeedLimitKph = 60.0,
        Role = RailRole.Track,
        EndpointA = new NoneEndpointRef(),
        EndpointB = new NoneEndpointRef()
    };

    private static Platform MakePlatform(int id, params int[] facingRailIds) => new()
    {
        Id = new PlatformId(id),
        Base = new FloorUnitObjectBase { FloorUnitId = new FloorUnitId(1), Position = new Point(0, 0) },
        FacingRailIds = facingRailIds.Select(r => new RailId(r)).ToList()
    };

    private static readonly DateRange SampleRange = new(new DateTime(2026, 1, 1), new DateTime(2026, 1, 2));

    private static TemporaryRestriction MakeRailRestriction(int id, int railId) => new(
        new TemporaryRestrictionId(id),
        new RestrictionTarget.Rail(new RailId(railId)),
        ExtraRunTimeSec: null,
        SpeedLimitKph: 25,
        DateRange: SampleRange,
        Note: "");

    private static Train MakeTrainUsingRail(int trainId, int railId)
    {
        var train = new Train
        {
            Id = new TrainId(trainId),
            TimeTableSetId = new TimeTableSetId(1),
            TrainNumber = $"{trainId}M",
            ServiceRouteId = new ServiceRouteId(1),
            TrainTypeId = new TrainTypeId(1),
            TrainTypeName = new DisplayName { Name = "普通" },
            Nickname = new DisplayName { Name = "" },
        };
        // StopTimesInternalはDiaEditCoreアセンブリ内からのみ書き込み可能（同一アセンブリのテストなので使用可）。
        train.StopTimesInternal[new StopKey(new StationId(1), 0)] = new StopTime { TrackRailId = new RailId(railId) };
        return train;
    }

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
    public void Execute_RemovesRail_WhenNoReferencesExist()
    {
        var rail = MakeRail(1);
        var rails = new List<Rail> { rail };

        var command = new DeleteRailCommand(
            rails, rail, MakeSession(),
            allPlatforms: Array.Empty<Platform>(),
            allRestrictions: Array.Empty<TemporaryRestriction>(),
            allTrains: Array.Empty<Train>());
        command.Execute();

        Assert.Empty(rails);
    }

    [Fact]
    public void Undo_RestoresDeletedRail()
    {
        var rail = MakeRail(1);
        var rails = new List<Rail> { rail };

        var command = new DeleteRailCommand(
            rails, rail, MakeSession(),
            Array.Empty<Platform>(), Array.Empty<TemporaryRestriction>(), Array.Empty<Train>());
        command.Execute();
        command.Undo();

        Assert.Single(rails);
        Assert.Same(rail, rails[0]);
    }

    [Fact]
    public void Constructor_Throws_WhenPlatformReferencesRail()
    {
        var rail = MakeRail(1);
        var rails = new List<Rail> { rail };
        var platforms = new[] { MakePlatform(1, facingRailIds: 1) };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new DeleteRailCommand(
                rails, rail, MakeSession(),
                platforms, Array.Empty<TemporaryRestriction>(), Array.Empty<Train>()));

        Assert.Contains("FacingRailIds", ex.Message);
    }

    [Fact]
    public void Constructor_Throws_WhenTemporaryRestrictionTargetsRail()
    {
        var rail = MakeRail(1);
        var rails = new List<Rail> { rail };
        var restrictions = new[] { MakeRailRestriction(1, railId: 1) };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new DeleteRailCommand(
                rails, rail, MakeSession(),
                Array.Empty<Platform>(), restrictions, Array.Empty<Train>()));

        Assert.Contains("TemporaryRestriction", ex.Message);
    }

    [Fact]
    public void Constructor_Throws_WhenTrainStopTimeReferencesRail()
    {
        var rail = MakeRail(1);
        var rails = new List<Rail> { rail };
        var trains = new[] { MakeTrainUsingRail(trainId: 1, railId: 1) };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new DeleteRailCommand(
                rails, rail, MakeSession(),
                Array.Empty<Platform>(), Array.Empty<TemporaryRestriction>(), trains));

        Assert.Contains("StopTime.TrackRailId", ex.Message);
    }

    [Fact]
    public void Constructor_Throws_WithAllThreeReasonsAggregated_WhenAllThreePathsReference()
    {
        // 3経路すべてに参照がある場合、最初の1件で即throwせず、理由を集約して1回の例外にまとめる
        // （旧実装からの変更点。v12.20設計判断）。
        var rail = MakeRail(1);
        var rails = new List<Rail> { rail };
        var platforms = new[] { MakePlatform(1, facingRailIds: 1) };
        var restrictions = new[] { MakeRailRestriction(1, railId: 1) };
        var trains = new[] { MakeTrainUsingRail(trainId: 1, railId: 1) };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new DeleteRailCommand(rails, rail, MakeSession(), platforms, restrictions, trains));

        Assert.Contains("FacingRailIds", ex.Message);
        Assert.Contains("TemporaryRestriction", ex.Message);
        Assert.Contains("StopTime.TrackRailId", ex.Message);
    }

    [Fact]
    public void Constructor_DoesNotThrow_WhenReferencesPointToOtherRail()
    {
        // Platform/TemporaryRestriction/TrainがいずれもRailId=2を参照しており、
        // 削除対象のRailId=1には無関係であることを確認する（誤検知しないことの確認）。
        var railToDelete = MakeRail(1);
        var otherRail = MakeRail(2);
        var rails = new List<Rail> { railToDelete, otherRail };

        var platforms = new[] { MakePlatform(1, facingRailIds: 2) };
        var restrictions = new[] { MakeRailRestriction(1, railId: 2) };
        var trains = new[] { MakeTrainUsingRail(trainId: 1, railId: 2) };

        var command = new DeleteRailCommand(
            rails, railToDelete, MakeSession(), platforms, restrictions, trains);

        Assert.NotNull(command);
    }

    [Fact]
    public void AffectedIds_ContainsOnlySelf_WhenNoReferences()
    {
        var rail = MakeRail(1);
        var rails = new List<Rail> { rail };

        var command = new DeleteRailCommand(
            rails, rail, MakeSession(),
            Array.Empty<Platform>(), Array.Empty<TemporaryRestriction>(), Array.Empty<Train>());

        Assert.Single(command.AffectedIds);
    }
}
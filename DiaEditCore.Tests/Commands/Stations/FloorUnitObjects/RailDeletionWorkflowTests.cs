namespace DiaEditCore.Tests.Commands.Stations.FloorUnitObjects;

using System;
using System.Collections.Generic;

using DiaEditCore.Commands;
using DiaEditCore.Commands.Stations.FloorUnitObjects;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;
using DiaEditCore.Model.TimeTable;
using DiaEditCore.Model.TimeTable.Trains;
using DiaEditCore.Session;

using Xunit;

public sealed class RailDeletionWorkflowTests
{
    private static readonly FloorUnitId FloorUnit = new(1);

    private static FloorUnitObjectBase MakeBase(Point p) => new() { FloorUnitId = FloorUnit, Position = p };

    private static readonly ValidationRules DefaultValidationRules = new(
        MinDwellTimeSec: null,
        MinHeadwaySec: null,
        MinTurnaroundSec: null,
        TrackEntryMarginSec: null,
        TrackPassMarginSec: null,
        EnableConflictDetection: true,
        EnableCarLengthCheck: true);

    private static ProjectSession MakeSession()
    {
        var session = new ProjectSession(new CommandInvoker());
        session.Load(new ProjectFile { SchemaVersion = 1, ProjectSettings = new ProjectSettings(DefaultValidationRules) });
        return session;
    }

    private static Rail MakeRail(int id, RailEndpointRef a, RailEndpointRef b) => new()
    {
        Id = new RailId(id),
        Name = "",
        LengthM = 10,
        SpeedLimitKph = 25,
        Role = RailRole.Normal,
        EndpointA = a,
        EndpointB = b,
    };

    // ================================
    // 両端点が孤立（削除後N=0）→両端Vanish
    // ================================

    [Fact]
    public void Execute_BothEndpointsIsolated_DeletesRailAndBothVanishingEndpoints()
    {
        var posA = new Point(0, 0);
        var posB = new Point(10, 0);
        var entryA = new EntryPoint { Id = new EntryPointId(1), Base = MakeBase(posA), Type = EntryPointType.Arrival };
        var entryB = new EntryPoint { Id = new EntryPointId(2), Base = MakeBase(posB), Type = EntryPointType.Departure };
        var rail = MakeRail(1, new EntryPointEndpointRef(entryA.Id), new EntryPointEndpointRef(entryB.Id));

        var rails = new List<Rail> { rail };
        var noneEndpoints = new List<NoneEndpoint>();
        var boundaryPoints = new List<BoundaryPoint>();
        var entryPoints = new List<EntryPoint> { entryA, entryB };
        var bufferStops = new List<BufferStop>();
        var switchers = new List<Switcher>();
        var stationPaths = new List<StationPath>();

        var command = RailDeletionWorkflow.Create(
            rails, rail, MakeSession(),
            allPlatforms: Array.Empty<Platform>(),
            allRestrictions: Array.Empty<TemporaryRestriction>(),
            allTrains: Array.Empty<Train>(),
            noneEndpoints, boundaryPoints, entryPoints, bufferStops, switchers, stationPaths);

        command.Execute();

        Assert.Empty(rails);
        Assert.Empty(entryPoints);
    }

    [Fact]
    public void Undo_RestoresRailAndBothVanishedEndpoints()
    {
        var posA = new Point(0, 0);
        var posB = new Point(10, 0);
        var entryA = new EntryPoint { Id = new EntryPointId(1), Base = MakeBase(posA), Type = EntryPointType.Arrival };
        var entryB = new EntryPoint { Id = new EntryPointId(2), Base = MakeBase(posB), Type = EntryPointType.Departure };
        var rail = MakeRail(1, new EntryPointEndpointRef(entryA.Id), new EntryPointEndpointRef(entryB.Id));

        var rails = new List<Rail> { rail };
        var noneEndpoints = new List<NoneEndpoint>();
        var boundaryPoints = new List<BoundaryPoint>();
        var entryPoints = new List<EntryPoint> { entryA, entryB };
        var bufferStops = new List<BufferStop>();
        var switchers = new List<Switcher>();
        var stationPaths = new List<StationPath>();

        var command = RailDeletionWorkflow.Create(
            rails, rail, MakeSession(),
            Array.Empty<Platform>(), Array.Empty<TemporaryRestriction>(), Array.Empty<Train>(),
            noneEndpoints, boundaryPoints, entryPoints, bufferStops, switchers, stationPaths);

        command.Execute();
        command.Undo();

        Assert.Single(rails);
        Assert.Same(rail, rails[0]);
        Assert.Equal(2, entryPoints.Count);
    }

    // ================================
    // BoundaryPointを共有する2本のうち1本を削除→残る側がN=1へ降格（Keep/NoneEndpoint化）
    // ================================

    [Fact]
    public void Execute_RemainingRailAtSharedBoundaryPoint_DowngradesToNoneEndpoint()
    {
        var sharedPos = new Point(5, 5);
        var bp = new BoundaryPoint { Id = new BoundaryPointId(1), Base = MakeBase(sharedPos) };

        var railToDelete = MakeRail(1,
            new BoundaryPointEndpointRef(bp.Id),
            new EntryPointEndpointRef(new EntryPointId(99)));
        var railToKeep = MakeRail(2,
            new BoundaryPointEndpointRef(bp.Id),
            new EntryPointEndpointRef(new EntryPointId(98)));

        var deletedRailFarEndpoint = new EntryPoint
        {
            Id = new EntryPointId(99), Base = MakeBase(new Point(0, 0)), Type = EntryPointType.Arrival,
        };
        var keptRailFarEndpoint = new EntryPoint
        {
            Id = new EntryPointId(98), Base = MakeBase(new Point(20, 20)), Type = EntryPointType.Departure,
        };

        var rails = new List<Rail> { railToDelete, railToKeep };
        var noneEndpoints = new List<NoneEndpoint>();
        var boundaryPoints = new List<BoundaryPoint> { bp };
        var entryPoints = new List<EntryPoint> { deletedRailFarEndpoint, keptRailFarEndpoint };
        var bufferStops = new List<BufferStop>();
        var switchers = new List<Switcher>();
        var stationPaths = new List<StationPath>();

        var command = RailDeletionWorkflow.Create(
            rails, railToDelete, MakeSession(),
            Array.Empty<Platform>(), Array.Empty<TemporaryRestriction>(), Array.Empty<Train>(),
            noneEndpoints, boundaryPoints, entryPoints, bufferStops, switchers, stationPaths);

        command.Execute();

        // railToDeleteのみが消え、railToKeepは残る
        Assert.Single(rails);
        Assert.Same(railToKeep, rails[0]);

        // 共有BoundaryPointはN=1に縮退したためNoneEndpointへ降格
        Assert.Empty(boundaryPoints);
        Assert.Single(noneEndpoints);
        Assert.IsType<NoneEndpointRef>(railToKeep.EndpointA);

        // railToDelete側の遠端（EntryPointId=99）はN=0となりVanishで消滅、
        // railToKeep側の遠端（EntryPointId=98）は無関係のため残る
        Assert.Single(entryPoints);
        Assert.Equal(keptRailFarEndpoint.Id, entryPoints[0].Id);
    }

    [Fact]
    public void Undo_RestoresBoundaryPointDowngrade_ToOriginalState()
    {
        var sharedPos = new Point(5, 5);
        var bp = new BoundaryPoint { Id = new BoundaryPointId(1), Base = MakeBase(sharedPos) };
        var originalKeepRef = new BoundaryPointEndpointRef(bp.Id);

        var railToDelete = MakeRail(1, new BoundaryPointEndpointRef(bp.Id), new EntryPointEndpointRef(new EntryPointId(99)));
        var railToKeep = MakeRail(2, originalKeepRef, new EntryPointEndpointRef(new EntryPointId(98)));

        var deletedRailFarEndpoint = new EntryPoint { Id = new EntryPointId(99), Base = MakeBase(new Point(0, 0)), Type = EntryPointType.Arrival };
        var keptRailFarEndpoint = new EntryPoint { Id = new EntryPointId(98), Base = MakeBase(new Point(20, 20)), Type = EntryPointType.Departure };

        var rails = new List<Rail> { railToDelete, railToKeep };
        var noneEndpoints = new List<NoneEndpoint>();
        var boundaryPoints = new List<BoundaryPoint> { bp };
        var entryPoints = new List<EntryPoint> { deletedRailFarEndpoint, keptRailFarEndpoint };
        var bufferStops = new List<BufferStop>();
        var switchers = new List<Switcher>();
        var stationPaths = new List<StationPath>();

        var command = RailDeletionWorkflow.Create(
            rails, railToDelete, MakeSession(),
            Array.Empty<Platform>(), Array.Empty<TemporaryRestriction>(), Array.Empty<Train>(),
            noneEndpoints, boundaryPoints, entryPoints, bufferStops, switchers, stationPaths);

        command.Execute();
        command.Undo();

        Assert.Equal(2, rails.Count);
        Assert.Single(boundaryPoints);
        Assert.Empty(noneEndpoints);
        Assert.Equal(originalKeepRef, railToKeep.EndpointA);
        Assert.Equal(2, entryPoints.Count);
    }

    // ================================
    // 既存DeleteRailCommandの3経路チェックが無改修のまま効いていること
    // ================================

    [Fact]
    public void Create_Throws_WhenRailIsReferencedByPlatform()
    {
        // Createメソッド呼び出し（内部でDeleteRailCommandのコンストラクタが実行される）時点で、
        // TransActionCommand化する前に例外が飛ぶことを確認する（RailDeletionWorkFlow.csの
        // ドキュメントコメント「望ましい挙動」の直接検証）。
        //
        // 注意：Createは内部でDeleteRailCommand構築前に両端点のResolvePositionを行うため、
        // Rail.EndpointA/Bが指すEntryPoint実体を実際にentryPointsコレクションへ用意しておく
        // 必要がある（用意しないと「FacingRailIds違反」より先に「参照先が見つからない」という
        // 別の例外が飛んでしまい、本テストの検証意図とズレる）。
        var epA = new EntryPoint { Id = new EntryPointId(1), Base = MakeBase(new Point(0, 0)), Type = EntryPointType.Arrival };
        var epB = new EntryPoint { Id = new EntryPointId(2), Base = MakeBase(new Point(10, 0)), Type = EntryPointType.Departure };
        var rail = MakeRail(1, new EntryPointEndpointRef(epA.Id), new EntryPointEndpointRef(epB.Id));
        var rails = new List<Rail> { rail };
        var platform = new Platform
        {
            Id = new PlatformId(1),
            Base = MakeBase(new Point(0, 0)),
            SecondaryPosition = new Point(10, 10),
            FacingRailIds = new List<RailId> { rail.Id },
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            RailDeletionWorkflow.Create(
                rails, rail, MakeSession(),
                new[] { platform }, Array.Empty<TemporaryRestriction>(), Array.Empty<Train>(),
                new List<NoneEndpoint>(), new List<BoundaryPoint>(),
                new List<EntryPoint> { epA, epB }, new List<BufferStop>(), new List<Switcher>(), new List<StationPath>()));

        Assert.Contains("FacingRailIds", ex.Message);
    }

    [Fact]
    public void Create_DoesNotDeleteRail_WhenConstructorThrows()
    {
        // 例外発生時、rails自体には副作用が及んでいないことを確認する。
        // 上記と同じ理由で、EndpointA/Bが指すEntryPoint実体を用意する。
        var epA = new EntryPoint { Id = new EntryPointId(1), Base = MakeBase(new Point(0, 0)), Type = EntryPointType.Arrival };
        var epB = new EntryPoint { Id = new EntryPointId(2), Base = MakeBase(new Point(10, 0)), Type = EntryPointType.Departure };
        var rail = MakeRail(1, new EntryPointEndpointRef(epA.Id), new EntryPointEndpointRef(epB.Id));
        var rails = new List<Rail> { rail };
        var trains = new[]
        {
            new Train
            {
                Id = new TrainId(1),
                TimeTableSetId = new TimeTableSetId(1),
                TrainNumber = "1M",
                ServiceRouteId = new ServiceRouteId(1),
                TrainTypeId = new TrainTypeId(1),
                TrainTypeName = new DisplayName { Name = "普通" },
                Nickname = new DisplayName { Name = "" },
            },
        };
        trains[0].StopTimesInternal[new StopKey(new StationId(1), 0)] = new StopTime { TrackRailId = rail.Id };

        Assert.Throws<InvalidOperationException>(() =>
            RailDeletionWorkflow.Create(
                rails, rail, MakeSession(),
                Array.Empty<Platform>(), Array.Empty<TemporaryRestriction>(), trains,
                new List<NoneEndpoint>(), new List<BoundaryPoint>(),
                new List<EntryPoint> { epA, epB }, new List<BufferStop>(), new List<Switcher>(), new List<StationPath>()));

        Assert.Single(rails);
        Assert.Same(rail, rails[0]);
    }
}
namespace DiaEditCore.Tests.Commands.Stations.FloorUnitObjects;

using System;
using System.Collections.Generic;
using System.Linq;

using DiaEditCore.Algorithm.Stations.FloorUnitObjects;
using DiaEditCore.Commands;
using DiaEditCore.Commands.Stations.FloorUnitObjects;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;
using DiaEditCore.Session;

using Xunit;

public sealed class RailEndpointConvergenceWorkflowTests
{
    private static readonly Point Pos = new(10, 10);
    private static readonly FloorUnitId FloorUnit = new(1);

    private static FloorUnitObjectBase MakeBase(Point? p = null) =>
        new() { FloorUnitId = FloorUnit, Position = p ?? Pos };

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

    /// <summary>各テストで使い回す空のコレクション・IdAllocator一式。</summary>
    private sealed class Fixture
    {
        public List<Rail> Rails { get; } = new();
        public List<NoneEndpoint> NoneEndpoints { get; } = new();
        public List<BoundaryPoint> BoundaryPoints { get; } = new();
        public List<EntryPoint> EntryPoints { get; } = new();
        public List<BufferStop> BufferStops { get; } = new();
        public List<Switcher> Switchers { get; } = new();
        public List<StationPath> StationPaths { get; } = new();

        public IdAllocator<NoneEndpointId> NoneEndpointIds { get; } = new(v => new NoneEndpointId(v), Array.Empty<int>());
        public IdAllocator<BoundaryPointId> BoundaryPointIds { get; } = new(v => new BoundaryPointId(v), Array.Empty<int>());
        public IdAllocator<SwitcherId> SwitcherIds { get; } = new(v => new SwitcherId(v), Array.Empty<int>());

        public IUndoableCommand? Reconcile(
            IReadOnlyList<RailEndpointLocation> converging,
            ObjectId? oldObjectId,
            Point? position = null) =>
            RailEndpointConvergenceWorkflow.Reconcile(
                position ?? Pos, FloorUnit, converging, oldObjectId, MakeSession(),
                Rails, NoneEndpoints, NoneEndpointIds,
                BoundaryPoints, BoundaryPointIds,
                EntryPoints, BufferStops,
                Switchers, SwitcherIds,
                StationPaths);
    }

    // ================================
    // Vanish (N=0)
    // ================================

    [Fact]
    public void Vanish_WithoutOldObjectId_ReturnsNull()
    {
        var fx = new Fixture();

        var result = fx.Reconcile(Array.Empty<RailEndpointLocation>(), oldObjectId: null);

        Assert.Null(result);
    }

    [Fact]
    public void Vanish_WithOldObjectId_DeletesOldEndpoint()
    {
        var fx = new Fixture();
        var oldEntry = new EntryPoint { Id = new EntryPointId(1), Base = MakeBase(), Type = EntryPointType.Both };
        fx.EntryPoints.Add(oldEntry);

        var result = fx.Reconcile(Array.Empty<RailEndpointLocation>(), new EntryPointObjectId(oldEntry.Id));

        Assert.NotNull(result);
        result!.Execute();

        Assert.Empty(fx.EntryPoints);
    }

    [Fact]
    public void Vanish_Undo_RestoresDeletedEndpoint()
    {
        var fx = new Fixture();
        var oldEntry = new EntryPoint { Id = new EntryPointId(1), Base = MakeBase(), Type = EntryPointType.Both };
        fx.EntryPoints.Add(oldEntry);

        var result = fx.Reconcile(Array.Empty<RailEndpointLocation>(), new EntryPointObjectId(oldEntry.Id))!;
        result.Execute();
        result.Undo();

        Assert.Single(fx.EntryPoints);
        Assert.Same(oldEntry, fx.EntryPoints[0]);
    }

    [Fact]
    public void Vanish_Throws_WhenOldEndpointIsBlockedByStationPath()
    {
        var fx = new Fixture();
        var oldEntry = new EntryPoint { Id = new EntryPointId(1), Base = MakeBase(), Type = EntryPointType.Both };
        fx.EntryPoints.Add(oldEntry);

        fx.StationPaths.Add(new StationPath
        {
            Id = new StationPathId(1),
            FloorUnitId = FloorUnit,
            Name = "経路A",
            Direction = StationPathDirection.Arrival,
            Waypoints = new List<StationPathWaypoint> { new EntryPointWaypoint(oldEntry.Id) },
        });

        Assert.Throws<InvalidOperationException>(
            () => fx.Reconcile(Array.Empty<RailEndpointLocation>(), new EntryPointObjectId(oldEntry.Id)));

        // ブロックされた場合は削除ステップが一切積まれないこと（副作用なし）も確認する。
        Assert.Single(fx.EntryPoints);
    }

    // ================================
    // Keep (N=1)
    // ================================

    [Fact]
    public void Keep_AlreadyEntryPoint_ReturnsNull()
    {
        var fx = new Fixture();
        var ep = new EntryPoint { Id = new EntryPointId(1), Base = MakeBase(), Type = EntryPointType.Arrival };
        fx.EntryPoints.Add(ep);
        var rail = MakeRail(1, new EntryPointEndpointRef(ep.Id), new BufferStopEndpointRef(new BufferStopId(1)));
        fx.Rails.Add(rail);

        var converging = new List<RailEndpointLocation> { new(rail.Id, RailEnd.A, rail.EndpointA) };

        var result = fx.Reconcile(converging, new EntryPointObjectId(ep.Id));

        Assert.Null(result);
    }

    [Fact]
    public void Keep_Throws_WhenDowngradedBoundaryPointIsBlockedByStationPath()
    {
        var fx = new Fixture();
        var bp = new BoundaryPoint { Id = new BoundaryPointId(1), Base = MakeBase() };
        fx.BoundaryPoints.Add(bp);
        var rail = MakeRail(1, new BoundaryPointEndpointRef(bp.Id), new BufferStopEndpointRef(new BufferStopId(1)));
        fx.Rails.Add(rail);

        fx.StationPaths.Add(new StationPath
        {
            Id = new StationPathId(1),
            FloorUnitId = FloorUnit,
            Name = "経路B",
            Direction = StationPathDirection.Arrival,
            Waypoints = new List<StationPathWaypoint> { new BoundaryPointWaypoint(bp.Id) },
        });

        var converging = new List<RailEndpointLocation> { new(rail.Id, RailEnd.A, rail.EndpointA) };

        Assert.Throws<InvalidOperationException>(
            () => fx.Reconcile(converging, new BoundaryPointObjectId(bp.Id)));

        // ブロックされた場合はNoneEndpoint作成・BoundaryPoint削除・Ref張替えのいずれも発生しないこと。
        Assert.Empty(fx.NoneEndpoints);
        Assert.Single(fx.BoundaryPoints);
        Assert.IsType<BoundaryPointEndpointRef>(rail.EndpointA);
    }

    [Fact]
    public void Keep_Throws_WhenDowngradedSwitcherIsBlockedByStationPath()
    {
        var fx = new Fixture();
        var sw = new Switcher { Id = new SwitcherId(1), Base = MakeBase(), PortCount = 3 };
        fx.Switchers.Add(sw);
        var rail = MakeRail(1, new SwitcherEndpointRef(sw.Id, 0), new BufferStopEndpointRef(new BufferStopId(1)));
        fx.Rails.Add(rail);

        fx.StationPaths.Add(new StationPath
        {
            Id = new StationPathId(1),
            FloorUnitId = FloorUnit,
            Name = "経路C",
            Direction = StationPathDirection.Shunting,
            Waypoints = new List<StationPathWaypoint> { new SwitcherWaypoint(sw.Id) },
        });

        var converging = new List<RailEndpointLocation> { new(rail.Id, RailEnd.A, rail.EndpointA) };

        Assert.Throws<InvalidOperationException>(
            () => fx.Reconcile(converging, new SwitcherObjectId(sw.Id)));

        Assert.Empty(fx.NoneEndpoints);
        Assert.Single(fx.Switchers);
        Assert.IsType<SwitcherEndpointRef>(rail.EndpointA);
    }

    [Fact]
    public void Keep_AlreadyBufferStop_ReturnsNull()
    {
        var fx = new Fixture();
        var bs = new BufferStop { Id = new BufferStopId(1), Base = MakeBase() };
        fx.BufferStops.Add(bs);
        var rail = MakeRail(1, new BufferStopEndpointRef(bs.Id), new EntryPointEndpointRef(new EntryPointId(1)));
        fx.Rails.Add(rail);

        var converging = new List<RailEndpointLocation> { new(rail.Id, RailEnd.A, rail.EndpointA) };

        var result = fx.Reconcile(converging, new BufferStopObjectId(bs.Id));

        Assert.Null(result);
    }

    [Fact]
    public void Keep_DowngradesFromBoundaryPoint_ToNoneEndpoint()
    {
        var fx = new Fixture();
        var bp = new BoundaryPoint { Id = new BoundaryPointId(1), Base = MakeBase() };
        fx.BoundaryPoints.Add(bp);
        var rail = MakeRail(1, new BoundaryPointEndpointRef(bp.Id), new BufferStopEndpointRef(new BufferStopId(1)));
        fx.Rails.Add(rail);

        var converging = new List<RailEndpointLocation> { new(rail.Id, RailEnd.A, rail.EndpointA) };

        var result = fx.Reconcile(converging, new BoundaryPointObjectId(bp.Id));

        Assert.NotNull(result);
        result!.Execute();

        Assert.Single(fx.NoneEndpoints);
        Assert.Empty(fx.BoundaryPoints);
        Assert.IsType<NoneEndpointRef>(rail.EndpointA);
    }

    [Fact]
    public void Keep_Undo_RestoresOriginalBoundaryPointAndRef()
    {
        var fx = new Fixture();
        var bp = new BoundaryPoint { Id = new BoundaryPointId(1), Base = MakeBase() };
        fx.BoundaryPoints.Add(bp);
        var originalRef = new BoundaryPointEndpointRef(bp.Id);
        var rail = MakeRail(1, originalRef, new BufferStopEndpointRef(new BufferStopId(1)));
        fx.Rails.Add(rail);

        var converging = new List<RailEndpointLocation> { new(rail.Id, RailEnd.A, rail.EndpointA) };
        var result = fx.Reconcile(converging, new BoundaryPointObjectId(bp.Id))!;

        result.Execute();
        result.Undo();

        Assert.Empty(fx.NoneEndpoints);
        Assert.Single(fx.BoundaryPoints);
        Assert.Equal(originalRef, rail.EndpointA);
    }

    // ================================
    // BoundaryPoint (N=2)
    // ================================

    [Fact]
    public void BoundaryPoint_AlreadySameBoundaryPoint_ReturnsNull()
    {
        var fx = new Fixture();
        var bp = new BoundaryPoint { Id = new BoundaryPointId(1), Base = MakeBase() };
        fx.BoundaryPoints.Add(bp);
        var rail1 = MakeRail(1, new BoundaryPointEndpointRef(bp.Id), new BufferStopEndpointRef(new BufferStopId(1)));
        var rail2 = MakeRail(2, new BoundaryPointEndpointRef(bp.Id), new BufferStopEndpointRef(new BufferStopId(2)));
        fx.Rails.AddRange(new[] { rail1, rail2 });

        var converging = new List<RailEndpointLocation>
        {
            new(rail1.Id, RailEnd.A, rail1.EndpointA),
            new(rail2.Id, RailEnd.A, rail2.EndpointA),
        };

        var result = fx.Reconcile(converging, new BoundaryPointObjectId(bp.Id));

        Assert.Null(result);
    }

    [Fact]
    public void BoundaryPoint_CreatesNewBoundaryPoint_AndRetargetsBothRails_AndDeletesOldEndpoints()
    {
        var fx = new Fixture();
        var ep1 = new EntryPoint { Id = new EntryPointId(1), Base = MakeBase(), Type = EntryPointType.Arrival };
        var ep2 = new EntryPoint { Id = new EntryPointId(2), Base = MakeBase(), Type = EntryPointType.Departure };
        fx.EntryPoints.AddRange(new[] { ep1, ep2 });

        var rail1 = MakeRail(1, new EntryPointEndpointRef(ep1.Id), new BufferStopEndpointRef(new BufferStopId(1)));
        var rail2 = MakeRail(2, new EntryPointEndpointRef(ep2.Id), new BufferStopEndpointRef(new BufferStopId(2)));
        fx.Rails.AddRange(new[] { rail1, rail2 });

        var converging = new List<RailEndpointLocation>
        {
            new(rail1.Id, RailEnd.A, rail1.EndpointA),
            new(rail2.Id, RailEnd.A, rail2.EndpointA),
        };

        var result = fx.Reconcile(converging, oldObjectId: null);

        Assert.NotNull(result);
        result!.Execute();

        Assert.Single(fx.BoundaryPoints);
        Assert.Empty(fx.EntryPoints);

        var newBpId = fx.BoundaryPoints[0].Id;
        Assert.Equal(new BoundaryPointEndpointRef(newBpId), rail1.EndpointA);
        Assert.Equal(new BoundaryPointEndpointRef(newBpId), rail2.EndpointA);
    }

    [Fact]
    public void BoundaryPoint_Undo_RestoresOriginalEndpointsAndRemovesNewBoundaryPoint()
    {
        var fx = new Fixture();
        var ep1 = new EntryPoint { Id = new EntryPointId(1), Base = MakeBase(), Type = EntryPointType.Arrival };
        var ep2 = new EntryPoint { Id = new EntryPointId(2), Base = MakeBase(), Type = EntryPointType.Departure };
        fx.EntryPoints.AddRange(new[] { ep1, ep2 });

        var rail1 = MakeRail(1, new EntryPointEndpointRef(ep1.Id), new BufferStopEndpointRef(new BufferStopId(1)));
        var rail2 = MakeRail(2, new EntryPointEndpointRef(ep2.Id), new BufferStopEndpointRef(new BufferStopId(2)));
        fx.Rails.AddRange(new[] { rail1, rail2 });

        var converging = new List<RailEndpointLocation>
        {
            new(rail1.Id, RailEnd.A, rail1.EndpointA),
            new(rail2.Id, RailEnd.A, rail2.EndpointA),
        };

        var result = fx.Reconcile(converging, oldObjectId: null)!;
        result.Execute();
        result.Undo();

        Assert.Empty(fx.BoundaryPoints);
        Assert.Equal(2, fx.EntryPoints.Count);
        Assert.Equal(new EntryPointEndpointRef(ep1.Id), rail1.EndpointA);
        Assert.Equal(new EntryPointEndpointRef(ep2.Id), rail2.EndpointA);
    }

    [Fact]
    public void BoundaryPoint_Throws_WhenVanishingEndpointIsBlockedByStationPath()
    {
        var fx = new Fixture();
        var ep1 = new EntryPoint { Id = new EntryPointId(1), Base = MakeBase(), Type = EntryPointType.Arrival };
        var ep2 = new EntryPoint { Id = new EntryPointId(2), Base = MakeBase(), Type = EntryPointType.Departure };
        fx.EntryPoints.AddRange(new[] { ep1, ep2 });

        var rail1 = MakeRail(1, new EntryPointEndpointRef(ep1.Id), new BufferStopEndpointRef(new BufferStopId(1)));
        var rail2 = MakeRail(2, new EntryPointEndpointRef(ep2.Id), new BufferStopEndpointRef(new BufferStopId(2)));
        fx.Rails.AddRange(new[] { rail1, rail2 });

        fx.StationPaths.Add(new StationPath
        {
            Id = new StationPathId(1),
            FloorUnitId = FloorUnit,
            Name = "経路A",
            Direction = StationPathDirection.Arrival,
            Waypoints = new List<StationPathWaypoint> { new EntryPointWaypoint(ep1.Id) },
        });

        var converging = new List<RailEndpointLocation>
        {
            new(rail1.Id, RailEnd.A, rail1.EndpointA),
            new(rail2.Id, RailEnd.A, rail2.EndpointA),
        };

        Assert.Throws<InvalidOperationException>(() => fx.Reconcile(converging, oldObjectId: null));
    }

    // ================================
    // SwitcherNew (N=3,4)
    // ================================

    [Fact]
    public void SwitcherNew_CreatesSwitcherWithCorrectPortCount_AndAssignsPortsByRailIdOrder()
    {
        var fx = new Fixture();
        var ep1 = new EntryPoint { Id = new EntryPointId(1), Base = MakeBase(), Type = EntryPointType.Arrival };
        var ep2 = new EntryPoint { Id = new EntryPointId(2), Base = MakeBase(), Type = EntryPointType.Departure };
        var ep3 = new EntryPoint { Id = new EntryPointId(3), Base = MakeBase(), Type = EntryPointType.Both };
        fx.EntryPoints.AddRange(new[] { ep1, ep2, ep3 });

        var rail1 = MakeRail(1, new EntryPointEndpointRef(ep1.Id), new BufferStopEndpointRef(new BufferStopId(1)));
        var rail2 = MakeRail(2, new EntryPointEndpointRef(ep2.Id), new BufferStopEndpointRef(new BufferStopId(2)));
        var rail3 = MakeRail(3, new EntryPointEndpointRef(ep3.Id), new BufferStopEndpointRef(new BufferStopId(3)));
        fx.Rails.AddRange(new[] { rail1, rail2, rail3 });

        var converging = new List<RailEndpointLocation>
        {
            new(rail1.Id, RailEnd.A, rail1.EndpointA),
            new(rail2.Id, RailEnd.A, rail2.EndpointA),
            new(rail3.Id, RailEnd.A, rail3.EndpointA),
        };

        var result = fx.Reconcile(converging, oldObjectId: null);

        Assert.NotNull(result);
        result!.Execute();

        Assert.Single(fx.Switchers);
        Assert.Equal(3, fx.Switchers[0].PortCount);
        Assert.Empty(fx.EntryPoints);

        var newSwitcherId = fx.Switchers[0].Id;
        Assert.Equal(new SwitcherEndpointRef(newSwitcherId, 0), rail1.EndpointA);
        Assert.Equal(new SwitcherEndpointRef(newSwitcherId, 1), rail2.EndpointA);
        Assert.Equal(new SwitcherEndpointRef(newSwitcherId, 2), rail3.EndpointA);
    }

    [Fact]
    public void SwitcherNew_Undo_RestoresOriginalEntryPointsAndRemovesNewSwitcher()
    {
        var fx = new Fixture();
        var ep1 = new EntryPoint { Id = new EntryPointId(1), Base = MakeBase(), Type = EntryPointType.Arrival };
        var ep2 = new EntryPoint { Id = new EntryPointId(2), Base = MakeBase(), Type = EntryPointType.Departure };
        var ep3 = new EntryPoint { Id = new EntryPointId(3), Base = MakeBase(), Type = EntryPointType.Both };
        fx.EntryPoints.AddRange(new[] { ep1, ep2, ep3 });

        var rail1 = MakeRail(1, new EntryPointEndpointRef(ep1.Id), new BufferStopEndpointRef(new BufferStopId(1)));
        var rail2 = MakeRail(2, new EntryPointEndpointRef(ep2.Id), new BufferStopEndpointRef(new BufferStopId(2)));
        var rail3 = MakeRail(3, new EntryPointEndpointRef(ep3.Id), new BufferStopEndpointRef(new BufferStopId(3)));
        fx.Rails.AddRange(new[] { rail1, rail2, rail3 });

        var converging = new List<RailEndpointLocation>
        {
            new(rail1.Id, RailEnd.A, rail1.EndpointA),
            new(rail2.Id, RailEnd.A, rail2.EndpointA),
            new(rail3.Id, RailEnd.A, rail3.EndpointA),
        };

        var result = fx.Reconcile(converging, oldObjectId: null)!;
        result.Execute();
        result.Undo();

        Assert.Empty(fx.Switchers);
        Assert.Equal(3, fx.EntryPoints.Count);
        Assert.Equal(new EntryPointEndpointRef(ep1.Id), rail1.EndpointA);
        Assert.Equal(new EntryPointEndpointRef(ep2.Id), rail2.EndpointA);
        Assert.Equal(new EntryPointEndpointRef(ep3.Id), rail3.EndpointA);
    }

    // ================================
    // SwitcherExpand (N=3,4、既存Switcher参照あり)
    // ================================

    [Fact]
    public void SwitcherExpand_AlreadyReconciled_ReturnsNull()
    {
        var fx = new Fixture();
        var sw = new Switcher { Id = new SwitcherId(1), Base = MakeBase(), PortCount = 3 };
        fx.Switchers.Add(sw);

        var r1 = MakeRail(1, new SwitcherEndpointRef(sw.Id, 0), new BufferStopEndpointRef(new BufferStopId(1)));
        var r2 = MakeRail(2, new SwitcherEndpointRef(sw.Id, 1), new BufferStopEndpointRef(new BufferStopId(2)));
        var r3 = MakeRail(3, new SwitcherEndpointRef(sw.Id, 2), new BufferStopEndpointRef(new BufferStopId(3)));
        fx.Rails.AddRange(new[] { r1, r2, r3 });

        var converging = new List<RailEndpointLocation>
        {
            new(r1.Id, RailEnd.A, r1.EndpointA),
            new(r2.Id, RailEnd.A, r2.EndpointA),
            new(r3.Id, RailEnd.A, r3.EndpointA),
        };

        var result = fx.Reconcile(converging, oldObjectId: null);

        Assert.Null(result);
    }

    /// <summary>
    /// 【重要・要確認】SwitcherExpandケースの疑わしい挙動を検証するテスト。
    ///
    /// RailEndpointConvergenceResolver.FindBlockingStationPathsのXMLドキュメントコメントには
    /// 「既存Switcherが『拡張』される場合（ConvergenceKind.SwitcherExpand）はSwitcher自体は
    /// 消滅しない点に注意（呼び出し側でdisappearingIdsに含めないこと）」と明記されている。
    ///
    /// しかし実装（RailEndpointConvergenceWorkflow.Reconcileのcase SwitcherExpand）は、
    /// AddDeleteStepsForVanishingEndpoints(converging, ...)を「収束集合そのまま」渡している。
    /// convergingには拡張対象である既存Switcherを指すSwitcherEndpointRefが含まれるため、
    /// disappearing（=削除対象ObjectId集合）にSwitcherObjectId(existingSwitcherId)が
    /// 含まれてしまい、ChangeSwitcherAttributesCommandで拡張したはずの同じSwitcherが
    /// 直後にDeleteFloorUnitObjectCommandで削除されてしまう。
    ///
    /// 本テストは「ドキュメント上あるべき正しい挙動（Switcherは生き残る）」を期待値として
    /// 書いている。現在の実装ではこのテストは失敗するはずであり、失敗自体が上記の設計と
    /// 実装の不一致を裏付ける。
    /// </summary>
    [Fact]
    public void SwitcherExpand_ExpandedSwitcher_ShouldSurvive_AndNotBeDeletedByItself()
    {
        var fx = new Fixture();
        var existingSwitcher = new Switcher
        {
            Id = new SwitcherId(1),
            Base = MakeBase(),
            PortCount = 3,
            Mechanism = new SwitchMechanism { RootPortIndex = 0, NormalPortIndex = 1, ReversePortIndex = 2 },
            ValidRoutes = new List<PortPair> { new(0, 1) },
        };
        fx.Switchers.Add(existingSwitcher);

        var ep = new EntryPoint { Id = new EntryPointId(1), Base = MakeBase(), Type = EntryPointType.Arrival };
        fx.EntryPoints.Add(ep);

        var railSwitcher0 = MakeRail(1, new SwitcherEndpointRef(existingSwitcher.Id, 0), new BufferStopEndpointRef(new BufferStopId(1)));
        var railSwitcher1 = MakeRail(2, new SwitcherEndpointRef(existingSwitcher.Id, 1), new BufferStopEndpointRef(new BufferStopId(2)));
        var railSwitcher2 = MakeRail(3, new SwitcherEndpointRef(existingSwitcher.Id, 2), new BufferStopEndpointRef(new BufferStopId(3)));
        var railNew = MakeRail(4, new EntryPointEndpointRef(ep.Id), new BufferStopEndpointRef(new BufferStopId(4)));
        fx.Rails.AddRange(new[] { railSwitcher0, railSwitcher1, railSwitcher2, railNew });

        var converging = new List<RailEndpointLocation>
        {
            new(railSwitcher0.Id, RailEnd.A, railSwitcher0.EndpointA),
            new(railSwitcher1.Id, RailEnd.A, railSwitcher1.EndpointA),
            new(railSwitcher2.Id, RailEnd.A, railSwitcher2.EndpointA),
            new(railNew.Id, RailEnd.A, railNew.EndpointA),
        };

        var result = fx.Reconcile(converging, oldObjectId: null);

        Assert.NotNull(result);
        result!.Execute();

        // 期待：拡張された既存Switcherは1件のまま生き残り、PortCountが4に拡張されている。
        Assert.Single(fx.Switchers);
        Assert.Equal(existingSwitcher.Id, fx.Switchers[0].Id);
        Assert.Equal(4, fx.Switchers[0].PortCount);

        Assert.Empty(fx.EntryPoints); // 旧EntryPointは正しく消滅している
        Assert.All(
            new[] { railSwitcher0, railSwitcher1, railSwitcher2, railNew },
            r => Assert.IsType<SwitcherEndpointRef>(r.EndpointA));
    }

    /// <summary>
    /// 上記と同種の懸念の、より実害の大きい派生ケース。
    /// 拡張対象の既存Switcherを（他の理由で）StationPath.WaypointsのSwitcherWaypointが
    /// 参照している場合、FindBlockingStationPathsのdisappearing集合に誤って
    /// existingSwitcherIdが含まれることで、本来ブロックされるべきでない「単純な拡張」操作が
    /// 誤って例外で弾かれてしまわないかを確認する。
    /// 現在の実装ではこのテストは（意図せず）例外を送出するはずであり、失敗した場合は
    /// 上記の設計上の懸念が実運用上も問題になることの証拠となる。
    /// </summary>
    [Fact]
    public void SwitcherExpand_ShouldNotBeBlockedByStationPathReferencingTheSurvivingSwitcherItself()
    {
        var fx = new Fixture();
        var existingSwitcher = new Switcher { Id = new SwitcherId(1), Base = MakeBase(), PortCount = 3 };
        fx.Switchers.Add(existingSwitcher);

        var ep = new EntryPoint { Id = new EntryPointId(1), Base = MakeBase(), Type = EntryPointType.Arrival };
        fx.EntryPoints.Add(ep);

        var railSwitcher0 = MakeRail(1, new SwitcherEndpointRef(existingSwitcher.Id, 0), new BufferStopEndpointRef(new BufferStopId(1)));
        var railSwitcher1 = MakeRail(2, new SwitcherEndpointRef(existingSwitcher.Id, 1), new BufferStopEndpointRef(new BufferStopId(2)));
        var railSwitcher2 = MakeRail(3, new SwitcherEndpointRef(existingSwitcher.Id, 2), new BufferStopEndpointRef(new BufferStopId(3)));
        var railNew = MakeRail(4, new EntryPointEndpointRef(ep.Id), new BufferStopEndpointRef(new BufferStopId(4)));
        fx.Rails.AddRange(new[] { railSwitcher0, railSwitcher1, railSwitcher2, railNew });

        // この既存Switcherを（拡張後も）正当に参照するStationPathが既にあるケース。
        fx.StationPaths.Add(new StationPath
        {
            Id = new StationPathId(1),
            FloorUnitId = FloorUnit,
            Name = "既存の構内進路",
            Direction = StationPathDirection.Shunting,
            Waypoints = new List<StationPathWaypoint> { new SwitcherWaypoint(existingSwitcher.Id) },
        });

        var converging = new List<RailEndpointLocation>
        {
            new(railSwitcher0.Id, RailEnd.A, railSwitcher0.EndpointA),
            new(railSwitcher1.Id, RailEnd.A, railSwitcher1.EndpointA),
            new(railSwitcher2.Id, RailEnd.A, railSwitcher2.EndpointA),
            new(railNew.Id, RailEnd.A, railNew.EndpointA),
        };

        // 期待：Switcher自体は消滅しないため、このStationPathが拡張操作をブロックしてはならない。
        var exception = Record.Exception(() => fx.Reconcile(converging, oldObjectId: null));
        Assert.Null(exception);
    }

    // ================================
    // Error (N>=5)
    // ================================

    [Fact]
    public void FiveOrMoreConverging_Throws()
    {
        var fx = new Fixture();
        var converging = new List<RailEndpointLocation>();
        for (var i = 1; i <= 5; i++)
        {
            var ep = new EntryPoint { Id = new EntryPointId(i), Base = MakeBase(), Type = EntryPointType.Arrival };
            fx.EntryPoints.Add(ep);
            var rail = MakeRail(i, new EntryPointEndpointRef(ep.Id), new BufferStopEndpointRef(new BufferStopId(i)));
            fx.Rails.Add(rail);
            converging.Add(new RailEndpointLocation(rail.Id, RailEnd.A, rail.EndpointA));
        }

        Assert.Throws<InvalidOperationException>(() => fx.Reconcile(converging, oldObjectId: null));
    }
}
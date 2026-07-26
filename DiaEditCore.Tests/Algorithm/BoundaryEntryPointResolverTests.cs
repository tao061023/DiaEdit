using DiaEditCore.Algorithm;

using DiaEditCore.Model;
using DiaEditCore.Model.Routes;

using Xunit;

namespace DiaEditCore.Tests.Algorithm;

public class BoundaryEntryPointResolverTests
{
    private static MainRoute MakeMainRoute(MainRouteId id, params StationId[] stations)
        => new()
        {
            Id = id,
            Name = new DisplayName { Name = "test-main-route" },
            StationOrder = stations.ToList(),
        };

    private static StationConnectionSegment MakeSegment(
        StationConnectionSegmentId id, StationId from, StationId to, MainRouteId mainRouteId)
        => new()
        {
            Id = id,
            FromStationId = from,
            ToStationId = to,
            FromEntryPointId = new EntryPointId(id.Value * 10 + 1),
            ToEntryPointId = new EntryPointId(id.Value * 10 + 2),
            MainRouteId = mainRouteId,
            BaseRunTimeSec = 60,
        };

    private static StationConnection MakeConnection(
        StationConnectionId id, MainRouteId mainRouteId, StationConnectionDirection direction,
        params StationConnectionSegmentId[] segmentIds)
        => new()
        {
            Id = id,
            Name = "test-sc",
            MainRouteId = mainRouteId,
            Direction = direction,
            Segments = segmentIds.ToList(),
        };

    // 3駅A-B-C、下り方向のSC1本（A→B、B→Cの2segment）で構成される基本ケース
    private static (MainRoute mainRoute, List<StationConnection> connections, List<StationConnectionSegment> segments)
        BuildThreeStationDownOnly()
    {
        var mainRouteId = new MainRouteId(1);
        var stA = new StationId(1);
        var stB = new StationId(2);
        var stC = new StationId(3);
        var mainRoute = MakeMainRoute(mainRouteId, stA, stB, stC);

        var seg1 = MakeSegment(new StationConnectionSegmentId(1), stA, stB, mainRouteId);
        var seg2 = MakeSegment(new StationConnectionSegmentId(2), stB, stC, mainRouteId);

        var scDown = MakeConnection(
            new StationConnectionId(1), mainRouteId, StationConnectionDirection.Down,
            seg1.Id, seg2.Id);

        return (mainRoute, new List<StationConnection> { scDown }, new List<StationConnectionSegment> { seg1, seg2 });
    }

    [Fact]
    public void 正常系_単一区間で一致するStationConnectionが1件返る()
    {
        // このテスト専用：A-Bの1ホップのみで構成されるStationConnection
        // （BuildThreeStationDownOnly()のSCはA-B-Cの2ホップ全体を1本で構成するため流用できない）
        var mainRouteId = new MainRouteId(1);
        var stA = new StationId(1);
        var stB = new StationId(2);
        var mainRoute = MakeMainRoute(mainRouteId, stA, stB);

        var seg = MakeSegment(new StationConnectionSegmentId(1), stA, stB, mainRouteId);
        var scDown = MakeConnection(new StationConnectionId(1), mainRouteId, StationConnectionDirection.Down, seg.Id);

        var result = BoundaryEntryPointResolver.ResolveBoundaryEntryPoint(
            mainRouteId, fromIndex: 0, toIndex: 1,
            new List<MainRoute> { mainRoute },
            new List<StationConnection> { scDown },
            new List<StationConnectionSegment> { seg });

        Assert.Single(result);
        Assert.Equal(new StationId(1), result[0].FromStationId);
        Assert.Equal(new StationId(2), result[0].ToStationId);
    }

    [Fact]
    public void 正常系_2ホップ全体を1本で構成するSCは1ホップ区間の問い合わせでは一致しない()
    {
        // BuildThreeStationDownOnly()のSCはA-B-Cの2ホップ全体を1本で構成しているため、
        // fromIndex=0,toIndex=1（A-Bの1ホップだけ）の問い合わせでは一致しないことを明示する回帰テスト
        var (mainRoute, connections, segments) = BuildThreeStationDownOnly();

        var result = BoundaryEntryPointResolver.ResolveBoundaryEntryPoint(
            mainRoute.Id, fromIndex: 0, toIndex: 1,
            new List<MainRoute> { mainRoute }, connections, segments);

        Assert.Empty(result);
    }

    [Fact]
    public void 正常系_複数ホップにまたがるStationConnectionでも境界駅側の末尾要素が返る()
    {
        var (mainRoute, connections, segments) = BuildThreeStationDownOnly();

        // A(0) -> C(2) の2ホップ区間。末尾要素（B->C）が境界駅要素として返る想定
        var result = BoundaryEntryPointResolver.ResolveBoundaryEntryPoint(
            mainRoute.Id, fromIndex: 0, toIndex: 2,
            new List<MainRoute> { mainRoute }, connections, segments);

        Assert.Single(result);
        Assert.Equal(new StationId(2), result[0].FromStationId);
        Assert.Equal(new StationId(3), result[0].ToStationId);
    }

    [Fact]
    public void 複々線_同一MainRoute同一Directionの複数StationConnectionが両方候補として返る()
    {
        var mainRouteId = new MainRouteId(1);
        var stA = new StationId(1);
        var stB = new StationId(2);
        var mainRoute = MakeMainRoute(mainRouteId, stA, stB);

        // 本線・緩行線：物理的に別のSCSを使う2本のDown方向StationConnection
        var segMain = MakeSegment(new StationConnectionSegmentId(1), stA, stB, mainRouteId);
        var segLocal = MakeSegment(new StationConnectionSegmentId(2), stA, stB, mainRouteId);

        var scMain = MakeConnection(new StationConnectionId(1), mainRouteId, StationConnectionDirection.Down, segMain.Id);
        var scLocal = MakeConnection(new StationConnectionId(2), mainRouteId, StationConnectionDirection.Down, segLocal.Id);

        var result = BoundaryEntryPointResolver.ResolveBoundaryEntryPoint(
            mainRouteId, fromIndex: 0, toIndex: 1,
            new List<MainRoute> { mainRoute },
            new List<StationConnection> { scMain, scLocal },
            new List<StationConnectionSegment> { segMain, segLocal });

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void 異常系_駅列が一致しないStationConnectionは候補から除外される()
    {
        var mainRouteId = new MainRouteId(1);
        var stA = new StationId(1);
        var stB = new StationId(2);
        var stX = new StationId(99); // stationOrderに含まれない駅
        var mainRoute = MakeMainRoute(mainRouteId, stA, stB);

        // A -> X という、mainRoute.StationOrderの並びと矛盾するsegmentを持つSC
        var mismatchedSeg = MakeSegment(new StationConnectionSegmentId(1), stA, stX, mainRouteId);
        var mismatchedSc = MakeConnection(
            new StationConnectionId(1), mainRouteId, StationConnectionDirection.Down, mismatchedSeg.Id);

        var result = BoundaryEntryPointResolver.ResolveBoundaryEntryPoint(
            mainRouteId, fromIndex: 0, toIndex: 1,
            new List<MainRoute> { mainRoute },
            new List<StationConnection> { mismatchedSc },
            new List<StationConnectionSegment> { mismatchedSeg });

        Assert.Empty(result);
    }

    [Fact]
    public void 異常系_対応するStationConnectionが存在しない場合は空リスト()
    {
        var mainRouteId = new MainRouteId(1);
        var mainRoute = MakeMainRoute(mainRouteId, new StationId(1), new StationId(2));

        var result = BoundaryEntryPointResolver.ResolveBoundaryEntryPoint(
            mainRouteId, fromIndex: 0, toIndex: 1,
            new List<MainRoute> { mainRoute },
            new List<StationConnection>(),
            new List<StationConnectionSegment>());

        Assert.Empty(result);
    }

    [Fact]
    public void 異常系_fromIndexとtoIndexが同一の場合は空リスト()
    {
        var (mainRoute, connections, segments) = BuildThreeStationDownOnly();

        var result = BoundaryEntryPointResolver.ResolveBoundaryEntryPoint(
            mainRoute.Id, fromIndex: 1, toIndex: 1,
            new List<MainRoute> { mainRoute }, connections, segments);

        Assert.Empty(result);
    }

    [Fact]
    public void 異常系_範囲外のインデックスは空リスト()
    {
        var (mainRoute, connections, segments) = BuildThreeStationDownOnly();

        var result = BoundaryEntryPointResolver.ResolveBoundaryEntryPoint(
            mainRoute.Id, fromIndex: 0, toIndex: 5,
            new List<MainRoute> { mainRoute }, connections, segments);

        Assert.Empty(result);
    }

    [Fact]
    public void 方向判定_fromIndexがtoIndexより大きい場合はUp方向のStationConnectionのみ一致する()
    {
        var mainRouteId = new MainRouteId(1);
        var stA = new StationId(1);
        var stB = new StationId(2);
        var mainRoute = MakeMainRoute(mainRouteId, stA, stB);

        // Up方向：B -> A のsegmentを持つSC
        var segUp = MakeSegment(new StationConnectionSegmentId(1), stB, stA, mainRouteId);
        var scUp = MakeConnection(new StationConnectionId(1), mainRouteId, StationConnectionDirection.Up, segUp.Id);

        // Down方向：A -> B のsegmentを持つSC（今回のfromIndex=1,toIndex=0では一致しないはず）
        var segDown = MakeSegment(new StationConnectionSegmentId(2), stA, stB, mainRouteId);
        var scDown = MakeConnection(new StationConnectionId(2), mainRouteId, StationConnectionDirection.Down, segDown.Id);

        var result = BoundaryEntryPointResolver.ResolveBoundaryEntryPoint(
            mainRouteId, fromIndex: 1, toIndex: 0,
            new List<MainRoute> { mainRoute },
            new List<StationConnection> { scUp, scDown },
            new List<StationConnectionSegment> { segUp, segDown });

        Assert.Single(result);
        Assert.Equal(new StationId(2), result[0].FromStationId);
        Assert.Equal(new StationId(1), result[0].ToStationId);
    }

    [Fact]
    public void 異常系_MainRouteIdが存在しない場合は空リスト()
    {
        var (mainRoute, connections, segments) = BuildThreeStationDownOnly();

        var result = BoundaryEntryPointResolver.ResolveBoundaryEntryPoint(
            new MainRouteId(999), fromIndex: 0, toIndex: 1,
            new List<MainRoute> { mainRoute }, connections, segments);

        Assert.Empty(result);
    }
}
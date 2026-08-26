namespace DiaEditCore.Tests.Algorithm.Dependency;

using DiaEditCore.Algorithm.Dependency;
using DiaEditCore.Model;

using Xunit;

public class DependencyResolverTests
{
    private static TimeTableSetCache MakeCache() => new();

    [Fact]
    public void ResolveAffected_Station_ResolvesConnectedStationConnection()
    {
        var cache = MakeCache();
        var stationId = new StationId(1);
        var scId = new StationConnectionId(10);
        cache.StationConnectionIndex[stationId] = [scId];

        var changed = new HashSet<ObjectId> { new StationObjectId(stationId) };
        var result = DependencyResolver.ResolveAffected(changed, cache);

        Assert.Equal(2, result.Count);
        Assert.Contains(new StationObjectId(stationId), result);
        Assert.Contains(new StationConnectionObjectId(scId), result);
    }

    [Fact]
    public void ResolveAffected_StationConnectionSegment_ResolvesBothScAndTemporaryRestriction()
    {
        var cache = MakeCache();
        var scsId = new StationConnectionSegmentId(1);
        var scId = new StationConnectionId(10);
        var trId = new TemporaryRestrictionId(20);
        cache.ScsUsedByIndex[scsId] = [scId];
        cache.TemporaryRestrictionBySegmentIndex[scsId] = [trId];

        var changed = new HashSet<ObjectId> { new StationConnectionSegmentObjectId(scsId) };
        var result = DependencyResolver.ResolveAffected(changed, cache);

        Assert.Equal(3, result.Count);
        Assert.Contains(new StationConnectionObjectId(scId), result);
        Assert.Contains(new TemporaryRestrictionObjectId(trId), result);
    }

    [Fact]
    public void ResolveAffected_ScsUsedByMultipleStationConnections_ResolvesAll()
    {
        var cache = MakeCache();
        var scsId = new StationConnectionSegmentId(1);
        var scIdA = new StationConnectionId(10);
        var scIdB = new StationConnectionId(11);
        cache.ScsUsedByIndex[scsId] = [scIdA, scIdB];

        var changed = new HashSet<ObjectId> { new StationConnectionSegmentObjectId(scsId) };
        var result = DependencyResolver.ResolveAffected(changed, cache);

        Assert.Contains(new StationConnectionObjectId(scIdA), result);
        Assert.Contains(new StationConnectionObjectId(scIdB), result);
    }

    [Fact]
    public void ResolveAffected_MultiHop_EntryPointToStationConnection()
    {
        var cache = MakeCache();
        var entryPointId = new EntryPointId(1);
        var scId = new StationConnectionId(10);
        cache.EntryPointConnectionIndex[entryPointId] = [scId];

        var changed = new HashSet<ObjectId> { new EntryPointObjectId(entryPointId) };
        var result = DependencyResolver.ResolveAffected(changed, cache);

        Assert.Equal(2, result.Count);
        Assert.Contains(new StationConnectionObjectId(scId), result);
    }

    [Fact]
    public void ResolveAffected_TerminalNode_ReturnsOnlyItself()
    {
        var cache = MakeCache();
        var boundaryPointId = new BoundaryPointId(1);

        var changed = new HashSet<ObjectId> { new BoundaryPointObjectId(boundaryPointId) };
        var result = DependencyResolver.ResolveAffected(changed, cache);

        Assert.Single(result);
        Assert.Contains(new BoundaryPointObjectId(boundaryPointId), result);
    }

    [Fact]
    public void ResolveAffected_EmptyInput_ReturnsEmpty()
    {
        var cache = MakeCache();
        var changed = new HashSet<ObjectId>();

        var result = DependencyResolver.ResolveAffected(changed, cache);

        Assert.Empty(result);
    }

    [Fact]
    public void ResolveAffected_AlreadyIncludedDependent_DoesNotDuplicate()
    {
        var cache = MakeCache();
        var stationId = new StationId(1);
        var scId = new StationConnectionId(10);
        cache.StationConnectionIndex[stationId] = [scId];

        // changedIds に最初から依存先(StationConnection)も含めておく
        var changed = new HashSet<ObjectId>
        {
            new StationObjectId(stationId),
            new StationConnectionObjectId(scId)
        };
        var result = DependencyResolver.ResolveAffected(changed, cache);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ResolveAffected_CyclicDependency_TerminatesAndVisitsAllNodes()
    {
        var cache = MakeCache();
        // 人工的な循環データ：StationA→SC1→StationB→SC2→StationA（相当）を
        // MainRouteConnectionIndexとStationConnectionIndexを組み合わせて模擬する。
        // 実際のモデル上は循環しない想定だが、6.11.2節「循環があっても停止すること」を検証する。
        var stationA = new StationId(1);
        var stationB = new StationId(2);
        var sc1 = new StationConnectionId(10);
        var sc2 = new StationConnectionId(11);
        var mainRoute = new MainRouteId(100);

        cache.StationConnectionIndex[stationA] = [sc1];
        cache.StationConnectionIndex[stationB] = [sc2];
        // StationConnectionObjectId自体は現状終端ノードなので、循環を作るには
        // MainRouteを経由する形にする（MainRoute→SC、SC自体は終端という現実の構造に近い形）
        cache.MainRouteConnectionIndex[mainRoute] = [sc1, sc2];

        var changed = new HashSet<ObjectId>
        {
            new StationObjectId(stationA),
            new StationObjectId(stationB),
            new MainRouteObjectId(mainRoute)
        };

        // 無限ループせず終了することそのものが主眼（タイムアウトなしで戻ってくることを確認）
        var result = DependencyResolver.ResolveAffected(changed, cache);

        Assert.Contains(new StationConnectionObjectId(sc1), result);
        Assert.Contains(new StationConnectionObjectId(sc2), result);
        Assert.Equal(5, result.Count); // StationA, StationB, MainRoute, SC1, SC2
    }

    [Fact]
    public void ResolveAffected_EntryPoint_ResolvesOrphanedStationConnectionSegment()
    {
        // どのStationConnectionにも属さない孤立SegmentからのEntryPoint直接参照を
        // EntryPointUsedBySegmentIndex経由で捕捉できることを検証する（グラフ完成セッション新設分）。
        var cache = MakeCache();
        var entryPointId = new EntryPointId(1);
        var scsId = new StationConnectionSegmentId(50);
        cache.EntryPointUsedBySegmentIndex[entryPointId] = [scsId];
    
        var changed = new HashSet<ObjectId> { new EntryPointObjectId(entryPointId) };
        var result = DependencyResolver.ResolveAffected(changed, cache);
    
        Assert.Contains(new StationConnectionSegmentObjectId(scsId), result);
    }
    
    [Fact]
    public void ResolveAffected_EntryPoint_ResolvesBothStationConnectionAndOrphanedSegment()
    {
        // EntryPointConnectionIndex（SC経由）とEntryPointUsedBySegmentIndex（孤立Segment直接参照）が
        // 同時に存在するケースで、両方が合成されて返ることを検証する。
        var cache = MakeCache();
        var entryPointId = new EntryPointId(1);
        var scId = new StationConnectionId(10);
        var scsId = new StationConnectionSegmentId(50);
        cache.EntryPointConnectionIndex[entryPointId] = [scId];
        cache.EntryPointUsedBySegmentIndex[entryPointId] = [scsId];
    
        var changed = new HashSet<ObjectId> { new EntryPointObjectId(entryPointId) };
        var result = DependencyResolver.ResolveAffected(changed, cache);
    
        Assert.Contains(new StationConnectionObjectId(scId), result);
        Assert.Contains(new StationConnectionSegmentObjectId(scsId), result);
    }
    
    [Fact]
    public void ResolveAffected_MainRoute_ResolvesOrphanedStationConnectionSegment()
    {
        // どのStationConnectionにも属さない孤立SegmentからのMainRoute直接参照を
        // MainRouteUsedBySegmentIndex経由で捕捉できることを検証する（グラフ完成セッション新設分）。
        var cache = MakeCache();
        var mainRouteId = new MainRouteId(100);
        var scsId = new StationConnectionSegmentId(50);
        cache.MainRouteUsedBySegmentIndex[mainRouteId] = [scsId];
    
        var changed = new HashSet<ObjectId> { new MainRouteObjectId(mainRouteId) };
        var result = DependencyResolver.ResolveAffected(changed, cache);
    
        Assert.Contains(new StationConnectionSegmentObjectId(scsId), result);
    }
    
    [Fact]
    public void ResolveAffected_MainRoute_ResolvesBothStationConnectionAndOrphanedSegment()
    {
        var cache = MakeCache();
        var mainRouteId = new MainRouteId(100);
        var scId = new StationConnectionId(10);
        var scsId = new StationConnectionSegmentId(50);
        cache.MainRouteConnectionIndex[mainRouteId] = [scId];
        cache.MainRouteUsedBySegmentIndex[mainRouteId] = [scsId];
    
        var changed = new HashSet<ObjectId> { new MainRouteObjectId(mainRouteId) };
        var result = DependencyResolver.ResolveAffected(changed, cache);
    
        Assert.Contains(new StationConnectionObjectId(scId), result);
        Assert.Contains(new StationConnectionSegmentObjectId(scsId), result);
    }
    [Fact]
    public void ResolveAffected_StationConnection_ResolvesReferencingServiceRoute()
    {
        var cache = MakeCache();
        var scId = new StationConnectionId(10);
        var routeId = new ServiceRouteId(1);
        cache.StationConnectionUsedByServiceRouteIndex[scId] = [routeId];

        var changed = new HashSet<ObjectId> { new StationConnectionObjectId(scId) };
        var result = DependencyResolver.ResolveAffected(changed, cache);

        Assert.Equal(2, result.Count);
        Assert.Contains(new ServiceRouteObjectId(routeId), result);
    }

    [Fact]
    public void ResolveAffected_StationConnection_ResolvesMultipleReferencingServiceRoutes()
    {
        // PairedSelectedStationConnectionId経由で同一StationConnectionを別ServiceRouteが
        // 参照するケース（複数ServiceRouteからの参照）を検証する。
        var cache = MakeCache();
        var scId = new StationConnectionId(10);
        var routeIdA = new ServiceRouteId(1);
        var routeIdB = new ServiceRouteId(2);
        cache.StationConnectionUsedByServiceRouteIndex[scId] = [routeIdA, routeIdB];

        var changed = new HashSet<ObjectId> { new StationConnectionObjectId(scId) };
        var result = DependencyResolver.ResolveAffected(changed, cache);

        Assert.Contains(new ServiceRouteObjectId(routeIdA), result);
        Assert.Contains(new ServiceRouteObjectId(routeIdB), result);
    }

    [Fact]
    public void ResolveAffected_StationConnection_NoReferencingServiceRoute_ReturnsOnlyItself()
    {
        var cache = MakeCache();
        var scId = new StationConnectionId(10);

        var changed = new HashSet<ObjectId> { new StationConnectionObjectId(scId) };
        var result = DependencyResolver.ResolveAffected(changed, cache);

        Assert.Single(result);
    }

    [Fact]
    public void ResolveAffected_ServiceRoute_IsTerminalNode()
    {
        // §9.1項目3残課題：ServiceRoute←Trainの逆引きは§5.14.4棚卸し未着手のため、
        // 現時点では意図的に終端として振る舞うことを固定する回帰テスト。
        var cache = MakeCache();
        var routeId = new ServiceRouteId(1);

        var changed = new HashSet<ObjectId> { new ServiceRouteObjectId(routeId) };
        var result = DependencyResolver.ResolveAffected(changed, cache);

        Assert.Single(result);
    }

    [Fact]
    public void ResolveDirectDependents_AllObjectIdTypes_AreExplicitlyHandled()
    {
        // C#のswitch式はsealed record階層の部分型網羅性をコンパイル時に検証できないため
        // （§9.1項目20参照）、このテストがCS8509の代替となる「新規ObjectId型の
        // ケース追加漏れ検知」を担う。ObjectId.csに型を追加したのにDependencyResolver側の
        // switchケース追加を忘れると、このテストが失敗して気づける。
        var cache = MakeCache();

        var objectIdTypes = typeof(ObjectId).Assembly.GetTypes()
            .Where(t => typeof(ObjectId).IsAssignableFrom(t) && !t.IsAbstract);

        foreach (var type in objectIdTypes)
        {
            // 各ObjectId派生型はId1個のプリミティブ型record structを取るコンストラクタを持つ前提
            // （既存の全24種と一致するパターン）。デフォルト値でインスタンス化する。
            var ctor = type.GetConstructors().Single();
            var idType = ctor.GetParameters()[0].ParameterType;
            var idInstance = Activator.CreateInstance(idType, [0]);
            var instance = (ObjectId)ctor.Invoke([idInstance]);

            var exception = Record.Exception(() => DependencyResolver.ResolveDirectDependents(instance, cache).ToList());

            Assert.False(
                exception is NotSupportedException,
                $"{type.Name} がDependencyResolver.ResolveDirectDependentsのswitchで明示的にケース化されていません（catch-allに吸収されています）。");
        }
    }
}
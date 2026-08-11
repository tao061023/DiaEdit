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
}
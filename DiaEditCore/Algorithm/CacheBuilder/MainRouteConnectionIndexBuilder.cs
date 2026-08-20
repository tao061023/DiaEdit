namespace DiaEditCore.Algorithm.CacheBuilder;

using DiaEditCore.Model;
using DiaEditCore.Model.Routes;

/// <summary>
/// TimeTableSetCache.MainRouteConnectionIndex（MainRouteId→それを参照するStationConnectionの一覧）の構築を担う。
///
/// StationConnection.MainRouteIdはStationConnection自身が直接保持する属性のため、
/// EntryPointSequenceResolver等の展開処理を経由せず、StationConnection列を1回走査するだけで導出できる
/// </summary>
public static class MainRouteConnectionIndexBuilder
{
    public static Dictionary<MainRouteId, List<StationConnectionId>> Build(
        IEnumerable<StationConnection> allStationConnections)
    {
        var index = new Dictionary<MainRouteId, List<StationConnectionId>>();

        foreach (var sc in allStationConnections)
        {
            if (!index.TryGetValue(sc.MainRouteId, out var list))
            {
                list = new List<StationConnectionId>();
                index[sc.MainRouteId] = list;
            }
            list.Add(sc.Id);
        }

        return index;
    }
}
namespace DiaEditCore.Algorithm.CacheBuilder;

using DiaEditCore.Model;
using DiaEditCore.Model.Routes;

/// <summary>
/// TimeTableSetCache.ScsUsedByIndex（StationConnectionSegmentId→それを含むStationConnectionの一覧）の構築を担う。
///
/// StationConnection.Segments（SCSId配列）を1回走査するだけで導出できる逆引きインデックス
/// （複々線等でSCSが複数のStationConnectionから共有されうるため、値はList）。
/// v12.18で判明した「RebuildAllが空のまま」だった6インデックスのうちの1つ。
/// 消費者はDependencyResolver.ResolveDirectDependents（StationConnectionSegmentObjectIdケースの
/// うちStationConnection逆引き部分）のみ。
/// </summary>
public static class ScsUsedByIndexBuilder
{
    public static Dictionary<StationConnectionSegmentId, List<StationConnectionId>> Build(
        IEnumerable<StationConnection> allStationConnections)
    {
        var index = new Dictionary<StationConnectionSegmentId, List<StationConnectionId>>();

        foreach (var sc in allStationConnections)
        {
            foreach (var segId in sc.Segments)
            {
                if (!index.TryGetValue(segId, out var list))
                {
                    list = new List<StationConnectionId>();
                    index[segId] = list;
                }
                list.Add(sc.Id);
            }
        }

        return index;
    }
}
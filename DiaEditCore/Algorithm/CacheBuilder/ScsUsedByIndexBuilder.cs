namespace DiaEditCore.Algorithm.CacheBuilder;

using DiaEditCore.Model;
using DiaEditCore.Model.Routes;

/// <summary>
/// TimeTableSetCache.ScsUsedByIndex（StationConnectionSegmentId→それを含むStationConnectionの一覧）の構築を担う。
///
/// StationConnection.Segments（SCSId配列）を1回走査するだけで導出できる逆引きインデックス
/// （値がListである理由：SCは「複々線・双単線区間における経路をユーザーが分かりやすいようにグルーピングするための用途」であり、
/// 同一(MainRouteId, Direction)内で同一SCSが複数のStationConnectionから参照されることは許容しない（複線区間は物理的に1本の
/// 経路しか持たないため）。SCSが複数のStationConnectionから共有されうるのは、あくまで双単線区間
/// （同一SCSを上り方向SCと下り方向SCの双方が参照する。すなわちDirectionが異なるケースのみ）。
/// 同一方向内での重複はStationConnectionSegmentOverlapValidatorが検証対象とする。
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
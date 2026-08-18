namespace DiaEditCore.Algorithm.CacheBuilder;

using DiaEditCore.Model;
using DiaEditCore.Model.Routes;

/// <summary>
/// TimeTableSetCache.StationUsedByMainRouteIndex（StationId→それをStationOrderに含むMainRouteの一覧）の構築を担う。
///
/// MainRoute.StationOrderはMainRoute自身が直接保持する属性のため、MainRoute列を1回走査するだけで導出できる
/// （§9.1項目6の監査で判明：DeleteStationCommandがMainRoute.StationOrder経由の直接参照を
/// 一切チェックしていなかった欠落の是正。消費者はDependencyResolver.ResolveDirectDependents
/// （StationObjectIdケース）のみ）。
/// </summary>
public static class StationUsedByMainRouteIndexBuilder
{
    public static Dictionary<StationId, List<MainRouteId>> Build(IEnumerable<MainRoute> allMainRoutes)
    {
        var index = new Dictionary<StationId, List<MainRouteId>>();

        foreach (var mainRoute in allMainRoutes)
        {
            foreach (var stationId in mainRoute.StationOrder)
            {
                if (!index.TryGetValue(stationId, out var list))
                {
                    list = new List<MainRouteId>();
                    index[stationId] = list;
                }

                // 同一MainRoute内で同一駅を複数回通過するケース（環状線・デルタ線折返し等）で
                // 重複登録しないようにする。
                if (!list.Contains(mainRoute.Id))
                {
                    list.Add(mainRoute.Id);
                }
            }
        }

        return index;
    }
}
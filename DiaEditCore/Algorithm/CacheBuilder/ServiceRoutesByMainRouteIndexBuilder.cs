namespace DiaEditCore.Algorithm.CacheBuilder;

using DiaEditCore.Model;
using DiaEditCore.Model.Routes;

/// <summary>
/// TimeTableSetCache.ServiceRoutesByMainRouteIndex（MainRouteId→経由するServiceRouteの一覧）の
/// 構築を担う。元はServiceRouteStationOrderResolver.BuildServiceRoutesByMainRouteIndexだったが、
/// キャッシュ構築処理と駅順序解決アルゴリズムの責務分離のため分離した（v12.18）。
///
/// 用途はUI表示専用（MainRoute編集時の影響範囲表示）に限定し、DependencyResolverの
/// AffectedIds算出には使わない（6.1節参照）。
/// </summary>
public static class ServiceRoutesByMainRouteIndexBuilder
{
    public static Dictionary<MainRouteId, List<ServiceRouteId>> Build(
        IReadOnlyList<ServiceRoute> allServiceRoutes)
    {
        var index = new Dictionary<MainRouteId, List<ServiceRouteId>>();

        foreach (var sr in allServiceRoutes)
        {
            foreach (var seg in sr.Segments)
            {
                if (!index.TryGetValue(seg.MainRouteId, out var list))
                {
                    list = new List<ServiceRouteId>();
                    index[seg.MainRouteId] = list;
                }

                if (!list.Contains(sr.Id))
                {
                    list.Add(sr.Id);
                }
            }
        }

        return index;
    }
}
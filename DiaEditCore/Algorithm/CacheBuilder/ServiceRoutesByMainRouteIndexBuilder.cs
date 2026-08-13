namespace DiaEditCore.Algorithm.CacheBuilder;

using DiaEditCore.Model;
using DiaEditCore.Model.Routes;

/// <summary>
/// TimeTableSetCache.ServiceRoutesByMainRouteIndex（MainRouteId→経由するServiceRouteの一覧）の構築を担う。
/// 
/// 用途はUI表示専用（MainRoute編集時の影響範囲表示）に限定し、DependencyResolverの
/// AffectedIds算出には使わない。
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
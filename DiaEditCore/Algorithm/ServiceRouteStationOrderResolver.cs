using DiaEditCore.Model;
using DiaEditCore.Model.Routes;

namespace DiaEditCore.Algorithm;

/// <summary>
/// ServiceRouteが通る駅の順序付き全リスト（境界駅だけでなく中間駅も含む）を返す
/// 非永続の導出処理。基準列車選択UIの停車パターン表示のために追加する。
/// 都度導出・非保存。
/// </summary>
public static class ServiceRouteStationOrderResolver
{
    public static IReadOnlyList<StationId> ResolveServiceRouteStationOrder(
        ServiceRoute sr,
        IReadOnlyList<MainRoute> allMainRoutes)
    {
        return ResolveWithMainRoute(sr, allMainRoutes).Select(x => x.StationId).ToList();
    }

    /// <summary>
    /// resolveServiceRouteStationOrderと同じ駅列に加え、各駅がどのSegment（MainRoute）由来かを返す。
    /// StopPatternResolverが駅表示名の解決にMainRoute.StationDisplayNameOverridesを
    /// 適用する際に使用する。境界駅（前Segmentの終端と重複する駅）は、重複除去の結果として
    /// 前Segment側のMainRouteIdが採用される。
    /// </summary>
    public static IReadOnlyList<(StationId StationId, MainRouteId MainRouteId)> ResolveServiceRouteStationMainRoutes(
        ServiceRoute sr,
        IReadOnlyList<MainRoute> allMainRoutes)
    {
        return ResolveWithMainRoute(sr, allMainRoutes);
    }

    private static List<(StationId StationId, MainRouteId MainRouteId)> ResolveWithMainRoute(
        ServiceRoute sr,
        IReadOnlyList<MainRoute> allMainRoutes)
    {
        var result = new List<(StationId StationId, MainRouteId MainRouteId)>();

        foreach (var seg in sr.Segments)
        {
            var mainRoute = allMainRoutes.FirstOrDefault(mr => mr.Id == seg.MainRouteId);
            if (mainRoute is null) continue; // 参照整合性エラーは保存時検証で別途検出する想定

            var stations = ExtractRange(mainRoute.StationOrder, seg.FromStationIndex, seg.ToStationIndex);
            if (stations.Count == 0) continue;

            if (result.Count > 0)
            {
                // 前segmentの終端（境界駅）と重複するため、今回segmentの先頭要素を除く
                stations.RemoveAt(0);
            }

            result.AddRange(stations.Select(st => (st, seg.MainRouteId)));
        }

        return result;
    }

    private static List<StationId> ExtractRange(IReadOnlyList<StationId> stationOrder, int fromIndex, int toIndex)
    {
        var result = new List<StationId>();
        if (fromIndex < 0 || fromIndex >= stationOrder.Count ||
            toIndex < 0 || toIndex >= stationOrder.Count)
        {
            return result; // 範囲外インデックスは空扱い（保存時検証で別途検出する想定）
        }

        if (fromIndex <= toIndex)
        {
            for (var i = fromIndex; i <= toIndex; i++) result.Add(stationOrder[i]);
        }
        else
        {
            for (var i = fromIndex; i >= toIndex; i--) result.Add(stationOrder[i]);
        }

        return result;
    }
}

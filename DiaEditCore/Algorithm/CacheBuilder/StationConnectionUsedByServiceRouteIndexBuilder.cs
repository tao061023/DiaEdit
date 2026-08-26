namespace DiaEditCore.Algorithm.CacheBuilder;

using DiaEditCore.Model;
using DiaEditCore.Model.Routes;

/// <summary>
/// TimeTableSetCache.StationConnectionUsedByServiceRouteIndex（StationConnectionId→それを
/// SelectedStationConnectionId／PairedSelectedStationConnectionIdとして参照するServiceRouteの一覧）
/// の構築を担う。
///
/// 参照元は§5.13.2「参照関係表」の
/// 「ServiceRouteSegment | SelectedStationConnectionId／PairedSelectedStationConnectionId | StationConnection」。
/// 両フィールドともnull許容（候補1件の区間は自動採用のためSelected系は未設定のまま）なので、
/// null値はインデックスへ加えない。
///
/// §5.14.4棚卸し表に記載の`StationConnectionUsedByServiceRouteIndexBuilder`候補を実装したもの。
/// 消費者はDependencyResolver.ResolveDirectDependents（StationConnectionObjectIdケース）のみ
/// （§9.1項目3）。
/// </summary>
public static class StationConnectionUsedByServiceRouteIndexBuilder
{
    public static Dictionary<StationConnectionId, List<ServiceRouteId>> Build(
        IEnumerable<ServiceRoute> allServiceRoutes)
    {
        var index = new Dictionary<StationConnectionId, List<ServiceRouteId>>();

        foreach (var route in allServiceRoutes)
        {
            foreach (var segment in route.Segments)
            {
                if (segment.SelectedStationConnectionId is { } scId)
                {
                    Add(index, scId, route.Id);
                }

                if (segment.PairedSelectedStationConnectionId is { } pairedScId)
                {
                    Add(index, pairedScId, route.Id);
                }
            }
        }

        return index;
    }

    private static void Add(
        Dictionary<StationConnectionId, List<ServiceRouteId>> index,
        StationConnectionId stationConnectionId,
        ServiceRouteId serviceRouteId)
    {
        if (!index.TryGetValue(stationConnectionId, out var list))
        {
            list = new List<ServiceRouteId>();
            index[stationConnectionId] = list;
        }

        // 同一ServiceRoute内で複数Segmentが同一StationConnectionを指すことは通常ないが、
        // 万一の重複時にAffectedIds側のHashSetでの吸収に任せるため、ここでは重複除去しない
        // （StationUsedBySegmentIndexBuilder等、既存Builder群と同じ簡潔優先の方針）。
        list.Add(serviceRouteId);
    }
}
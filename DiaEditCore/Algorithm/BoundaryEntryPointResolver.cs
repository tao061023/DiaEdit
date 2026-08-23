using DiaEditCore.Model;
using DiaEditCore.Model.Routes;

namespace DiaEditCore.Algorithm;

/// <summary>
/// 境界駅のEPを、既存のEntryPointSequenceResolverの結果から取り出すためのラッパー。 <br/>
/// ServiceRoutePathResolver・ReversalResolverが下請けとして利用する。 <br/>
/// 都度導出・非保存。複々線等で対応するStationConnectionが複数存在しうるため、 <br/>
/// 該当する全候補を返す（列車種別ごとに利用可能なEPが異なりうるため、この段階では絞り込まない）。
/// </summary>
public static class BoundaryEntryPointResolver
{
    public static IReadOnlyList<EntryPointSequenceElement> ResolveBoundaryEntryPoint(
        MainRouteId mainRouteId,
        int fromIndex,
        int toIndex,
        IReadOnlyList<MainRoute> allMainRoutes,
        IReadOnlyList<StationConnection> allStationConnections,
        IReadOnlyList<StationConnectionSegment> allSegments)
    {
        var mainRoute = allMainRoutes.FirstOrDefault(mr => mr.Id == mainRouteId);
        if (mainRoute is null) return Array.Empty<EntryPointSequenceElement>();

        var stationOrder = mainRoute.StationOrder;
        if (fromIndex < 0 || fromIndex >= stationOrder.Count ||
            toIndex < 0 || toIndex >= stationOrder.Count ||
            fromIndex == toIndex)
        {
            return Array.Empty<EntryPointSequenceElement>();
        }

        var direction = fromIndex < toIndex
            ? StationConnectionDirection.Down
            : StationConnectionDirection.Up;

        var expectedStations = BuildExpectedStations(stationOrder, fromIndex, toIndex);

        var result = new List<EntryPointSequenceElement>();
        foreach (var sc in allStationConnections)
        {
            if (sc.MainRouteId != mainRouteId || sc.Direction != direction) continue;

            // v12.29系統(ii)対応：向き解決にMainRoute.StationOrderを要するためallMainRoutesを渡す
            var seq = EntryPointSequenceResolver.Resolve(sc, allSegments, allMainRoutes);
            if (!MatchesExpectedStations(seq, expectedStations)) continue;

            result.Add(seq[^1]);
        }

        return result;
    }

    public static IReadOnlyList<StationConnectionId> ResolveBoundaryStationConnection(
        MainRouteId mainRouteId,
        int fromIndex,
        int toIndex,
        IReadOnlyList<MainRoute> allMainRoutes,
        IReadOnlyList<StationConnection> allStationConnections,
        IReadOnlyList<StationConnectionSegment> allSegments)
    {
        var mainRoute = allMainRoutes.FirstOrDefault(mr => mr.Id == mainRouteId);
        if (mainRoute is null) return Array.Empty<StationConnectionId>();

        var stationOrder = mainRoute.StationOrder;
        if (fromIndex < 0 || fromIndex >= stationOrder.Count ||
            toIndex < 0 || toIndex >= stationOrder.Count ||
            fromIndex == toIndex)
        {
            return Array.Empty<StationConnectionId>();
        }

        var direction = fromIndex < toIndex
            ? StationConnectionDirection.Down
            : StationConnectionDirection.Up;

        var expectedStations = BuildExpectedStations(stationOrder, fromIndex, toIndex);

        var result = new List<StationConnectionId>();
        foreach (var sc in allStationConnections)
        {
            if (sc.MainRouteId != mainRouteId || sc.Direction != direction) continue;

            var seq = EntryPointSequenceResolver.Resolve(sc, allSegments, allMainRoutes);
            if (!MatchesExpectedStations(seq, expectedStations)) continue;

            result.Add(sc.Id);
        }

        return result;
    }

    private static List<StationId> BuildExpectedStations(IReadOnlyList<StationId> stationOrder, int fromIndex, int toIndex)
    {
        var stations = new List<StationId>();
        if (fromIndex < toIndex)
        {
            for (var i = fromIndex; i <= toIndex; i++) stations.Add(stationOrder[i]);
        }
        else
        {
            for (var i = fromIndex; i >= toIndex; i--) stations.Add(stationOrder[i]);
        }
        return stations;
    }

    /// <summary>
    /// EntryPointSequenceElement列が示す駅の連鎖（Fromの先頭→各要素のTo）が
    /// expectedStationsと完全一致するかを検証する。
    /// </summary>
    private static bool MatchesExpectedStations(
        IReadOnlyList<EntryPointSequenceElement> seq,
        IReadOnlyList<StationId> expectedStations)
    {
        if (seq.Count != expectedStations.Count - 1) return false;
        if (seq.Count == 0) return false;

        if (seq[0].FromStationId != expectedStations[0]) return false;

        for (var i = 0; i < seq.Count; i++)
        {
            if (seq[i].ToStationId != expectedStations[i + 1]) return false;
            if (i > 0 && seq[i].FromStationId != seq[i - 1].ToStationId) return false;
        }

        return true;
    }
}
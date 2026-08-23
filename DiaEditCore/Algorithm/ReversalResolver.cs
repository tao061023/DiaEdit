using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.Stations;

namespace DiaEditCore.Algorithm;

/// <summary>
/// 編成前後反転の自動導出。単一MainRoute内のスイッチバック判定（ResolveDirectionReversalStations）と、
/// 境界駅（MainRoute間）での折り返し判定（ResolveReversesAtBoundary）を、同一の判定基準で扱う。
/// （クラス冒頭のコメントは既存版から変更なし。以下メソッド本体のみv12.29対応）
/// </summary>
public static class ReversalResolver
{
    public static Dictionary<StationId, bool> ResolveDirectionReversalStations(
        MainRoute mainRoute,
        IReadOnlyList<MainRoute> allMainRoutes,
        IReadOnlyList<StationConnection> allStationConnections,
        IReadOnlyList<StationConnectionSegment> allSegments,
        IReadOnlyList<Rail> allRails,
        IReadOnlyDictionary<StationId, IReadOnlyList<StationPath>> stationPathsByStation)
    {
        var result = new Dictionary<StationId, bool>();
        var stationOrder = mainRoute.StationOrder;
        var railResolver = new RailSequenceResolver(allRails);

        for (var i = 1; i < stationOrder.Count - 1; i++)
        {
            var stationId = stationOrder[i];
            if (!stationPathsByStation.TryGetValue(stationId, out var pathsAtStation) || pathsAtStation.Count == 0)
            {
                continue;
            }

            var arrivingCandidates = BoundaryEntryPointResolver.ResolveBoundaryEntryPoint(
                mainRoute.Id, i - 1, i, allMainRoutes, allStationConnections, allSegments);

            var departingCandidates = ResolveDepartureEntryPointCandidates(
                mainRoute.Id, i, i + 1, allMainRoutes, allStationConnections, allSegments);

            var reversal = JudgeReversalByTrack(arrivingCandidates, departingCandidates, pathsAtStation, railResolver, allRails);
            if (reversal is not null)
            {
                result[stationId] = reversal.Value;
            }
        }

        return result;
    }

    public static bool? ResolveReversesAtBoundary(
        ServiceRouteSegment prevSegment,
        ServiceRouteSegment nextSegment,
        IReadOnlyList<MainRoute> allMainRoutes,
        IReadOnlyList<StationConnection> allStationConnections,
        IReadOnlyList<StationConnectionSegment> allSegments,
        IReadOnlyList<Rail> allRails,
        IReadOnlyList<StationPath> pathsAtBoundaryStation)
    {
        var prevMainRoute = allMainRoutes.FirstOrDefault(mr => mr.Id == prevSegment.MainRouteId);
        var nextMainRoute = allMainRoutes.FirstOrDefault(mr => mr.Id == nextSegment.MainRouteId);
        if (prevMainRoute is null || nextMainRoute is null) return null;

        var boundaryStationId = SafeStationAt(prevMainRoute, prevSegment.ToStationIndex);
        var nextStartStationId = SafeStationAt(nextMainRoute, nextSegment.FromStationIndex);
        if (boundaryStationId is null || nextStartStationId is null || boundaryStationId != nextStartStationId)
        {
            return null;
        }

        if (pathsAtBoundaryStation.Count == 0) return null;

        var prevStep = Math.Sign(prevSegment.ToStationIndex - prevSegment.FromStationIndex);
        if (prevStep == 0) return null;
        var prevPenultimateIndex = prevSegment.ToStationIndex - prevStep;

        var nextStep = Math.Sign(nextSegment.ToStationIndex - nextSegment.FromStationIndex);
        if (nextStep == 0) return null;
        var nextFollowingIndex = nextSegment.FromStationIndex + nextStep;

        var arrivingCandidates = BoundaryEntryPointResolver.ResolveBoundaryEntryPoint(
            prevSegment.MainRouteId, prevPenultimateIndex, prevSegment.ToStationIndex,
            allMainRoutes, allStationConnections, allSegments);

        var departingCandidates = ResolveDepartureEntryPointCandidates(
            nextSegment.MainRouteId, nextSegment.FromStationIndex, nextFollowingIndex,
            allMainRoutes, allStationConnections, allSegments);

        var railResolver = new RailSequenceResolver(allRails);
        return JudgeReversalByTrack(arrivingCandidates, departingCandidates, pathsAtBoundaryStation, railResolver, allRails);
    }

    private static bool? JudgeReversalByTrack(
        IReadOnlyList<EntryPointSequenceElement> arrivingCandidates,
        IReadOnlyList<EntryPointSequenceElement> departingCandidates,
        IReadOnlyList<StationPath> pathsAtStation,
        RailSequenceResolver railResolver,
        IReadOnlyList<Rail> allRails)
    {
        if (arrivingCandidates.Count == 0 || departingCandidates.Count == 0 || pathsAtStation.Count == 0)
        {
            return null;
        }

        foreach (var arriving in arrivingCandidates)
        {
            var arrivalTrackKeys = ResolveTrackEndpointKeys(
                pathsAtStation, arriving.ToEntryPointId, StationPathDirection.Arrival, railResolver, allRails);
            if (arrivalTrackKeys.Count == 0) continue;

            foreach (var departing in departingCandidates)
            {
                var departureTrackKeys = ResolveTrackEndpointKeys(
                    pathsAtStation, departing.FromEntryPointId, StationPathDirection.Departure, railResolver, allRails);

                if (departureTrackKeys.Overlaps(arrivalTrackKeys))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static HashSet<ObjectId> ResolveTrackEndpointKeys(
        IReadOnlyList<StationPath> pathsAtStation,
        EntryPointId entryPointId,
        StationPathDirection direction,
        RailSequenceResolver railResolver,
        IReadOnlyList<Rail> allRails)
    {
        var keys = new HashSet<ObjectId>();

        var matchingPaths = pathsAtStation.Where(p =>
            p.Direction == direction &&
            p.Waypoints.Any(w => w is EntryPointWaypoint ep && ep.Id == entryPointId));

        foreach (var path in matchingPaths)
        {
            IReadOnlyList<RailId> railIds;
            try
            {
                railIds = railResolver.Resolve(path);
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            foreach (var railId in railIds)
            {
                var rail = allRails.FirstOrDefault(r => r.Id == railId);
                if (rail is null || rail.Role != RailRole.Track) continue;

                if (rail.EndpointA.ToObjectId() is { } a) keys.Add(a);
                if (rail.EndpointB.ToObjectId() is { } b) keys.Add(b);
            }
        }

        return keys;
    }

    /// <summary>
    /// BoundaryEntryPointResolver.ResolveBoundaryEntryPointと同じ絞り込み条件で一致するStationConnectionを探索し、
    /// fromIndex側（出発側）の要素（EntryPointSequenceElement列の先頭要素）を返す。
    /// v12.29対応：EntryPointSequenceResolver.Resolveが系統(ii)化（allMainRoutes必須）されたため、
    /// 本メソッドは元々allMainRoutesを既に引数に持っていたのでそのまま渡すだけで済む。
    /// </summary>
    private static IReadOnlyList<EntryPointSequenceElement> ResolveDepartureEntryPointCandidates(
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

        var direction = fromIndex < toIndex ? StationConnectionDirection.Down : StationConnectionDirection.Up;
        var expectedStations = BuildExpectedStations(stationOrder, fromIndex, toIndex);

        var result = new List<EntryPointSequenceElement>();
        foreach (var sc in allStationConnections)
        {
            if (sc.MainRouteId != mainRouteId || sc.Direction != direction) continue;

            var seq = EntryPointSequenceResolver.Resolve(sc, allSegments, allMainRoutes);
            if (!MatchesExpectedStations(seq, expectedStations)) continue;

            result.Add(seq[0]);
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

    private static StationId? SafeStationAt(MainRoute mainRoute, int index)
        => index >= 0 && index < mainRoute.StationOrder.Count ? mainRoute.StationOrder[index] : null;
}
namespace DiaEditCore.Algorithm;

using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.Stations;

/// <summary>
/// 編成前後反転の自動導出。単一MainRoute内のスイッチバック判定（ResolveDirectionReversalStations）と、
/// 境界駅（MainRoute間）での折り返し判定（ResolveReversesAtBoundary）を、同一の判定基準で扱う。
///
/// 判定基準：SCSから得た進入側EntryPointId・進出側EntryPointIdそれぞれについて、それを含む
/// StationPath（Direction=Arrival／Departure）を列挙し、そのStationPathが経由するRailRole.Track
/// のRail群（EndpointA/EndpointBの RailEndpointRef）を集合として取り出す。進入側・進出側の集合に
/// 重複するRailEndpointRefが存在すれば、進入と進出で同一の物理番線を使用している＝折り返しが必須と
/// 判定する（デルタ線は必ずMainRouteとして登録される制約があるため、使用する番線による向きの差異は
/// 発生せず、複数StationPath候補間の判定はORでよい）。
///
/// 両メソッドとも、EP引き当ての下請けとしてBoundaryEntryPointResolver・EntryPointSequenceResolver・
/// RailSequenceResolverを共有する。出力（directionReversalStations／ServiceRouteSegment.reversesAtBoundary）は
/// あくまで保存時のデフォルト値提示の候補であり、確定はユーザーが行う。
///
/// 注：Shunting時に使用Trackが変わるケース（RailRole.Shunting側のRailEndpointRef一致）は
/// 本メソッドの対象外（Arrival/Departure StationPathのみを扱う）。必要になった場合は別途対応する。
/// </summary>
public static class ReversalResolver
{
    /// <summary>
    /// mainRoute内の各中間駅（先頭・末尾を除く）について、スイッチバック判定を行う。
    /// 戻り値はStationId→判定結果（true=反転が必要と推定／false=不要と推定）。
    /// 判定不能（対応するStationConnectionが無い／該当駅のStationPathが渡されていない）駅は結果に含めない。
    /// </summary>
    /// <param name="stationPathsByStation">駅ごとのStationPath一覧（呼び出し側でFloorUnitId→StationId対応により絞り込み済みのもの）</param>
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
                continue; // 判定不能
            }

            // 進入側：SCS[i-1→i]（Down方向）の到着EP（末尾要素のToEntryPointId）
            var arrivingCandidates = BoundaryEntryPointResolver.ResolveBoundaryEntryPoint(
                mainRoute.Id, i - 1, i, allMainRoutes, allStationConnections, allSegments);

            // 進出側：SCS[i→i+1]（Down方向）の出発EP（先頭要素のFromEntryPointId）
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

    /// <summary>
    /// 境界駅（ServiceRouteSegmentの境界）における折り返し要否を判定する。
    /// prevSegmentの終端駅とnextSegmentの起点駅が同一駅であることを前提とする
    /// （異なる場合はnullを返し、判定不能として扱う）。
    /// </summary>
    /// <param name="pathsAtBoundaryStation">境界駅のStationPath一覧</param>
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

    /// <summary>
    /// 進入側候補（ToEntryPointId基準）・進出側候補（FromEntryPointId基準）の全組み合わせについて、
    /// それぞれが経由するRailRole.Track Railの端点集合（RailEndpointRef）に重複があるかを判定する。
    /// いずれかの組み合わせで重複があればtrue（OR）。候補やStationPathが無ければnull（判定不能）。
    /// </summary>
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

    /// <summary>
    /// entryPointIdをWaypointsに含み、指定方向を持つStationPathを列挙し、
    /// それらが経由するRailRole.Track RailのEndpointA/EndpointB（RailEndpointRef）のKey集合を返す。
    /// </summary>
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
                // waypoint間のRailが存在しない不整合データは別途保存時検証で検出する想定のため、ここではスキップする
                continue;
            }

            foreach (var railId in railIds)
            {
                var rail = allRails.FirstOrDefault(r => r.Id == railId);
                if (rail is null || rail.Role != RailRole.Track) continue;

                // NoneEndpointRef（未接続端部）はToObjectId()がnullを返すため、
                // keysに加えない。None同士は「同一地点を共有している」とはみなさない。
                if (rail.EndpointA.ToObjectId() is { } a) keys.Add(a);
                if (rail.EndpointB.ToObjectId() is { } b) keys.Add(b);
            }
        }

        return keys;
    }

    /// <summary>
    /// BoundaryEntryPointResolver.ResolveBoundaryEntryPointと同じ絞り込み条件で一致するStationConnectionを探索し、
    /// fromIndex側（出発側）の要素（EntryPointSequenceElement列の先頭要素）を返す。
    /// BoundaryEntryPointResolverは到着側（末尾要素）しか返さないため、出発側取得用に複製している。
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

            var seq = EntryPointSequenceResolver.Resolve(sc, allSegments);
            if (!MatchesExpectedStations(seq, expectedStations)) continue;

            result.Add(seq[0]); // fromIndex側（出発側）の要素
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
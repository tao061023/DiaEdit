using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.TimeTable.Trains;

namespace DiaEditCore.Algorithm.Conflicts;

/// <summary>
/// StationConnectionSegment用途のConflictChecker。
/// v12.29対応：EntryPointSequenceCache.Buildの系統(ii)化に伴い、allMainRoutesを新規に受け取る。
/// （クラス冒頭の詳細コメントは既存版から変更なし）
///
/// v12.29追加修正：ホップ→SCS解決を「StationConnection.Segments[0]固定」から、
/// そのホップの実際の発着駅（TrainRunSegment.FromStationId/ToStationId）と一致するSCSを
/// EntryPointSequenceResolver.ResolveOriented（系統(i)、無向マッチング）で特定する方式へ変更した。
/// 旧実装は「1RunSegment=1SCS」を暗黙に仮定しており、複数ホップを1本のStationConnectionが
/// カバーする広域SC（本セッションでServiceRouteToRunSegmentsResolverが正式にサポートした構成。
/// 例：A→B→CをカバーするSCが、A→BとB→Cの両方のTrainRunSegmentから同一StationConnectionIdとして
/// 参照される）に対して、常にSegments[0]（＝A→B用のSCS）を誤って採用してしまい、
/// B→C区間の占有がA→B側のSCSに誤計上される・B→C側のSCSには一切計上されない、という
/// サイレントな不具合があった。
/// </summary>
public static class StationConnectionSegmentOccupancyProvider
{
    public static Dictionary<StationConnectionSegmentId, List<ConflictChecker.Occupancy>> BuildOccupancy(
        IReadOnlyList<Train> trains,
        IReadOnlyList<StationConnection> stationConnections,
        IReadOnlyList<StationConnectionSegment> allSegments,
        IReadOnlyList<MainRoute> allMainRoutes,
        IReadOnlyDictionary<StationPathId, StationPath> pathsById,
        IReadOnlyDictionary<(EntryPointId, RailId), StationPathId> arrivalIndex,
        IReadOnlyDictionary<(RailId, EntryPointId), StationPathId> departureIndex)
    {
        var result = new Dictionary<StationConnectionSegmentId, List<ConflictChecker.Occupancy>>();

        void Add(StationConnectionSegmentId scsId, ConflictChecker.Occupancy occ)
        {
            if (!result.TryGetValue(scsId, out var list))
                result[scsId] = list = new List<ConflictChecker.Occupancy>();
            list.Add(occ);
        }

        var resolveEp = EntryPointSequenceCache.Build(stationConnections, allSegments, allMainRoutes);
        var scById = stationConnections.ToDictionary(sc => sc.Id);

        foreach (var train in trains)
        {
            var segs = train.RunSegments;
            if (segs.Count == 0) continue;

            for (int i = 0; i < segs.Count; i++)
            {
                var departureVisit = StopVisitOccupancyResolver.Resolve(train, i, resolveEp, pathsById, arrivalIndex, departureIndex);
                var arrivalVisit = StopVisitOccupancyResolver.Resolve(train, i + 1, resolveEp, pathsById, arrivalIndex, departureIndex);

                if (departureVisit is not { DepartureEnd: { } start }) continue;
                if (arrivalVisit is not { ArrivalStart: { } end }) continue;

                var hop = segs[i];
                var sc = scById[hop.StationConnectionId];
                var scsId = ResolveHopSegmentId(sc, hop.FromStationId, hop.ToStationId, allSegments);
                if (scsId is null) continue; // データ不整合（保存時検証の管轄）。占有として計上しない

                Add(scsId.Value, new ConflictChecker.Occupancy(train.Id, start, end));
            }
        }

        return result;
    }

    /// <summary>
    /// StationConnection.Segmentsのうち、fromStationId/toStationIdに一致するSCSを1件特定する。
    /// EntryPointSequenceResolver.ResolveOriented（系統(i)、無向マッチング）を用いるため、
    /// 双単線区間でA/Bの向きが反転していても正しく一致する。
    /// 一致が0件・複数件の場合はnull（呼び出し側でそのホップを占有計上から除外する）。
    /// </summary>
    private static StationConnectionSegmentId? ResolveHopSegmentId(
        StationConnection sc,
        StationId fromStationId,
        StationId toStationId,
        IReadOnlyList<StationConnectionSegment> allSegments)
    {
        StationConnectionSegmentId? found = null;

        foreach (var segId in sc.Segments)
        {
            var seg = allSegments.FirstOrDefault(s => s.Id == segId);
            if (seg is null) continue;
            if (EntryPointSequenceResolver.ResolveOriented(seg, fromStationId, toStationId) is null) continue;

            if (found is not null) return null; // 複数一致は不整合として扱う
            found = seg.Id;
        }

        return found;
    }
}
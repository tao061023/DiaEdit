using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.TimeTable.Trains;

namespace DiaEditCore.Algorithm.Conflicts;

/// <summary>
/// StationConnectionSegment（駅間）用途のConflictChecker（6.5節）：全Trainを走査し、
/// SCSごとの占有区間を構築する。
///
/// 「1 RunSegment＝1 SCS」が保証されている前提（実装セッションで確定）のため、
/// EntryPointSequenceResolverによる複数SCS展開は不要で、StationConnection.Segments[0]を
/// そのまま対象SCSIdとして採用できる。
///
/// 占有区間 = 出発駅側DepartureStationPathの占有終了 〜 到着駅側ArrivalStationPathの占有開始
/// （Track用途と同様、StopVisitOccupancyResolverの計算をそのまま再利用する）。
///
/// 注：現時点ではTrack用途と異なりPrevTrain/NextTrainによる境界延長は行わない
/// （RunSegmentの内部区間であるため、始発・終着側の欠落は発生しない＝visitI/visitI+1は
/// 常にTrain自身のRunSegments内で両方求まる）。
/// </summary>
public static class StationConnectionSegmentOccupancyProvider
{
    public static Dictionary<StationConnectionSegmentId, List<ConflictChecker.Occupancy>> BuildOccupancy(
        IReadOnlyList<Train> trains,
        IReadOnlyList<StationConnection> stationConnections,
        IReadOnlyList<StationConnectionSegment> allSegments,
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

        var resolveEp = EntryPointSequenceCache.Build(stationConnections, allSegments);
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

                var scsId = scById[segs[i].StationConnectionId].Segments[0];
                Add(scsId, new ConflictChecker.Occupancy(train.Id, start, end));
            }
        }

        return result;
    }
}
using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.TimeTable.Trains;

namespace DiaEditCore.Algorithm.Conflicts;

/// <summary>
/// StationConnectionSegment用途のConflictChecker。
/// v12.29対応：EntryPointSequenceCache.Buildの系統(ii)化に伴い、allMainRoutesを新規に受け取る。
/// （クラス冒頭の詳細コメントは既存版から変更なし）
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

                var scsId = scById[segs[i].StationConnectionId].Segments[0];
                Add(scsId, new ConflictChecker.Occupancy(train.Id, start, end));
            }
        }

        return result;
    }
}
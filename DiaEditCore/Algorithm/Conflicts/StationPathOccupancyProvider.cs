using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.TimeTable.Trains;

namespace DiaEditCore.Algorithm.Conflicts;

/// <summary>
/// 全Trainを走査し、StationPathごとの占有区間（ConflictChecker.Occupancy）を構築する。
/// v12.29対応：EntryPointSequenceResolver.Resolveが系統(ii)化（allMainRoutes必須）されたため、
/// 本ProviderもallMainRoutesを新規に受け取り、EntryPointSequenceCache.Buildへ渡す。
/// </summary>
public static class StationPathOccupancyProvider
{
    public static Dictionary<StationPathId, List<ConflictChecker.Occupancy>> BuildOccupancy(
        IReadOnlyList<Train> trains,
        IReadOnlyList<StationConnection> stationConnections,
        IReadOnlyList<StationConnectionSegment> allSegments,
        IReadOnlyList<MainRoute> allMainRoutes,   // ← 追加（第4引数に挿入。呼び出し元の引数順に注意）
        IReadOnlyDictionary<StationPathId, StationPath> pathsById,
        IReadOnlyDictionary<(EntryPointId, RailId), StationPathId> arrivalIndex,
        IReadOnlyDictionary<(RailId, EntryPointId), StationPathId> departureIndex)
        {
            var result = new Dictionary<StationPathId, List<ConflictChecker.Occupancy>>();

            void Add(StationPathId spId, ConflictChecker.Occupancy occ)
            {
                if (!result.TryGetValue(spId, out var list))
                    result[spId] = list = new List<ConflictChecker.Occupancy>();
                list.Add(occ);
            }

            var resolveEp = EntryPointSequenceCache.Build(stationConnections, allSegments, allMainRoutes);

            foreach (var train in trains)
            {
                var segs = train.RunSegments;
                if (segs.Count == 0) continue;

                for (int visitSeq = 0; visitSeq <= segs.Count; visitSeq++)
                {
                    var visit = StopVisitOccupancyResolver.Resolve(
                        train, visitSeq, resolveEp, pathsById, arrivalIndex, departureIndex);
                    if (visit is not { } v) continue;

                    if (v.ArrivalSpId is { } arrSpId && v.ArrivalStart is { } arrStart && v.ArrivalEnd is { } arrEnd)
                        Add(arrSpId, new ConflictChecker.Occupancy(train.Id, arrStart, arrEnd));

                    if (v.DepartureSpId is { } depSpId && v.DepartureStart is { } depStart && v.DepartureEnd is { } depEnd)
                        Add(depSpId, new ConflictChecker.Occupancy(train.Id, depStart, depEnd));
                }
            }

            return result;
        }
}

/// <summary>
/// StationConnectionIdごとのEntryPointSequence解決結果をメモ化するだけの小さなキャッシュ。
/// v12.29対応：EntryPointSequenceResolver.Resolveの系統(ii)化に伴いallMainRoutesが必須引数になった。
/// </summary>
public static class EntryPointSequenceCache
{
    public static Func<StationConnectionId, IReadOnlyList<EntryPointSequenceElement>> Build(
        IReadOnlyList<StationConnection> stationConnections,
        IReadOnlyList<StationConnectionSegment> allSegments,
        IReadOnlyList<MainRoute> allMainRoutes)
    {
        var connectionMap = stationConnections.ToDictionary(s => s.Id);
        var cache = new Dictionary<StationConnectionId, IReadOnlyList<EntryPointSequenceElement>>();
 
        return scId =>
        {
            if (cache.TryGetValue(scId, out var cached))
            {
                return cached;
            }
 
            if (connectionMap.TryGetValue(scId, out var connection))
            {
                return cache[scId] = EntryPointSequenceResolver.Resolve(connection, allSegments, allMainRoutes);
            }
 
            throw new KeyNotFoundException($"StationConnection with ID '{scId}' was not found.");
        };
    }
}
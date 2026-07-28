using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.TimeTable;

namespace DiaEditCore.Algorithm;

/// <summary>
/// 全Trainを走査し、StationPathごとの占有区間（ConflictChecker.Occupancy）を構築する。
/// 仮列車（IsProvisional=true）も対象に含める（6.5節・「仮列車も対象に含める」）。
///
/// 各訪問(visitSeq)ごとの到着/出発StationPath占有算出自体はStopVisitOccupancyResolverに
/// 切り出し済み（v11.23）。TrackOccupancyProviderと同一ロジックを共有する。
/// </summary>
public static class StationPathOccupancyProvider
{
    public static Dictionary<StationPathId, List<ConflictChecker.Occupancy>> BuildOccupancy(
        IReadOnlyList<Train> trains,
        IReadOnlyList<StationConnection> stationConnections,
        IReadOnlyList<StationConnectionSegment> allSegments,
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

        var resolveEp = EntryPointSequenceCache.Build(stationConnections, allSegments);

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
/// StationPathOccupancyProvider・TrackOccupancyProviderの双方から同一インスタンスを
/// 共有する必要はなく、都度Buildして良い(TimeTableSetCache側の責務ではない、6.5節用途限定の
/// 呼び出し内ローカルキャッシュ)。
/// 追記：StopVisitOccupancyResolverTests.csで外部呼出しが行われるためpublicとした。
/// </summary>
public static class EntryPointSequenceCache
{
    public static Func<StationConnectionId, IReadOnlyList<EntryPointSequenceElement>> Build(
        IReadOnlyList<StationConnection> stationConnections,
        IReadOnlyList<StationConnectionSegment> allSegments)
    {
        // 事前に Id から要素を引ける辞書を作っておく（First() のループを無くして高速化）
        var connectionMap = stationConnections.ToDictionary(s => s.Id);
        
        // シングルスレッド前提のため、通常の Dictionary でOK
        var cache = new Dictionary<StationConnectionId, IReadOnlyList<EntryPointSequenceElement>>();

        return scId =>
        {
            // すでにキャッシュにあればそれを返す
            if (cache.TryGetValue(scId, out var cached))
            {
                return cached;
            }

            // キャッシュにない場合、事前作成した辞書から O(1) で高速に取得して解決
            if (connectionMap.TryGetValue(scId, out var connection))
            {
                return cache[scId] = EntryPointSequenceResolver.Resolve(connection, allSegments);
            }

            // 元の First() と同様に、見つからない場合は例外を投げる
            throw new KeyNotFoundException($"StationConnection with ID '{scId}' was not found.");
        };
    }
}
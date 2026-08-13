namespace DiaEditCore.Algorithm.CacheBuilder;

using DiaEditCore.Model;
using DiaEditCore.Model.Routes;

/// <summary>
/// TimeTableSetCache.StationConnectionIndex（StationId→それを通るStationConnectionの一覧）と
/// TimeTableSetCache.EntryPointConnectionIndex（EntryPointId→それを通るStationConnectionの一覧）を
/// 同時に構築する。
///
/// 両インデックスとも、StationConnection自身は駅・EntryPointを直接保持せず、Segments（SCSId配列）を
/// 経由してのみ辿れる（EntryPointSequenceResolver.Resolveが担う展開処理）ため、
/// 1つのStationConnectionにつき1回のResolve呼び出しで双方の駅・EntryPoint集合を同時に取り出せる。
/// StationPathTrackIndexBuilder.Build（Arrival/DepartureIndexを同時返却）と同じ「関連する複数の
/// インデックスを1回の走査で導出する」設計パターンを踏襲し、SCS展開処理の重複実行を避けている。
///
/// v12.18で判明した「RebuildAllが空のまま」だった6インデックスのうち2つ。
/// 消費者はDependencyResolver.ResolveDirectDependents（StationObjectId／EntryPointObjectIdケース）のみ。
/// 同一StationConnectionが複数のセグメント区間で同一駅・同一EntryPointを重複して通る場合は
/// 呼び出し側での重複登録を避けるため、StationConnectionId単位でHashSetを介して一意化する。
/// </summary>
public static class StationAndEntryPointConnectionIndexBuilder
{
    public static (
        Dictionary<StationId, List<StationConnectionId>> StationConnectionIndex,
        Dictionary<EntryPointId, List<StationConnectionId>> EntryPointConnectionIndex
    ) Build(
        IEnumerable<StationConnection> allStationConnections,
        IReadOnlyList<StationConnectionSegment> allSegments)
    {
        var stationIndex = new Dictionary<StationId, List<StationConnectionId>>();
        var entryPointIndex = new Dictionary<EntryPointId, List<StationConnectionId>>();

        void AddStation(StationId stationId, StationConnectionId scId, HashSet<StationId> seen)
        {
            if (!seen.Add(stationId)) return; // 同一SC内での重複登録を防止
            if (!stationIndex.TryGetValue(stationId, out var list))
            {
                list = new List<StationConnectionId>();
                stationIndex[stationId] = list;
            }
            list.Add(scId);
        }

        void AddEntryPoint(EntryPointId entryPointId, StationConnectionId scId, HashSet<EntryPointId> seen)
        {
            if (!seen.Add(entryPointId)) return; // 同一SC内での重複登録を防止
            if (!entryPointIndex.TryGetValue(entryPointId, out var list))
            {
                list = new List<StationConnectionId>();
                entryPointIndex[entryPointId] = list;
            }
            list.Add(scId);
        }

        foreach (var sc in allStationConnections)
        {
            var seq = EntryPointSequenceResolver.Resolve(sc, allSegments);
            if (seq.Count == 0) continue;

            var seenStations = new HashSet<StationId>();
            var seenEntryPoints = new HashSet<EntryPointId>();

            foreach (var elem in seq)
            {
                AddStation(elem.FromStationId, sc.Id, seenStations);
                AddStation(elem.ToStationId, sc.Id, seenStations);
                AddEntryPoint(elem.FromEntryPointId, sc.Id, seenEntryPoints);
                AddEntryPoint(elem.ToEntryPointId, sc.Id, seenEntryPoints);
            }
        }

        return (stationIndex, entryPointIndex);
    }
}
namespace DiaEditCore.Algorithm.CacheBuilder;

using DiaEditCore.Model;
using DiaEditCore.Model.Routes;

/// <summary>
/// TimeTableSetCache.StationConnectionIndex（StationId→それを通るStationConnectionの一覧）と
/// TimeTableSetCache.EntryPointConnectionIndex（EntryPointId→それを通るStationConnectionの一覧）を
/// 同時に構築する。
///
/// v12.29 SCS direction-agnostic renameセッションでEntryPointSequenceResolver.Resolveが
/// allMainRoutesを要求するシグネチャへ変更されたことに伴い、本Builderも同様にallMainRoutesを
/// 受け取る（TimeTableSetCache.RebuildAllが既にmainRoutesを引数に持つため、呼び出し元の追従は軽微）。
///
/// 用途はStation・EntryPointの「集合」を求めるだけで順序に依存しないため、向き解決の失敗
/// （MainRoute未検出等）でSegmentがスキップされても実害は小さいが、Resolve側の防御的スキップに
/// 従い、結果的に該当StationConnectionの一部Stationが欠落しうる点は留意する
/// （EntryPointSequenceResolver.Resolveのコメント参照）。
/// </summary>
public static class StationAndEntryPointConnectionIndexBuilder
{
    public static (
        Dictionary<StationId, List<StationConnectionId>> StationConnectionIndex,
        Dictionary<EntryPointId, List<StationConnectionId>> EntryPointConnectionIndex
    ) Build(
        IEnumerable<StationConnection> allStationConnections,
        IReadOnlyList<StationConnectionSegment> allSegments,
        IReadOnlyList<MainRoute> allMainRoutes)
    {
        var stationIndex = new Dictionary<StationId, List<StationConnectionId>>();
        var entryPointIndex = new Dictionary<EntryPointId, List<StationConnectionId>>();

        void AddStation(StationId stationId, StationConnectionId scId, HashSet<StationId> seen)
        {
            if (!seen.Add(stationId)) return;
            if (!stationIndex.TryGetValue(stationId, out var list))
            {
                list = new List<StationConnectionId>();
                stationIndex[stationId] = list;
            }
            list.Add(scId);
        }

        void AddEntryPoint(EntryPointId entryPointId, StationConnectionId scId, HashSet<EntryPointId> seen)
        {
            if (!seen.Add(entryPointId)) return;
            if (!entryPointIndex.TryGetValue(entryPointId, out var list))
            {
                list = new List<StationConnectionId>();
                entryPointIndex[entryPointId] = list;
            }
            list.Add(scId);
        }

        foreach (var sc in allStationConnections)
        {
            var seq = EntryPointSequenceResolver.Resolve(sc, allSegments, allMainRoutes);
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
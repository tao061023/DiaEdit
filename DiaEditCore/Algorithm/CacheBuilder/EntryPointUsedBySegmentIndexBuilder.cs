namespace DiaEditCore.Algorithm.CacheBuilder;

using DiaEditCore.Model;
using DiaEditCore.Model.Routes;

/// <summary>
/// TimeTableSetCache.EntryPointUsedBySegmentIndex（EntryPointId→それをFrom/ToEntryPointIdに持つ
/// StationConnectionSegmentの一覧）の構築を担う。
///
/// StationConnectionSegment.FromEntryPointId/ToEntryPointIdはSegment自身が直接保持する属性のため、
/// Segment列を1回走査するだけで導出できる（StationUsedBySegmentIndexBuilderと同型）。
/// 既存のEntryPointConnectionIndex（StationConnection経由の間接参照）では、どのSCにも
/// 属さない孤立Segmentから直接参照されているEntryPointを捕捉できないため新設する
/// （Stationで既に対応済みの「孤立Segment問題」のEntryPoint版）。
/// 消費者はDependencyResolver.ResolveDirectDependents（EntryPointObjectIdケース）のみ。
/// </summary>
public static class EntryPointUsedBySegmentIndexBuilder
{
    public static Dictionary<EntryPointId, List<StationConnectionSegmentId>> Build(
        IEnumerable<StationConnectionSegment> allSegments)
    {
        var index = new Dictionary<EntryPointId, List<StationConnectionSegmentId>>();

        foreach (var seg in allSegments)
        {
            Add(index, seg.FromEntryPointId, seg.Id);
            Add(index, seg.ToEntryPointId, seg.Id);
        }

        return index;
    }

    private static void Add(
        Dictionary<EntryPointId, List<StationConnectionSegmentId>> index,
        EntryPointId entryPointId,
        StationConnectionSegmentId segmentId)
    {
        if (!index.TryGetValue(entryPointId, out var list))
        {
            list = new List<StationConnectionSegmentId>();
            index[entryPointId] = list;
        }
        list.Add(segmentId);
    }
}
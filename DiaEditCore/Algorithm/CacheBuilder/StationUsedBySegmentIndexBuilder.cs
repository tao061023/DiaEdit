namespace DiaEditCore.Algorithm.CacheBuilder;

using DiaEditCore.Model;
using DiaEditCore.Model.Routes;

/// <summary>
/// TimeTableSetCache.StationUsedBySegmentIndex（StationId→それをFrom/ToStationIdに持つ
/// StationConnectionSegmentの一覧）の構築を担う。
///
/// StationConnectionSegment.FromStationId/ToStationIdはSegment自身が直接保持する属性のため、
/// Segment列を1回走査するだけで導出できる。既存のStationConnectionIndex（Station→StationConnection）
/// はStationConnection経由の間接参照のみを捉えており、どのStationConnectionにも属さない孤立した
/// SegmentからのStation直接参照を捕捉できていなかった（§9.1項目6の監査で判明）。
/// 消費者はDependencyResolver.ResolveDirectDependents（StationObjectIdケース）のみ。
/// </summary>
public static class StationUsedBySegmentIndexBuilder
{
    public static Dictionary<StationId, List<StationConnectionSegmentId>> Build(
        IEnumerable<StationConnectionSegment> allSegments)
    {
        var index = new Dictionary<StationId, List<StationConnectionSegmentId>>();

        foreach (var seg in allSegments)
        {
            Add(index, seg.FromStationId, seg.Id);
            Add(index, seg.ToStationId, seg.Id);
        }

        return index;
    }

    private static void Add(
        Dictionary<StationId, List<StationConnectionSegmentId>> index,
        StationId stationId,
        StationConnectionSegmentId segmentId)
    {
        if (!index.TryGetValue(stationId, out var list))
        {
            list = new List<StationConnectionSegmentId>();
            index[stationId] = list;
        }
        list.Add(segmentId);
    }
}
namespace DiaEditCore.Algorithm.CacheBuilder;

using DiaEditCore.Model;
using DiaEditCore.Model.Routes;

/// <summary>
/// TimeTableSetCache.StationUsedBySegmentIndex（StationId→それをFrom/ToStationIdに持つ
/// StationConnectionSegmentの一覧）の構築を担う。
///
/// StationConnectionSegment.FromStationId/ToStationIdはSegment自身が直接保持する属性のため、
/// Segment列を1回走査するだけで導出できる。
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
            Add(index, seg.StationIdA, seg.Id);
            Add(index, seg.StationIdB, seg.Id);
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
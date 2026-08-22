namespace DiaEditCore.Algorithm.CacheBuilder;

using DiaEditCore.Model;
using DiaEditCore.Model.Routes;

/// <summary>
/// TimeTableSetCache.MainRouteUsedBySegmentIndex（MainRouteId→それをMainRouteIdに持つ
/// StationConnectionSegmentの一覧）の構築を担う。
///
/// StationConnectionSegment.MainRouteIdはSegment自身が直接保持する属性のため、
/// Segment列を1回走査するだけで導出できる（StationUsedBySegmentIndexBuilderと同型）。
/// 既存のMainRouteConnectionIndex（StationConnection経由の間接参照）では、どのSCにも
/// 属さない孤立Segmentから直接参照されているMainRouteを捕捉できないため新設する
/// （Stationで既に対応済みの「孤立Segment問題」のMainRoute版）。
/// 消費者はDependencyResolver.ResolveDirectDependents（MainRouteObjectIdケース）のみ。
/// </summary>
public static class MainRouteUsedBySegmentIndexBuilder
{
    public static Dictionary<MainRouteId, List<StationConnectionSegmentId>> Build(
        IEnumerable<StationConnectionSegment> allSegments)
    {
        var index = new Dictionary<MainRouteId, List<StationConnectionSegmentId>>();

        foreach (var seg in allSegments)
        {
            if (!index.TryGetValue(seg.MainRouteId, out var list))
            {
                list = new List<StationConnectionSegmentId>();
                index[seg.MainRouteId] = list;
            }
            list.Add(seg.Id);
        }

        return index;
    }
}
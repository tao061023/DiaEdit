namespace DiaEditCore.Algorithm.CacheBuilder;

using DiaEditCore.Model;
using DiaEditCore.Model.TimeTable;

/// <summary>
/// TimeTableSetCache.TemporaryRestrictionBySegmentIndex（StationConnectionSegmentId→それを対象と
/// するTemporaryRestrictionの一覧）の構築を担う。
///
/// TemporaryRestriction.TargetはRestrictionTarget.Segment（StationConnectionSegmentId対象）／
/// RestrictionTarget.Rail（RailId対象）の判別共用体（TemporaryRestriction.cs）。本インデックスは
/// Segmentケースのみを対象とする（Railケースはこのインデックスの対象外。RailId起点の逆引きが
/// 必要になった場合は別途RestrictionsByRailIndex等を新設して対応する。現時点でRail起点の
/// 逆引き消費者はDeleteRailCommandのみであり、Rail削除時のチェックはTemporaryRestriction列を
/// 直接1回線形走査すれば足りる規模のため、専用インデックス化は見送る）。
///
/// 消費者はDependencyResolver.ResolveDirectDependents（StationConnectionSegmentObjectIdケースの
/// うちTemporaryRestriction逆引き部分）のみ。
/// </summary>
public static class TemporaryRestrictionBySegmentIndexBuilder
{
    public static Dictionary<StationConnectionSegmentId, List<TemporaryRestrictionId>> Build(
        IEnumerable<TemporaryRestriction> allRestrictions)
    {
        var index = new Dictionary<StationConnectionSegmentId, List<TemporaryRestrictionId>>();

        foreach (var restriction in allRestrictions)
        {
            if (restriction.Target is not RestrictionTarget.Segment segmentTarget) continue;

            var segId = segmentTarget.StationConnectionSegmentId;
            if (!index.TryGetValue(segId, out var list))
            {
                list = new List<TemporaryRestrictionId>();
                index[segId] = list;
            }
            list.Add(restriction.Id);
        }

        return index;
    }
}
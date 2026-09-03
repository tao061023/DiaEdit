namespace DiaEditCore.Algorithm.Conflicts;

using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;

/// <summary>
///「統一グルーピング方式」：StationPathが占有する対象オブジェクトID群を求め、 <br/>
/// 全StationPathを対象オブジェクトIDでグルーピングする（同じ対象オブジェクトIDを含む <br/>
/// StationPath同士が、ConflictCheckerの1インスタンスに対応する）。 <br/>
///
/// 対象オブジェクトID群 = resolveRailSequence(sp) ∪ waypoints中のSwitcherId ∪ manualConflictObjectIds <br/>
///
/// Switcherの判定について：Switcher自体が「常に単一の物理的収束点を表す」よう設計されているため、 <br/>
/// 1つのSwitcherを共有するStationPathは、使用した経路の組み合わせによらず常に競合する。 <br/>
/// waypoints中のSwitcherWaypointをそのままグルーピング対象に含めるだけで、特別な判定ロジックは不要。 <br/>
/// </summary>
public static class StationPathConflictObjectResolver
{
    public static IReadOnlyList<ObjectId> Resolve(StationPath sp, RailSequenceResolver railSequenceResolver)
    {
        var result = new List<ObjectId>();

        foreach (var railId in railSequenceResolver.Resolve(sp))
            result.Add(new RailObjectId(railId));

        foreach (var wp in sp.Waypoints)
            if (wp is SwitcherWaypoint sw)
                result.Add(new SwitcherObjectId(sw.Id));

        foreach (var vcoId in sp.ManualConflictObjectIds)
            result.Add(new VirtualConflictObjectIdObject(vcoId));

        return result;
    }

    public static Dictionary<ObjectId, List<StationPathId>> GroupAll(
        IReadOnlyList<StationPath> allPaths, RailSequenceResolver railSequenceResolver)
    {
        var grouping = new Dictionary<ObjectId, List<StationPathId>>();

        foreach (var sp in allPaths)
        {
            foreach (var objId in Resolve(sp, railSequenceResolver))
            {
                if (!grouping.TryGetValue(objId, out var list))
                {
                    list = new List<StationPathId>();
                    grouping[objId] = list;
                }
                list.Add(sp.Id);
            }
        }

        return grouping;
    }
}
using DiaEditCore.Model.Stations;

namespace DiaEditCore.Serialization.Validation.Stations;

public sealed class StationPathValidator : IValidator<StationPath>
{
    public IReadOnlyList<IValidationIssue> Validate(StationPath target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();
        var wp = target.Waypoints;

        if (wp.Count < 1)
        {
            issues.Add(new ValidationIssue($"StationPath({target.Id}): waypointsは最低1件が必要"));
            return issues;
        }

        static bool IsTerminalType(StationPathWaypoint w) =>
            w is BoundaryPointWaypoint or EntryPointWaypoint or BufferStopWaypoint;

        // 1. 始点
        if (!IsTerminalType(wp[0]))
            issues.Add(new ValidationIssue($"StationPath({target.Id}): 始点はEntryPoint/BoundaryPoint/BufferStopのいずれか"));

        // 2. 終点
        if (!IsTerminalType(wp[^1]))
            issues.Add(new ValidationIssue($"StationPath({target.Id}): 終点はEntryPoint/BoundaryPoint/BufferStopのいずれか"));

        // 3. 中間要素はSwitcherのみ
        for (var i = 1; i < wp.Count - 1; i++)
        {
            if (wp[i] is not SwitcherWaypoint)
                issues.Add(new ValidationIssue($"StationPath({target.Id}): 中間waypoint[{i}]はSwitcherのみ許容"));
        }

        // 4. 隣接waypoint間を直接結ぶRailの存在確認
        for (var i = 0; i < wp.Count - 1; i++)
        {
            var aKey = wp[i].Key();
            var bKey = wp[i + 1].Key();
            var connected = context.Rails.Any(r =>
                (r.EndpointA.Key() == aKey && r.EndpointB.Key() == bKey) ||
                (r.EndpointA.Key() == bKey && r.EndpointB.Key() == aKey));
            if (!connected)
                issues.Add(new ValidationIssue($"StationPath({target.Id}): waypoint[{i}]とwaypoint[{i + 1}]を直接結ぶRailが存在しない"));
        }

        // 5. ループ排除（record構造的等価性をそのまま利用）
        var seen = new HashSet<StationPathWaypoint>();
        foreach (var w in wp)
        {
            if (!seen.Add(w))
                issues.Add(new ValidationIssue($"StationPath({target.Id}): waypoint {w} が重複（ループ）"));
        }

        // 6. name一意性
        var duplicateName = context.StationPaths.Any(sp =>
            sp.Id != target.Id && sp.FloorUnitId == target.FloorUnitId && sp.Name == target.Name);
        if (duplicateName)
            issues.Add(new ValidationIssue($"StationPath({target.Id}): name '{target.Name}' が同一FloorUnitId内で重複"));

        // 7. Track各端部からのEP到達可能性確認
        foreach (var rail in context.Rails.Where(r => r.Roll == RailRoll.Track))
        {
            foreach (var ep in new[] { rail.EndpointA, rail.EndpointB })
            {
                if (ep is BoundaryPointEndpointRef or NoneEndpointRef) continue;

                var epKey = ep.Key();
                var reachesEntryPoint = context.StationPaths.Any(sp =>
                    sp.Waypoints.Any(w => w.Key() == epKey) &&
                    sp.Waypoints.Any(w => w is EntryPointWaypoint));

                if (!reachesEntryPoint)
                    issues.Add(new ValidationIssue(
                        $"Rail({rail.Id})の端部({epKey})からArrival/DepartureEPへ到達するStationPathが存在しない"));
            }
        }

        // 8. AdjustmentSec >= 0
        if (target.AdjustmentSec < 0)
            issues.Add(new ValidationIssue($"StationPath({target.Id}): AdjustmentSecは0以上でなければならない"));

        return issues;
    }
}
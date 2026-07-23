using DiaEditCore.Model;

namespace DiaEditCore.Serialization.Validation;

public sealed class MainRouteValidator : IValidator<MainRoute>
{
    public IReadOnlyList<IValidationIssue> Validate(MainRoute target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();

        // stationDisplayNameOverridesのキーは全てstationOrderに含まれる駅であること
        var stationOrderSet = new HashSet<StationId>(target.StationOrder);
        foreach (var key in target.StationDisplayNameOverrides.Keys)
        {
            if (!stationOrderSet.Contains(key))
                issues.Add(new ValidationIssue(
                    $"MainRoute({target.Id}): StationDisplayNameOverridesのキー({key})がStationOrderに含まれない"));
        }

        // stationOrderの先頭駅・末尾駅（isLoop=trueの場合は同一駅）はStation.type != Haltであること
        if (target.StationOrder.Count > 0)
        {
            var firstId = target.StationOrder[0];
            var lastId = target.StationOrder[^1];

            void CheckNotHalt(StationId id, string label)
            {
                var station = context.Stations.FirstOrDefault(s => s.Id == id);
                if (station?.Type == StationType.Halt)
                    issues.Add(new ValidationIssue($"MainRoute({target.Id}): {label}駅({id})がHalt駅になっている"));
            }

            CheckNotHalt(firstId, "先頭");
            if (!target.IsLoop)
                CheckNotHalt(lastId, "末尾");
            // isLoop=trueの場合、firstId==lastId前提のためCheckNotHalt(firstId)の1回で十分
        }

        return issues;
    }
}
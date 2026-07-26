using DiaEditCore.Model.Stations;

namespace DiaEditCore.Serialization.Validation.Stations;

public sealed class PlatformValidator : IValidator<Platform>
{
    public IReadOnlyList<IValidationIssue> Validate(Platform target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();

        if (!context.FloorUnits.Any(f => f.Id == target.Base.FloorUnitId))
            issues.Add(new ValidationIssue($"Platform({target.Id}): FloorUnitId({target.Base.FloorUnitId})が存在しない"));

        if (target.FacingRailIds.Count == 0)
            issues.Add(new ValidationIssue($"Platform({target.Id}): FacingRailIdsが空（面するRailが1つも指定されていない）"));

        foreach (var railId in target.FacingRailIds)
        {
            if (!context.Rails.Any(r => r.Id == railId))
                issues.Add(new ValidationIssue($"Platform({target.Id}): FacingRailIds内のRailId({railId})が存在しない"));
        }

        if (target.EffectiveLength is { } len && len <= 0)
            issues.Add(new ValidationIssue($"Platform({target.Id}): EffectiveLengthは正の値でなければならない"));

        return issues;
    }
}

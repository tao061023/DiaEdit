namespace DiaEditCore.Serialization.Validation.Stations.FloorUnitObjects;

using DiaEditCore.Model.Stations.FloorUnitObjects;

public sealed class EntryPointValidator : IValidator<EntryPoint>
{
    public IReadOnlyList<IValidationIssue> Validate(EntryPoint target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();

        // 5.4.7節のHalt駅制約はSwitcher/BoundaryPointのみが対象。
        // EntryPointはHalt駅（単一EntryPointで構成、5.5.2節）にも必須のため対象外。

        if (!context.FloorUnits.Any(f => f.Id == target.Base.FloorUnitId))
            issues.Add(new ValidationIssue($"EntryPoint({target.Id}): FloorUnitId({target.Base.FloorUnitId})が存在しない"));

        return issues;
    }
}

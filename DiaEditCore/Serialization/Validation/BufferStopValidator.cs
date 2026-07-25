using DiaEditCore.Model;

namespace DiaEditCore.Serialization.Validation;

public sealed class BufferStopValidator : IValidator<BufferStop>
{
    public IReadOnlyList<IValidationIssue> Validate(BufferStop target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();

        // 5.4.7節のHalt駅制約はSwitcher/BoundaryPointのみが対象。BufferStopは対象外。

        if (!context.FloorUnits.Any(f => f.Id == target.Base.FloorUnitId))
            issues.Add(new ValidationIssue($"BufferStop({target.Id}): FloorUnitId({target.Base.FloorUnitId})が存在しない"));

        return issues;
    }
}

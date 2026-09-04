namespace DiaEditCore.Serialization.Validation.Stations.FloorUnitObjects;

using DiaEditCore.Model.Stations.FloorUnitObjects;

/// <summary>BufferStopValidatorと同型。Halt駅制約（4.4.7節）はSwitcher/BoundaryPointのみ対象のため対象外。</summary>
public sealed class NoneEndpointValidator : IValidator<NoneEndpoint>
{
    public IReadOnlyList<IValidationIssue> Validate(NoneEndpoint target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();

        if (!context.FloorUnits.Any(f => f.Id == target.Base.FloorUnitId))
            issues.Add(new ValidationIssue($"NoneEndpoint({target.Id}): FloorUnitId({target.Base.FloorUnitId})が存在しない"));

        return issues;
    }
}
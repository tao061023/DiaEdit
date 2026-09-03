namespace DiaEditCore.Serialization.Validation.Stations.FloorUnitObjects;

using DiaEditCore.Model.Stations.FloorUnitObjects;

public sealed class RailValidator : IValidator<Rail>
{
    public IReadOnlyList<IValidationIssue> Validate(Rail target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();

        if (target.Role == RailRole.Track && string.IsNullOrEmpty(target.Name))
            issues.Add(new ValidationIssue($"Rail({target.Id}) はRole=Trackのため名前が必須"));

        return issues;
    }
}
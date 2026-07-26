using DiaEditCore.Model.Stations;

namespace DiaEditCore.Serialization.Validation.Stations;

public sealed class RailValidator : IValidator<Rail>
{
    public IReadOnlyList<IValidationIssue> Validate(Rail target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();

        if (target.Roll == RailRoll.Track && string.IsNullOrEmpty(target.Name))
            issues.Add(new ValidationIssue($"Rail({target.Id}) はRoll=Trackのため名前が必須"));

        return issues;
    }
}
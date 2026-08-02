using DiaEditCore.Model.Cars;

namespace DiaEditCore.Serialization.Validation.Cars;

public sealed class CarCompositionValidator : IValidator<CarComposition>
{
    public IReadOnlyList<IValidationIssue> Validate(CarComposition target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();

        if (string.IsNullOrWhiteSpace(target.Name))
            issues.Add(new ValidationIssue($"CarComposition({target.Id}): Nameが空"));

        if (!context.CarConsists.Any(c => c.Id == target.CarConsistId))
            issues.Add(new ValidationIssue($"CarComposition({target.Id}): CarConsistId({target.CarConsistId})が存在しない"));

        return issues;
    }
}

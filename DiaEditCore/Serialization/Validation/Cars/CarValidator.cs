using DiaEditCore.Model.Cars;

namespace DiaEditCore.Serialization.Validation.Cars;

public sealed class CarValidator : IValidator<Car>
{
    public IReadOnlyList<IValidationIssue> Validate(Car target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();

        if (string.IsNullOrWhiteSpace(target.CarType))
            issues.Add(new ValidationIssue($"Car({target.Id}): CarTypeが空"));

        // LengthMはEffectiveLengthChecker（6.8節）が参照する実用値のため、0以下は不正とする
        if (target.LengthM <= 0)
            issues.Add(new ValidationIssue($"Car({target.Id}): LengthMが0以下"));

        return issues;
    }
}
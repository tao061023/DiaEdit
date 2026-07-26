using DiaEditCore.Model.Cars;

namespace DiaEditCore.Serialization.Validation.Cars;

public sealed class CarValidator : IValidator<Car>
{
    public IReadOnlyList<IValidationIssue> Validate(Car target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();

        if (string.IsNullOrWhiteSpace(target.Number))
            issues.Add(new ValidationIssue($"Car({target.Id}): Numberが空"));

        if (!context.VehicleTypes.Any(v => v.Id == target.VehicleTypeId))
            issues.Add(new ValidationIssue($"Car({target.Id}): VehicleTypeId({target.VehicleTypeId})が存在しない"));

        return issues;
    }
}

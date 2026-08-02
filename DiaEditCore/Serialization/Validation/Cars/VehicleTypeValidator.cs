using DiaEditCore.Model.Cars;

namespace DiaEditCore.Serialization.Validation.Cars;

public sealed class VehicleTypeValidator : IValidator<VehicleType>
{
    public IReadOnlyList<IValidationIssue> Validate(VehicleType target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();

        if (string.IsNullOrWhiteSpace(target.Name))
            issues.Add(new ValidationIssue($"VehicleType({target.Id}): Nameが空"));

        return issues;
    }
}
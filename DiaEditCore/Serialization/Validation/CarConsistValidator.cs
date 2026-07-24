using DiaEditCore.Model;

namespace DiaEditCore.Serialization.Validation;

public sealed class CarConsistValidator : IValidator<CarConsist>
{
    public IReadOnlyList<IValidationIssue> Validate(CarConsist target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();

        // Position重複禁止・連番
        var positions = target.Cars.Select(c => c.Position).OrderBy(p => p).ToList();
        for (var i = 0; i < positions.Count; i++)
        {
            if (positions[i] != i)
            {
                issues.Add(new ValidationIssue($"CarConsist({target.Id}): Positionが0始まりの連番になっていない"));
                break;
            }
        }

        // 各CarのVehicleTypeIdがCarConsist.VehicleTypeIdと一致すること
        foreach (var carRef in target.Cars)
        {
            var car = context.Cars.FirstOrDefault(c => c.Id == carRef.CarId);
            if (car is null)
            {
                issues.Add(new ValidationIssue($"CarConsist({target.Id}): 参照Car({carRef.CarId})が存在しない"));
            }
            else if (car.VehicleTypeId != target.VehicleTypeId)
            {
                issues.Add(new ValidationIssue($"CarConsist({target.Id}): Car({car.Id})のVehicleTypeIdが不一致"));
            }
        }

        // SourceTemplateがAttachedTemplateSourceの場合、参照先が実在すること
        if (target.SourceTemplate is AttachedTemplateSource ats)
        {
            var vehicleType = context.VehicleTypes.FirstOrDefault(v => v.Id == target.VehicleTypeId);
            var exists = vehicleType?.AttachedCarTemplates.Any(t => t.Id == ats.TemplateId) ?? false;
            if (!exists)
                issues.Add(new ValidationIssue($"CarConsist({target.Id}): AttachedCarTemplateId({ats.TemplateId})がVehicleType({target.VehicleTypeId})に存在しない"));
        }

        return issues;
    }
}
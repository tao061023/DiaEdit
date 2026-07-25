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
        var vehicleType = context.VehicleTypes.FirstOrDefault(v => v.Id == target.VehicleTypeId);
        if (target.SourceTemplate is AttachedTemplateSource ats)
        {
            var exists = vehicleType?.AttachedCarTemplates.Any(t => t.Id == ats.TemplateId) ?? false;
            if (!exists)
                issues.Add(new ValidationIssue($"CarConsist({target.Id}): AttachedCarTemplateId({ats.TemplateId})がVehicleType({target.VehicleTypeId})に存在しない"));
        }

        // Cars数が、SourceTemplateが指すひな型のスロット数と一致すること
        //   （どのひな型を参照しているかはSourceTemplateで一意に決まるため、Cars.Countはそれと常に整合しなければならない）
        if (vehicleType is not null)
        {
            var expectedSlotCount = target.SourceTemplate switch
            {
                BaseTemplateSource => (int?)vehicleType.BaseCarTemplate.Count,
                AttachedTemplateSource ats2 => vehicleType.AttachedCarTemplates
                    .FirstOrDefault(t => t.Id == ats2.TemplateId)?.Slots.Count,
                _ => null
            };

            // AttachedTemplateSourceで参照先ひな型自体が存在しない場合は、上のチェックで既にエラー済みなのでここでは二重報告しない
            if (expectedSlotCount is int n && target.Cars.Count != n)
            {
                issues.Add(new ValidationIssue(
                    $"CarConsist({target.Id}): Cars数({target.Cars.Count})がひな型スロット数({n})と不一致"));
            }
        }

        return issues;
    }
}
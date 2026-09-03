namespace DiaEditCore.Serialization.Validation.Cars;

using DiaEditCore.Model.Cars;

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

        // 各CarRefが参照するCarが実在すること
        var referencedCars = new List<Car>();
        foreach (var carRef in target.Cars)
        {
            var car = context.Cars.FirstOrDefault(c => c.Id == carRef.CarId);
            if (car is null)
            {
                issues.Add(new ValidationIssue($"CarConsist({target.Id}): 参照Car({carRef.CarId})が存在しない"));
            }
            else
            {
                referencedCars.Add(car);
            }
        }

        // Type（Basic/Attached）を問わず、動力車（IsPower=true）が最低1両含まれること
        if (referencedCars.Count > 0 && !referencedCars.Any(c => c.IsPower))
        {
            issues.Add(new ValidationIssue($"CarConsist({target.Id}): 動力車（IsPower=true）が1両も含まれていない"));
        }

        return issues;
    }
}
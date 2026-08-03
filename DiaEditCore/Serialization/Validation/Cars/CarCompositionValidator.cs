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

        // Name一意性：全CarCompositions内で常に一意（TrainValidator.TrainNumber一意性と同型の暫定全走査実装）
        if (context.CarCompositions.Any(c => c.Id != target.Id && c.Name == target.Name))
            issues.Add(new ValidationIssue($"CarComposition({target.Id}): Name({target.Name})が他のCarCompositionと重複している"));

        // Identifier一意性：CarConsistId（VehicleType/型）を問わず、CarComposition全体で常に一意
        // （StartOp時の一意性要求のため。CarConsistごとの局所一意性ではない）
        if (context.CarCompositions.Any(c => c.Id != target.Id && c.Identifier == target.Identifier))
            issues.Add(new ValidationIssue($"CarComposition({target.Id}): Identifier({target.Identifier})が他のCarCompositionと重複している"));

        return issues;
    }
}

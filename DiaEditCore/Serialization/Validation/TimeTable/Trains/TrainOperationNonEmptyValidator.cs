namespace DiaEditCore.Serialization.Validation.TimeTable.Trains;

using DiaEditCore.Model.TimeTable.Trains;

/// <summary>
/// TrainOperation単体で完結する検証（OperationNumberの非空チェックのみ）。
/// 一意性検証（TimeTableSet単位）はTrainOperationUniquenessValidator（横断検証）が別途担う。
/// </summary>
public sealed class TrainOperationNonEmptyValidator : IValidator<TrainOperation>
{
    public IReadOnlyList<IValidationIssue> Validate(TrainOperation target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();

        if (string.IsNullOrWhiteSpace(target.OperationNumber))
            issues.Add(new ValidationIssue($"TrainOperation({target.Id}): OperationNumberが空"));

        return issues;
    }
}
using DiaEditCore.Model.TimeTable;

namespace DiaEditCore.Serialization.Validation.TimeTable;

/// <summary>
/// TimeTableSet.TrainIdsが参照するTrainの実在性を検証する（5.13節）。
/// </summary>
public sealed class TimeTableSetValidator : IValidator<TimeTableSet>
{
    public IReadOnlyList<IValidationIssue> Validate(TimeTableSet target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();

        foreach (var trainId in target.TrainIds)
        {
            if (!context.Trains.Any(t => t.Id == trainId))
            {
                issues.Add(new ValidationIssue(
                    $"TimeTableSet({target.Id}).TrainIds: Train({trainId})が存在しません"));
            }
        }

        return issues;
    }
}
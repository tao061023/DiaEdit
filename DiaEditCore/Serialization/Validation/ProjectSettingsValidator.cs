namespace DiaEditCore.Serialization.Validation;

using DiaEditCore.Model;

/// <summary>
/// ProjectSettings.ValidationRulesの値域を検証する（5.16節）。
///
/// スコープ：
///   - 各int?フィールド（MinDwellTimeSec/MinHeadwaySec/MinTurnaroundSec/
///     TrackEntryMarginSec/TrackPassMarginSec）は、値がある場合0以上であること
///
/// スコープ外：
///   - フィールド間の相関チェック（例：MinTurnaroundSecとTrackEntryMarginSecの大小関係）は行わない
/// </summary>
public sealed class ProjectSettingsValidator : IValidator<ProjectSettings>
{
    public IReadOnlyList<IValidationIssue> Validate(ProjectSettings target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();
        var rules = target.ValidationRules;

        CheckNonNegative(rules.MinDwellTimeSec, nameof(rules.MinDwellTimeSec), issues);
        CheckNonNegative(rules.MinHeadwaySec, nameof(rules.MinHeadwaySec), issues);
        CheckNonNegative(rules.MinTurnaroundSec, nameof(rules.MinTurnaroundSec), issues);
        CheckNonNegative(rules.TrackEntryMarginSec, nameof(rules.TrackEntryMarginSec), issues);
        CheckNonNegative(rules.TrackPassMarginSec, nameof(rules.TrackPassMarginSec), issues);

        return issues;
    }

    private static void CheckNonNegative(int? value, string fieldName, List<IValidationIssue> issues)
    {
        if (value is { } v && v < 0)
        {
            issues.Add(new ValidationIssue(
                $"ProjectSettings.ValidationRules.{fieldName}({v})が負の値です"));
        }
    }
}

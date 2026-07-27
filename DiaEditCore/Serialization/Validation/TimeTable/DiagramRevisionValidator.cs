using DiaEditCore.Model.TimeTable;

namespace DiaEditCore.Serialization.Validation.Timetable;

/// <summary>
/// DiagramRevisionの参照整合性を検証する（5.13節）。
///
/// スコープ：
///   - BaseRevisionId：値ありの場合、参照先DiagramRevisionの実在性
///   - TimeTableSetIds：各要素が指すTimeTableSetの実在性
///   - BaseTimeTableSetId：値ありの場合、自身のTimeTableSetIdsに含まれているか（1025行目の保存時検証）
///
/// スコープ外（設計書上、保存時検証としては明記されていないためValidatorでは扱わない）：
///   - TimeTableSetIdsが空の間のBaseTimeTableSetId必須/null要求は、
///     DiagramRevision作成直後の編集フローにおけるUIレベルの運用ルール（ユーザーへの入力促し）であり、
///     保存時の構造的検証対象ではない。
/// </summary>
public sealed class DiagramRevisionValidator : IValidator<DiagramRevision>
{
    public IReadOnlyList<IValidationIssue> Validate(DiagramRevision target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();

        if (target.BaseRevisionId is { } baseRevisionId &&
            !context.DiagramRevisions.Any(r => r.Id == baseRevisionId))
        {
            issues.Add(new ValidationIssue(
                $"DiagramRevision({target.Id}).BaseRevisionId: DiagramRevision({baseRevisionId})が存在しません"));
        }

        foreach (var timeTableSetId in target.TimeTableSetIds)
        {
            if (!context.TimeTableSets.Any(s => s.Id == timeTableSetId))
            {
                issues.Add(new ValidationIssue(
                    $"DiagramRevision({target.Id}).TimeTableSetIds: TimeTableSet({timeTableSetId})が存在しません"));
            }
        }

        if (target.BaseTimeTableSetId is { } baseTimeTableSetId &&
            !target.TimeTableSetIds.Contains(baseTimeTableSetId))
        {
            issues.Add(new ValidationIssue(
                $"DiagramRevision({target.Id}).BaseTimeTableSetId({baseTimeTableSetId})がTimeTableSetIdsに含まれていません"));
        }

        return issues;
    }
}
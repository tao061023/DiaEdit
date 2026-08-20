using DiaEditCore.Model.TimeTable;

namespace DiaEditCore.Serialization.Validation.TimeTable;

/// <summary>
/// DisplayContextの参照整合性・範囲妥当性を検証する（5.15節）。
///
/// スコープ：
///   - MainRouteRangesが空でないこと（表示対象が存在しないDisplayContextを禁止）
///   - 各MainRouteRange.MainRouteIdが実在するか
///   - FromIndex/ToIndexが対象MainRoute.StationOrderの範囲内（0以上、StationOrder.Count未満）か
///
/// スコープ外：
///   - FromIndex == ToIndex（単一駅区間）の妥当性：MainRoute側で単一駅路線自体が
///     構造的に作れない（MainRouteValidator側の制約）ため、本Validatorでは考慮しない。
/// </summary>
public sealed class DisplayContextValidator : IValidator<DisplayContext>
{
    public IReadOnlyList<IValidationIssue> Validate(DisplayContext target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();

        if (target.MainRouteRanges.Count == 0)
        {
            issues.Add(new ValidationIssue(
                $"DisplayContext({target.Id}).MainRouteRanges: 空です（表示対象が存在しません）"));
            return issues;
        }

        foreach (var range in target.MainRouteRanges)
        {
            var mainRoute = context.MainRoutes.FirstOrDefault(m => m.Id == range.MainRouteId);
            if (mainRoute is null)
            {
                issues.Add(new ValidationIssue(
                    $"DisplayContext({target.Id}).MainRouteRanges: MainRoute({range.MainRouteId})が存在しません"));
                continue;
            }

            var stationCount = mainRoute.StationOrder.Count;

            if (range.FromIndex < 0 || range.FromIndex >= stationCount)
            {
                issues.Add(new ValidationIssue(
                    $"DisplayContext({target.Id}).MainRouteRanges: FromIndex({range.FromIndex})がMainRoute({range.MainRouteId}).StationOrderの範囲外です（0〜{stationCount - 1}）"));
            }

            if (range.ToIndex < 0 || range.ToIndex >= stationCount)
            {
                issues.Add(new ValidationIssue(
                    $"DisplayContext({target.Id}).MainRouteRanges: ToIndex({range.ToIndex})がMainRoute({range.MainRouteId}).StationOrderの範囲外です（0〜{stationCount - 1}）"));
            }
        }

        return issues;
    }
}

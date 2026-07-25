using DiaEditCore.Model;

namespace DiaEditCore.Serialization.Validation;

/// <summary>
/// TemporaryRestrictionの参照整合性・値域を検証する（5.14節）。
///
/// スコープ：
///   - Target（Segment/Rail）が指すStationConnectionSegment/Railの実在性
///   - DateRange.Start &lt;= DateRange.End
///   - ExtraRunTimeSec：値ありの場合、0以上（負の追加所要時分は無意味）
///   - SpeedLimitKph：値ありの場合、正の値（0km/h以下は速度制限として無意味）
/// </summary>
public sealed class TemporaryRestrictionValidator : IValidator<TemporaryRestriction>
{
    public IReadOnlyList<IValidationIssue> Validate(TemporaryRestriction target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();

        switch (target.Target)
        {
            case RestrictionTarget.Segment segment:
                if (!context.StationConnectionSegments.Any(s => s.Id == segment.StationConnectionSegmentId))
                {
                    issues.Add(new ValidationIssue(
                        $"TemporaryRestriction({target.Id}).Target: StationConnectionSegment({segment.StationConnectionSegmentId})が存在しません"));
                }
                break;

            case RestrictionTarget.Rail rail:
                if (!context.Rails.Any(r => r.Id == rail.RailId))
                {
                    issues.Add(new ValidationIssue(
                        $"TemporaryRestriction({target.Id}).Target: Rail({rail.RailId})が存在しません"));
                }
                break;
        }

        if (target.DateRange.Start > target.DateRange.End)
        {
            issues.Add(new ValidationIssue(
                $"TemporaryRestriction({target.Id}).DateRange: Start({target.DateRange.Start})がEnd({target.DateRange.End})より後です"));
        }

        if (target.ExtraRunTimeSec is { } extraRunTimeSec && extraRunTimeSec < 0)
        {
            issues.Add(new ValidationIssue(
                $"TemporaryRestriction({target.Id}).ExtraRunTimeSec({extraRunTimeSec})が負の値です"));
        }

        if (target.SpeedLimitKph is { } speedLimitKph && speedLimitKph <= 0)
        {
            issues.Add(new ValidationIssue(
                $"TemporaryRestriction({target.Id}).SpeedLimitKph({speedLimitKph})が0以下です"));
        }

        return issues;
    }
}
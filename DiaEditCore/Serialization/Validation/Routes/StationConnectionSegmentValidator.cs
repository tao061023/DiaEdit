using DiaEditCore.Model.Routes;

namespace DiaEditCore.Serialization.Validation.Routes;

public sealed class StationConnectionSegmentValidator : IValidator<StationConnectionSegment>
{
    public IReadOnlyList<IValidationIssue> Validate(StationConnectionSegment target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();

        if (target.BaseRunTimeSec < 0)
            issues.Add(new ValidationIssue($"StationConnectionSegment({target.Id}): BaseRunTimeSecは0以上でなければならない"));

        if (target.FromStationId == target.ToStationId)
            issues.Add(new ValidationIssue($"StationConnectionSegment({target.Id}): FromStationIdとToStationIdが同一"));

        return issues;
    }
}
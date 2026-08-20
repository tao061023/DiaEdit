using DiaEditCore.Model;
using DiaEditCore.Model.Routes;

namespace DiaEditCore.Serialization.Validation.Routes;

public sealed class StationConnectionSegmentValidator : IValidator<StationConnectionSegment>
{
    public IReadOnlyList<IValidationIssue> Validate(StationConnectionSegment target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();

        if (target.FromStationId == target.ToStationId)
            issues.Add(new ValidationIssue($"StationConnectionSegment({target.Id}): FromStationIdとToStationIdが同一"));

        ValidateEntryPointStationConsistency(target, context, issues, target.FromEntryPointId, target.FromStationId, "FromEntryPointId", "FromStationId");
        ValidateEntryPointStationConsistency(target, context, issues, target.ToEntryPointId, target.ToStationId, "ToEntryPointId", "ToStationId");

        return issues;
    }

    // §8.2項目4（v11.3実装セッションで判明）：EntryPointが実際にfromStationId/toStationId側のFloorUnit内に
    // 存在することを検証する。EntryPoint実体の引き当てを要する横断検証だが、ValidationContextが
    // 必要な参照（EntryPoints/FloorUnits）を既に提供しているため、単一オブジェクトValidatorの範疇で完結する。
    private static void ValidateEntryPointStationConsistency(
        StationConnectionSegment target,
        ValidationContext context,
        List<IValidationIssue> issues,
        EntryPointId entryPointId,
        StationId expectedStationId,
        string entryPointFieldName,
        string stationFieldName)
    {
        var ep = context.EntryPoints.FirstOrDefault(e => e.Id == entryPointId);
        if (ep is null)
        {
            issues.Add(new ValidationIssue(
                $"StationConnectionSegment({target.Id}): {entryPointFieldName}({entryPointId})が存在しない",
                ValidationSeverity.Warning));
            return;
        }

        var floorUnit = context.FloorUnits.FirstOrDefault(f => f.Id == ep.Base.FloorUnitId);
        if (floorUnit is null)
        {
            issues.Add(new ValidationIssue(
                $"StationConnectionSegment({target.Id}): {entryPointFieldName}({entryPointId})が参照するFloorUnit({ep.Base.FloorUnitId})が存在しない",
                ValidationSeverity.Warning));
            return;
        }

        if (floorUnit.StationId != expectedStationId)
        {
            issues.Add(new ValidationIssue(
                $"StationConnectionSegment({target.Id}): {entryPointFieldName}({entryPointId})はStation({floorUnit.StationId})のFloorUnit内に存在するが、{stationFieldName}は{expectedStationId}",
                ValidationSeverity.Warning));
        }
    }
}
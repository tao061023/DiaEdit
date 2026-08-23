using DiaEditCore.Model;
using DiaEditCore.Model.Routes;

namespace DiaEditCore.Serialization.Validation.Routes;

public sealed class StationConnectionSegmentValidator : IValidator<StationConnectionSegment>
{
    public IReadOnlyList<IValidationIssue> Validate(StationConnectionSegment target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();

        if (target.StationIdA == target.StationIdB)
            issues.Add(new ValidationIssue($"StationConnectionSegment({target.Id}): StationIdAとStationIdBが同一"));

        ValidateEntryPointStationConsistency(target, context, issues, target.EntryPointIdA, target.StationIdA, "EntryPointIdA", "StationIdA");
        ValidateEntryPointStationConsistency(target, context, issues, target.EntryPointIdB, target.StationIdB, "EntryPointIdB", "StationIdB");

        return issues;
    }

    // §8.2項目4（v11.3実装セッションで判明）：EntryPointが実際にStationIdA/StationIdB側のFloorUnit内に
    // 存在することを検証する。EntryPoint実体の引き当てを要する横断検証だが、ValidationContextが
    // 必要な参照（EntryPoints/FloorUnits）を既に提供しているため、単一オブジェクトValidatorの範疇で完結する。
    // v12.29：FromEntryPointId/FromStationId等の有向な語彙をEntryPointIdA/StationIdA等の
    // 無向な語彙へ機械的に置換。本検証自体はA側・B側それぞれ独立にEP↔Station対応を見るだけで
    // 向きに依存しないため、意味論上の変更はない。
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
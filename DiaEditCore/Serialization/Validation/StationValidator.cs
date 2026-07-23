using DiaEditCore.Model;

namespace DiaEditCore.Serialization.Validation;

public sealed class StationValidator : IValidator<Station>
{
    private readonly DisplayNameValidator _displayNameValidator = new();

    public IReadOnlyList<IValidationIssue> Validate(Station target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>(
            _displayNameValidator.Validate(target.DisplayName, context));

        // n≥1制約：このStationを参照するFloorUnitが1件以上存在すること
        var hasFloorUnit = context.FloorUnits.Any(f => f.StationId == target.Id);
        if (!hasFloorUnit)
            issues.Add(new ValidationIssue($"Station({target.Id}) を参照する FloorUnit が1件も存在しない"));

        // operatingCode/telegraphCodeの一意性は複数Stationを横断する検証のため、
        // 単一Station向けのStationValidatorではなくSaveValidationRunner側（後日実装）で行う想定
        return issues;
    }
}
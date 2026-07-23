using DiaEditCore.Model;

namespace DiaEditCore.Serialization.Validation;

public sealed class BoundaryPointValidator : IValidator<BoundaryPoint>
{
    public IReadOnlyList<IValidationIssue> Validate(BoundaryPoint target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();

        var floorUnit = context.FloorUnits.FirstOrDefault(f => f.Id == target.Base.FloorUnitId);
        var station = floorUnit is null ? null : context.Stations.FirstOrDefault(s => s.Id == floorUnit.StationId);

        if (station?.Type == StationType.Halt)
            issues.Add(new ValidationIssue($"BoundaryPoint({target.Id}): Halt駅にはBoundaryPointを配置できない"));

        return issues;
    }
}
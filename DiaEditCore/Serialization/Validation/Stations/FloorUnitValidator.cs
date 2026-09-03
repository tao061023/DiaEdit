namespace DiaEditCore.Serialization.Validation.Stations;

using DiaEditCore.Model.Stations;

public sealed class FloorUnitValidator : IValidator<FloorUnit>
{
    public IReadOnlyList<IValidationIssue> Validate(FloorUnit target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();

        var siblingDisplayOrders = context.FloorUnits
            .Where(f => f.StationId == target.StationId && f.Id != target.Id)
            .Select(f => f.DisplayOrder);

        if (siblingDisplayOrders.Contains(target.DisplayOrder))
        {
            issues.Add(new ValidationIssue(
                $"FloorUnit({target.Id}) の DisplayOrder({target.DisplayOrder}) が同一Station({target.StationId})内で重複している"));
        }

        return issues;
    }
}
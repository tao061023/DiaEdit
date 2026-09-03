namespace DiaEditCore.Serialization.Validation.TimeTable.Trains;

using System.Text.RegularExpressions;

using DiaEditCore.Model.TimeTable.Trains;

public sealed class TrainTypeValidator : IValidator<TrainType>
{
    private static readonly Regex ColorPattern = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);
    private readonly DisplayNameValidator _displayNameValidator = new();

    public IReadOnlyList<IValidationIssue> Validate(TrainType target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>(
            _displayNameValidator.Validate(target.Name, context));

        if (!ColorPattern.IsMatch(target.DiagramColor))
            issues.Add(new ValidationIssue($"TrainType({target.Id}): DiagramColor({target.DiagramColor})が#RRGGBB形式でない"));

        return issues;
    }
}

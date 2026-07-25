using System.Text.RegularExpressions;
using DiaEditCore.Model;

namespace DiaEditCore.Serialization.Validation;

public sealed class TrainTypeValidator : IValidator<TrainType>
{
    private static readonly Regex ColorPattern = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    public IReadOnlyList<IValidationIssue> Validate(TrainType target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();

        if (!ColorPattern.IsMatch(target.DiagramColor))
            issues.Add(new ValidationIssue($"TrainType({target.Id}): DiagramColor({target.DiagramColor})が#RRGGBB形式でない"));

        return issues;
    }
}

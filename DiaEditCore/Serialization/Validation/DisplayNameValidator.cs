namespace DiaEditCore.Serialization.Validation;

using DiaEditCore.Model;

public sealed class DisplayNameValidator : IValidator<DisplayName>
{
    public IReadOnlyList<IValidationIssue> Validate(DisplayName target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();

        if (string.IsNullOrEmpty(target.Name))
            issues.Add(new ValidationIssue("DisplayName.Name は空文字列不可"));

        if (target.Abbreviation is not null && target.Abbreviation.Length == 0)
            issues.Add(new ValidationIssue("DisplayName.Abbreviation は null か非空文字列のいずれか"));

        foreach (var key in target.Translations.Keys)
        {
            if (key != key.ToLowerInvariant())
                issues.Add(new ValidationIssue($"DisplayName.Translations のキー '{key}' は小文字正規化されていない"));
        }

        return issues;
    }
}
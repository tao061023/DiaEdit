namespace DiaEditCore.Serialization.Validation;

public enum ValidationSeverity { Warning, Notice }

public interface IValidationIssue
{
    string Message { get; }
    ValidationSeverity Severity { get; }
}

public sealed record ValidationIssue(string Message, ValidationSeverity Severity = ValidationSeverity.Warning) : IValidationIssue;

public interface IValidator<T>
{
    IReadOnlyList<IValidationIssue> Validate(T target, ValidationContext context);
}
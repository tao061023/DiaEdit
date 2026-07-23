namespace DiaEditCore.Serialization.Validation;

public interface IValidationIssue
{
    string Message { get; }
}

public sealed record ValidationIssue(string Message) : IValidationIssue;

public interface IValidator<T>
{
    IReadOnlyList<IValidationIssue> Validate(T target, ValidationContext context);
}
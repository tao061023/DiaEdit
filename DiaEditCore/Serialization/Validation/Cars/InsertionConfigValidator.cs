// using DiaEditCore.Model.Cars;

// namespace DiaEditCore.Serialization.Validation.Cars;

// public sealed class InsertionConfigValidator : IValidator<InsertionConfig>
// {
//     public IReadOnlyList<IValidationIssue> Validate(InsertionConfig target, ValidationContext context)
//     {
//         var issues = new List<IValidationIssue>();

//         if (target.BaseCarConsistId == target.InsertedCarConsistId)
//             issues.Add(new ValidationIssue($"InsertionConfig({target.Id}): BaseCarConsistIdとInsertedCarConsistIdが同一"));

//         var baseConsist = context.CarConsists.FirstOrDefault(c => c.Id == target.BaseCarConsistId);
//         var insertedConsist = context.CarConsists.FirstOrDefault(c => c.Id == target.InsertedCarConsistId);

//         if (baseConsist is null)
//             issues.Add(new ValidationIssue($"InsertionConfig({target.Id}): BaseCarConsistId({target.BaseCarConsistId})が存在しない"));
//         else if (baseConsist.SourceTemplate is not BaseTemplateSource)
//             issues.Add(new ValidationIssue($"InsertionConfig({target.Id}): BaseCarConsistId({target.BaseCarConsistId})は基本編成（BaseTemplateSource）でなければならない"));
//         else if (target.AfterPosition < 0 || target.AfterPosition > baseConsist.Cars.Count)
//             issues.Add(new ValidationIssue($"InsertionConfig({target.Id}): AfterPosition({target.AfterPosition})がBaseCarConsistのCars範囲外"));

//         if (insertedConsist is null)
//             issues.Add(new ValidationIssue($"InsertionConfig({target.Id}): InsertedCarConsistId({target.InsertedCarConsistId})が存在しない"));
//         else if (insertedConsist.SourceTemplate is not AttachedTemplateSource)
//             issues.Add(new ValidationIssue($"InsertionConfig({target.Id}): InsertedCarConsistId({target.InsertedCarConsistId})は付属編成（AttachedTemplateSource）でなければならない"));

//         return issues;
//     }
// }
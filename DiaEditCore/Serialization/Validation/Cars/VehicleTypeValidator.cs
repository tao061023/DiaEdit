using DiaEditCore.Model.Cars;

namespace DiaEditCore.Serialization.Validation.Cars;

public sealed class VehicleTypeValidator : IValidator<VehicleType>
{
    public IReadOnlyList<IValidationIssue> Validate(VehicleType target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();

        if (string.IsNullOrWhiteSpace(target.Name))
            issues.Add(new ValidationIssue($"VehicleType({target.Id}): Nameが空"));

        // lengthMはEffectiveLengthChecker（6.8節）が参照する実用値のため、0以下は不正とする
        if (target.LengthM <= 0)
            issues.Add(new ValidationIssue($"VehicleType({target.Id}): LengthMが0以下"));

        // baseCarTemplateは基本編成の唯一のひな型であり、空では基本編成のCarConsistを作成できない
        if (target.BaseCarTemplate.Count == 0)
            issues.Add(new ValidationIssue($"VehicleType({target.Id}): BaseCarTemplateが空"));

        for (var i = 0; i < target.BaseCarTemplate.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(target.BaseCarTemplate[i].CarTypeCode))
                issues.Add(new ValidationIssue($"VehicleType({target.Id}).BaseCarTemplate[{i}]: CarTypeCodeが空"));
        }

        // AttachedCarTemplateId重複禁止（CarConsist.SourceTemplate=AttachedTemplateSourceが一意に
        // ひな型を引き当てられることが前提のため）
        var dupIds = target.AttachedCarTemplates
            .GroupBy(t => t.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);
        foreach (var dupId in dupIds)
            issues.Add(new ValidationIssue($"VehicleType({target.Id}): AttachedCarTemplateId({dupId})が重複している"));

        for (var i = 0; i < target.AttachedCarTemplates.Count; i++)
        {
            var template = target.AttachedCarTemplates[i];

            if (string.IsNullOrWhiteSpace(template.Name))
                issues.Add(new ValidationIssue($"VehicleType({target.Id}).AttachedCarTemplates[{i}]({template.Id}): Nameが空"));

            if (template.Slots.Count == 0)
                issues.Add(new ValidationIssue($"VehicleType({target.Id}).AttachedCarTemplates[{i}]({template.Id}): Slotsが空"));

            for (var j = 0; j < template.Slots.Count; j++)
            {
                if (string.IsNullOrWhiteSpace(template.Slots[j].CarTypeCode))
                    issues.Add(new ValidationIssue($"VehicleType({target.Id}).AttachedCarTemplates[{i}]({template.Id}).Slots[{j}]: CarTypeCodeが空"));
            }
        }

        return issues;
    }
}

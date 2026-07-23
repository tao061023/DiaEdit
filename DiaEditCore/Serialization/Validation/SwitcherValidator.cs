using DiaEditCore.Model;

namespace DiaEditCore.Serialization.Validation;

public sealed class SwitcherValidator : IValidator<Switcher>
{
    public IReadOnlyList<IValidationIssue> Validate(Switcher target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();
        var n = target.PortCount;

        if (n >= 5)
        {
            issues.Add(new ValidationIssue(
                $"Switcher({target.Id}): PortCount={n} は5以上のため保存不可。複数Switcher+Railへの分解が必要"));
            return issues;
        }

        if (n == 3)
        {
            if (target.Mechanism is not { } m)
            {
                issues.Add(new ValidationIssue($"Switcher({target.Id}): PortCount=3はMechanism必須"));
            }
            else
            {
                if (m.RootPortIndex == m.NormalPortIndex)
                    issues.Add(new ValidationIssue($"Switcher({target.Id}): RootPortIndex と NormalPortIndex が同一"));
                foreach (var (label, v) in new[] { (nameof(m.RootPortIndex), m.RootPortIndex), (nameof(m.NormalPortIndex), m.NormalPortIndex), (nameof(m.ReversePortIndex), m.ReversePortIndex) })
                {
                    if (v < 0 || v >= n)
                        issues.Add(new ValidationIssue($"Switcher({target.Id}): {label}={v} が0〜{n - 1}の範囲外"));
                }
            }

            if (target.ValidRoutes.Count > 0)
                issues.Add(new ValidationIssue($"Switcher({target.Id}): PortCount=3ではValidRoutesは使用しない"));
        }
        else if (n == 4)
        {
            if (target.Mechanism is not null)
                issues.Add(new ValidationIssue($"Switcher({target.Id}): PortCount=4ではMechanismは常にnull"));

            if (target.ValidRoutes.Count == 0)
                issues.Add(new ValidationIssue($"Switcher({target.Id}): PortCount=4はValidRoutesが1件以上必須"));

            foreach (var pair in target.ValidRoutes)
            {
                if (pair.PortA == pair.PortB)
                    issues.Add(new ValidationIssue($"Switcher({target.Id}): PortPair({pair.PortA},{pair.PortB}) はPortA≠PortB制約に違反"));
                if (pair.PortA < 0 || pair.PortA >= n || pair.PortB < 0 || pair.PortB >= n)
                    issues.Add(new ValidationIssue($"Switcher({target.Id}): PortPair({pair.PortA},{pair.PortB}) が0〜{n - 1}の範囲外"));
            }
        }
        else
        {
            if (target.Mechanism is not null)
                issues.Add(new ValidationIssue($"Switcher({target.Id}): PortCount<=2ではMechanismは使用しない"));
            if (target.ValidRoutes.Count > 0)
                issues.Add(new ValidationIssue($"Switcher({target.Id}): PortCount<=2ではValidRoutesは使用しない"));
        }

        var floorUnit = context.FloorUnits.FirstOrDefault(f => f.Id == target.Base.FloorUnitId);
        var station = floorUnit is null ? null : context.Stations.FirstOrDefault(s => s.Id == floorUnit.StationId);
        if (station?.Type == StationType.Halt)
            issues.Add(new ValidationIssue($"Switcher({target.Id}): Halt駅にはSwitcherを配置できない"));

        return issues;
    }
}
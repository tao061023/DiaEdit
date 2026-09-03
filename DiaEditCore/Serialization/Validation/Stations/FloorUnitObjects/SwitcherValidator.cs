namespace DiaEditCore.Serialization.Validation.Stations.FloorUnitObjects;

using DiaEditCore.Model.Stations;
using DiaEditCore.Model.Stations.FloorUnitObjects;

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
                var portIndices = new[] { m.RootPortIndex, m.NormalPortIndex, m.ReversePortIndex };
                if (portIndices.Distinct().Count() != portIndices.Length)
                    issues.Add(new ValidationIssue($"Switcher({target.Id}): RootPortIndex/NormalPortIndex/ReversePortIndex が重複している"));
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

            var seenRoutes = new HashSet<PortPair>();
            foreach (var pair in target.ValidRoutes)
            {
                if (pair.PortA == pair.PortB)
                    issues.Add(new ValidationIssue($"Switcher({target.Id}): PortPair({pair.PortA},{pair.PortB}) はPortA≠PortB制約に違反"));
                if (pair.PortA < 0 || pair.PortA >= n || pair.PortB < 0 || pair.PortB >= n)
                    issues.Add(new ValidationIssue($"Switcher({target.Id}): PortPair({pair.PortA},{pair.PortB}) が0〜{n - 1}の範囲外"));

                var key = SwitcherRoutingExtensions.Normalize(pair.PortA, pair.PortB);
                if (!seenRoutes.Add(key))
                    issues.Add(new ValidationIssue($"Switcher({target.Id}): PortPair({pair.PortA},{pair.PortB}) が重複登録"));
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

        // ★追加（§8.2項目10）：PortCountと実際に接続しているRail端点数の一致検証。
        // Railは自身が接続するSwitcherを一方向参照する構造（SwitcherEndpointRef）のため、
        // 全Rail（EndpointA/EndpointBの両方）を走査してtarget.Idを指すものを集める必要がある
        // クロスオブジェクト検証。ValidationContext.Railsから引くだけで完結するため、
        // SaveValidationRunnerを待たずSwitcherValidator側へ直接実装する（v11.27原則）。
        var connectedPortIndices = new List<int>();
        foreach (var rail in context.Rails)
        {
            if (rail.EndpointA is SwitcherEndpointRef a && a.Id == target.Id)
                connectedPortIndices.Add(a.PortIndex);
            if (rail.EndpointB is SwitcherEndpointRef b && b.Id == target.Id)
                connectedPortIndices.Add(b.PortIndex);
        }

        if (connectedPortIndices.Count != n)
        {
            issues.Add(new ValidationIssue(
                $"Switcher({target.Id}): PortCount={n} だが実際に接続しているRail端点数は{connectedPortIndices.Count}"));
        }

        var duplicatedPorts = connectedPortIndices
            .GroupBy(p => p)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);
        foreach (var dup in duplicatedPorts)
        {
            issues.Add(new ValidationIssue(
                $"Switcher({target.Id}): PortIndex={dup} を指すRail端点が複数存在する（配線異常）"));
        }

        foreach (var p in connectedPortIndices.Distinct())
        {
            if (p < 0 || p >= n)
                issues.Add(new ValidationIssue($"Switcher({target.Id}): 接続Rail端点のPortIndex={p} が0〜{n - 1}の範囲外"));
        }
        
        // 追加バリデーション（保存時バリデーション項目4）：
        // Track役割・Shunting役割のRail端点はSwitcherを形成できない。
        // target（Switcher）を参照するRail（EndpointA/EndpointBがSwitcherEndpointRef(target.Id, ...)を指すもの）を
        // 全走査し、そのRail自身のRoleがNormal以外であれば不合格とする。
        foreach (var rail in context.Rails)
        {
            bool refsThisSwitcher =
                (rail.EndpointA is SwitcherEndpointRef refA && refA.Id == target.Id) ||
                (rail.EndpointB is SwitcherEndpointRef refB && refB.Id == target.Id);

            if (refsThisSwitcher && rail.Role != RailRole.Normal)
            {
                issues.Add(new ValidationIssue(
                    $"Switcher({target.Id}): Track/Shunting役割のRail({rail.Id})がSwitcherを形成している（Normal役割以外は許可されない）"));
            }
        }

        return issues;
    }
}
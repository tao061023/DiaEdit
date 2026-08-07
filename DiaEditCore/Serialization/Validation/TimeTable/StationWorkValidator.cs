using DiaEditCore.Model.TimeTable;

namespace DiaEditCore.Serialization.Validation.Timetable;

public sealed class StationWorkValidator : IValidator<StationWork>
{
    public IReadOnlyList<IValidationIssue> Validate(StationWork target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();

        // 5.11.5節：「None: works に格納しない」。Noneがリストに存在すること自体が矛盾
        if (target.Type == StationWorkType.None)
        {
            issues.Add(new ValidationIssue("StationWork: Type=NoneはWorks配列に格納してはならない"));
            return issues;
        }

        // StartOpConsist/CutGroupsは、それぞれ対応するTypeでのみ使用可能
        //   （型分離によりStartOpConsistとCutGroupsは別フィールドになったため、
        //    「片方しか値を持たない」という排他関係をここで構造的に検証する）
        if (target.Type != StationWorkType.StartOp && target.StartOpConsist.Count > 0)
            issues.Add(new ValidationIssue($"StationWork({target.Type}): StartOpConsistはStartOpでのみ使用可能"));

        var cutGroupsAllowed = target.Type is StationWorkType.Coupling or StationWorkType.Decoupling;
        if (!cutGroupsAllowed && target.CutGroups.Count > 0)
            issues.Add(new ValidationIssue($"StationWork({target.Type}): CutGroupsはCoupling/Decouplingでのみ使用可能"));

        var prevTrainOverridesAllowed = target.Type == StationWorkType.PrevTrain;
        if (!prevTrainOverridesAllowed && target.PrevTrainOperationOverrides.Count > 0)
            issues.Add(new ValidationIssue($"StationWork({target.Type}): PrevTrainOperationOverridesはPrevTrainでのみ使用可能"));

        // SplitOriginRefはPrevTrainにのみ付随する（セッションで確定：分割起点のTrainは常にPrevTrain型を使う）
        if (target.Type != StationWorkType.PrevTrain && target.SplitOrigin is not null)
            issues.Add(new ValidationIssue($"StationWork({target.Type}): SplitOriginはPrevTrainでのみ使用可能"));

        // SplitOrigin.OriginTrainId/OriginStopKeyの実在確認、および「兄弟Train間で発車時刻が重複しない」
        // 「two-pointer割当（GroupIndex導出）がCutGroups数と整合する」といった横断検証は、単一StationWorkでは
        // 完結しないため、SplitOriginCrossValidator（新設予定、6.12.1節TrainOperationCrossValidatorと同型）側の責務とする

        // 型別必須フィールド（5.11.5節のコメントに基づく）
        switch (target.Type)
        {
            case StationWorkType.StartOp:
                if (target.StartOpSeconds < 0)
                    issues.Add(new ValidationIssue("StationWork(StartOp): StartOpSecondsが未設定"));

                // StationWork.TrainOperationIdスカラーは廃止。各StartOpCarSlot.OperationIdがrequiredのため、
                // 「未設定」自体はC#の型システムで構造的に防止済み（継承フォールバックの概念自体が消滅）。

                // StartOpConsist内のPosition重複禁止・0始まり連番（既存どおり、v11.20）
                var startOpPositions = target.StartOpConsist.Select(c => c.Position).OrderBy(p => p).ToList();
                for (var i = 0; i < startOpPositions.Count; i++)
                {
                    if (startOpPositions[i] != i)
                    {
                        issues.Add(new ValidationIssue("StationWork(StartOp): StartOpConsistのPositionが0始まりの連番になっていない"));
                        break;
                    }
                }
                break;

            case StationWorkType.EndOp:
                if (target.EndOpSeconds < 0)
                    issues.Add(new ValidationIssue("StationWork(EndOp): EndOpSecondsが未設定"));
                break;

            case StationWorkType.Shunting:
                if (target.StationPathId is null)
                    issues.Add(new ValidationIssue("StationWork(Shunting): StationPathIdが未設定"));
                else if (!context.StationPaths.Any(sp => sp.Id == target.StationPathId))
                    issues.Add(new ValidationIssue($"StationWork(Shunting): StationPathId({target.StationPathId})が存在しない"));
                if (target.StartOpSeconds < 0 || target.EndOpSeconds < 0)
                    issues.Add(new ValidationIssue("StationWork(Shunting): StartOpSeconds/EndOpSecondsは両方必須"));
                break;

            case StationWorkType.Coupling:
            case StationWorkType.Decoupling:
                if (target.StartOpSeconds < 0 || target.EndOpSeconds < 0)
                    issues.Add(new ValidationIssue($"StationWork({target.Type}): StartOpSeconds/EndOpSecondsは両方必須"));
                if (target.CutGroups.Count == 0)
                    issues.Add(new ValidationIssue($"StationWork({target.Type}): CutGroupsが空"));
                break;

            case StationWorkType.NextTrain:
                if (target.NextTrainType is null)
                    issues.Add(new ValidationIssue("StationWork(NextTrain): NextTrainTypeが未設定"));
                break;

            case StationWorkType.PrevTrain:
                // 現時点では追加の必須フィールドなし（PrevTrainOperationOverridesは省略可＝全Composition継承）
                break;
        }

        // CutGroups内のCarCompositionId実在チェック（Coupling/Decouplingで使用）
        foreach (var cg in target.CutGroups)
        {
            if (!context.CarCompositions.Any(c => c.Id == cg.CarCompositionId))
                issues.Add(new ValidationIssue($"StationWork({target.Type}): CutGroups内のCarCompositionId({cg.CarCompositionId})が存在しない"));
        }

        // StartOpConsist内のCarCompositionId実在チェック（StartOpで使用）
        foreach (var slot in target.StartOpConsist)
        {
            if (!context.CarCompositions.Any(c => c.Id == slot.CarCompositionId))
                issues.Add(new ValidationIssue($"StationWork(StartOp): StartOpConsist内のCarCompositionId({slot.CarCompositionId})が存在しない"));
        }

        // CutGroups.GroupIndex：同一StationWork内で重複禁止（新設。SplitOriginRefが
        // (originTrainId, originStopKey, groupIndex)でCutGroupを一意に引き当てる前提のため、
        // 重複を許すと参照整合性検証（Rule 6）が曖昧になる）
        if (cutGroupsAllowed)
        {
            var duplicatedIndices = target.CutGroups
                .GroupBy(cg => cg.GroupIndex)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);
            foreach (var dupIndex in duplicatedIndices)
            {
                issues.Add(new ValidationIssue(
                    $"StationWork({target.Type}): CutGroups間でGroupIndex({dupIndex})が重複している"));
            }
        }

        // StartOpCarSlot.OperationId（Rule 5改訂）の検証
        //   ・ResolvedOperationRef：TrainOperation実体（context.TrainOperations）とのId一致を要求
        //   ・ProvisionalOperationRef：TODO（要Tao様確認）－改訂案のRule 5表はCutGroupのみ規定しており、
        //     StartOpCarSlotでProvisionalOperationRefを許容してよいか（出区時点で未確定運用のラベルを
        //     許すべきか）が確定できないため、現状はチェックをスキップし警告も出さない。
        foreach (var slot in target.StartOpConsist)
        {
            if (slot.OperationId is ResolvedOperationRef resolved &&
                !context.TrainOperations.Any(o => o.Id == resolved.Id))
            {
                issues.Add(new ValidationIssue(
                    $"StationWork(StartOp): StartOpConsist(Position={slot.Position})のOperationId({resolved.Id})が実在するTrainOperationと一致しない"));
            }
        }

        // CutGroup.OperationId（Rule 5改訂）の検証
        //   ①Decouplingは必須：Cで構造的にrequiredのため未設定自体は型で防止済み
        //   ②ResolvedOperationRefならTrainOperation実在確認必須（Decoupling/Coupling共通）
        //   ③Decoupling内でProvisionalOperationRefのLabelが重複してはならない
        //   ④CouplingはProvisionalOperationRefのみ許容（Resolvedが来たら違反）、実在チェック対象外
        if (target.Type == StationWorkType.Decoupling)
        {
            foreach (var cg in target.CutGroups)
            {
                if (cg.OperationId is ResolvedOperationRef resolved &&
                    !context.TrainOperations.Any(o => o.Id == resolved.Id))
                {
                    issues.Add(new ValidationIssue(
                        $"StationWork(Decoupling): CutGroups(GroupIndex={cg.GroupIndex})のOperationId({resolved.Id})が実在するTrainOperationと一致しない"));
                }
            }

            var duplicatedLabels = target.CutGroups
                .Select(cg => cg.OperationId)
                .OfType<ProvisionalOperationRef>()
                .GroupBy(r => r.Label)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);
            foreach (var dup in duplicatedLabels)
            {
                issues.Add(new ValidationIssue(
                    $"StationWork(Decoupling): CutGroups間でProvisionalOperationRefのLabel({dup})が重複している（解結時の最小構成ごとに独立した運用が必要）"));
            }
        }
        else if (target.Type == StationWorkType.Coupling)
        {
            foreach (var cg in target.CutGroups)
            {
                if (cg.OperationId is ResolvedOperationRef)
                {
                    issues.Add(new ValidationIssue(
                        $"StationWork(Coupling): CutGroups(GroupIndex={cg.GroupIndex})はProvisionalOperationRefのみ許容（履歴の自由記述用途）"));
                }
                // ProvisionalOperationRef側は実在チェック対象外（現行どおり）。Label重複は許容（Couplingは履歴の自由記述のため）。
            }
        }

        // PrevTrainOperationOverrides内のCarCompositionId実在チェック
        //   ※直前Trainの実際のConsistBlocksに含まれるCarCompositionIdかどうかという横断検証（Rule 2相当）は
        //     単一StopTime内で完結しないため、TrainOperationCrossValidator（6.12.1節、再設計予定）側の責務とする
        foreach (var ovr in target.PrevTrainOperationOverrides)
        {
            if (!context.CarCompositions.Any(c => c.Id == ovr.CarCompositionId))
                issues.Add(new ValidationIssue(
                    $"StationWork(PrevTrain): PrevTrainOperationOverrides内のCarCompositionId({ovr.CarCompositionId})が存在しない"));
            if (!context.TrainOperations.Any(o => o.Id == ovr.NewOperationId))
                issues.Add(new ValidationIssue(
                    $"StationWork(PrevTrain): PrevTrainOperationOverrides内のNewOperationId({ovr.NewOperationId})が実在するTrainOperationと一致しない"));
        }

        return issues;
    }
}
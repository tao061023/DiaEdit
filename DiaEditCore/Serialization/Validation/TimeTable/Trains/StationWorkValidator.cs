using DiaEditCore.Model.TimeTable.Trains;

namespace DiaEditCore.Serialization.Validation.TimeTable.Trains;

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

        // StartOpConsistは対応するTypeでのみ使用可能
        if (target.Type != StationWorkType.StartOp && target.StartOpConsist.Count > 0)
            issues.Add(new ValidationIssue($"StationWork({target.Type}): StartOpConsistはStartOpでのみ使用可能"));

        var prevTrainOverridesAllowed = target.Type == StationWorkType.PrevTrain;
        if (!prevTrainOverridesAllowed && target.PrevTrainOperationOverrides.Count > 0)
            issues.Add(new ValidationIssue($"StationWork({target.Type}): PrevTrainOperationOverridesはPrevTrainでのみ使用可能"));

        // SplitOriginRefはPrevTrainにのみ付随する
        if (target.Type != StationWorkType.PrevTrain && target.SplitOrigin is not null)
            issues.Add(new ValidationIssue($"StationWork({target.Type}): SplitOriginはPrevTrainでのみ使用可能"));

        // vNEXT新設：DecouplingDetail/CouplingDetailはType別に排他（TODO：Rule番号は設計書確定時に付与）
        if (target.Type != StationWorkType.Decoupling && target.DecouplingDetail is not null)
            issues.Add(new ValidationIssue($"StationWork({target.Type}): DecouplingDetailはDecouplingでのみ使用可能"));
        if (target.Type != StationWorkType.Coupling && target.CouplingDetail is not null)
            issues.Add(new ValidationIssue($"StationWork({target.Type}): CouplingDetailはCouplingでのみ使用可能"));

        // 型別必須フィールド
        switch (target.Type)
        {
            case StationWorkType.StartOp:
                if (target.StartOpSeconds < 0)
                    issues.Add(new ValidationIssue("StationWork(StartOp): StartOpSecondsが未設定"));

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

            case StationWorkType.Decoupling:
                ValidateDecoupling(target, context, issues);
                break;

            case StationWorkType.Coupling:
                ValidateCoupling(target, context, issues);
                break;

            case StationWorkType.NextTrain:
                if (target.NextTrainType is null)
                    issues.Add(new ValidationIssue("StationWork(NextTrain): NextTrainTypeが未設定"));
                break;

            case StationWorkType.PrevTrain:
                // 追加の必須フィールドなし（PrevTrainOperationOverridesは省略可＝全Composition継承）
                break;
        }

        // StartOpConsist内のCarCompositionId実在チェック
        foreach (var slot in target.StartOpConsist)
        {
            if (!context.CarCompositions.Any(c => c.Id == slot.CarCompositionId))
                issues.Add(new ValidationIssue($"StationWork(StartOp): StartOpConsist内のCarCompositionId({slot.CarCompositionId})が存在しない"));
        }

        // StartOpCarSlot.OperationNumber（Rule 5系）の検証
        foreach (var slot in target.StartOpConsist)
        {
            if (string.IsNullOrWhiteSpace(slot.OperationNumber))
                issues.Add(new ValidationIssue(
                    $"StationWork(StartOp): StartOpConsist(Position={slot.Position})のOperationNumberが未設定"));
        }
        

        // PrevTrainOperationOverrides内のCarCompositionId実在チェック
        foreach (var ovr in target.PrevTrainOperationOverrides)
        {
            if (!context.CarCompositions.Any(c => c.Id == ovr.CarCompositionId))
                issues.Add(new ValidationIssue(
                    $"StationWork(PrevTrain): PrevTrainOperationOverrides内のCarCompositionId({ovr.CarCompositionId})が存在しない"));
        }

        return issues;
    }

    // Rule 5（再改訂）・Rule 7（置換）・Rule 8（新設）
    private static void ValidateDecoupling(StationWork target, ValidationContext context, List<IValidationIssue> issues)
    {
        if (target.StartOpSeconds < 0 || target.EndOpSeconds < 0)
            issues.Add(new ValidationIssue("StationWork(Decoupling): StartOpSeconds/EndOpSecondsは両方必須"));

        if (target.DecouplingDetail is not { } detail)
        {
            issues.Add(new ValidationIssue("StationWork(Decoupling): DecouplingDetailが未設定"));
            return;
        }

        // Rule 8：front/rearとも最低1件必須（常に2グループへ分かれるため）
        if (detail.FrontGroup.Count == 0)
            issues.Add(new ValidationIssue("StationWork(Decoupling): FrontGroupが空"));
        if (detail.RearGroup.Count == 0)
            issues.Add(new ValidationIssue("StationWork(Decoupling): RearGroupが空"));

        // Rule 7（置換）：front/rear間でCarCompositionIdの重複は不可
        var frontIds = detail.FrontGroup.Select(e => e.CarCompositionId).ToHashSet();
        var dupIds = detail.RearGroup.Select(e => e.CarCompositionId).Where(id => frontIds.Contains(id)).ToList();
        foreach (var dup in dupIds)
            issues.Add(new ValidationIssue($"StationWork(Decoupling): CarCompositionId({dup})がFrontGroupとRearGroup間で重複している"));

        var allEntries = detail.FrontGroup.Concat(detail.RearGroup).ToList();

        // CarCompositionId実在チェック
        foreach (var entry in allEntries)
        {
            if (!context.CarCompositions.Any(c => c.Id == entry.CarCompositionId))
                issues.Add(new ValidationIssue($"StationWork(Decoupling): CarCompositionId({entry.CarCompositionId})が存在しない"));
        }

        // Rule 5：OperationId(Resolved)の実在確認
        foreach (var entry in allEntries)
        {
            if (string.IsNullOrWhiteSpace(entry.OperationNumber))
                issues.Add(new ValidationIssue(
                    $"StationWork(Decoupling): CarCompositionId({entry.CarCompositionId})のOperationNumberが未設定"));
        }
    }

    // Rule 9（新設、暫定：単一StationWork内で検証可能な範囲のみ。相互整合はCross Validator行きの可能性あり）
    private static void ValidateCoupling(StationWork target, ValidationContext context, List<IValidationIssue> issues)
    {
        if (target.StartOpSeconds < 0 || target.EndOpSeconds < 0)
            issues.Add(new ValidationIssue("StationWork(Coupling): StartOpSeconds/EndOpSecondsは両方必須"));

        if (target.CouplingDetail is not { } detail)
        {
            issues.Add(new ValidationIssue("StationWork(Coupling): CouplingDetailが未設定"));
            return;
        }

        // Rule 9（単一StationWork内で検証可能な範囲）
        if (!context.Trains.Any(t => t.Id == detail.PartnerTrainId))
        {
            issues.Add(new ValidationIssue(
                $"StationWork(Coupling): PartnerTrainId({detail.PartnerTrainId})が存在しない"));
        }
        else
        {
            var partnerTrain = context.Trains.First(t => t.Id == detail.PartnerTrainId);
            if (!partnerTrain.StopTimes.ContainsKey(detail.PartnerStopKey))
            {
                issues.Add(new ValidationIssue(
                    $"StationWork(Coupling): PartnerStopKey({detail.PartnerStopKey})がPartnerTrainId({detail.PartnerTrainId})のStopTimesに存在しない"));
            }
        }

        // 「PartnerTrain側の当該StopTimeに、自Trainを指すCouplingWorkが対称的に存在するか」という
        // 相互整合チェックは、対象Trainの中身を横断的に参照する必要があるため単一StationWork内では
        // 完結しない。SplitOriginCrossValidatorと同型の問題として、Cross Validator側の責務とする。
    }
}
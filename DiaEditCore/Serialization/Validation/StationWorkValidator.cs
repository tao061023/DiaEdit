using DiaEditCore.Model;

namespace DiaEditCore.Serialization.Validation;

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

        // 型別必須フィールド（5.11.5節のコメントに基づく）
        switch (target.Type)
        {
            case StationWorkType.StartOp:
                if (target.StartOpSeconds < 0)
                    issues.Add(new ValidationIssue("StationWork(StartOp): StartOpSecondsが未設定"));
                if (target.TrainOperationId is null)
                    issues.Add(new ValidationIssue("StationWork(StartOp): TrainOperationIdが未設定"));
                break;

            case StationWorkType.EndOp:
                if (target.EndOpSeconds < 0)
                    issues.Add(new ValidationIssue("StationWork(EndOp): EndOpSecondsが未設定"));
                break;

            case StationWorkType.OpNumberChange:
                if (target.TrainOperationId is null)
                    issues.Add(new ValidationIssue("StationWork(OpNumberChange): TrainOperationIdが未設定"));
                // 「終着駅のStopTimeでのみ選択可能」の制約はStopKey・Train側の文脈が必要なため、
                // ここでは検証不可（Train単位の検証、または将来のSaveValidationRunnerで対応）
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
                if (target.CutPoints.Count == 0)
                    issues.Add(new ValidationIssue($"StationWork({target.Type}): CutPointsが空"));
                break;

            case StationWorkType.NextTrain:
                if (target.NextTrainType is null)
                    issues.Add(new ValidationIssue("StationWork(NextTrain): NextTrainTypeが未設定"));
                break;

            case StationWorkType.PrevTrain:
                // 現時点では追加の必須フィールドなし
                break;
        }

        // CutPoints内のCarConsistId実在チェック（Coupling/Decoupling/StartOpで使用）
        foreach (var cp in target.CutPoints)
        {
            if (!context.CarConsists.Any(c => c.Id == cp.CarConsistId))
                issues.Add(new ValidationIssue($"StationWork({target.Type}): CutPoints内のCarConsistId({cp.CarConsistId})が存在しない"));
        }

        return issues;
    }
}
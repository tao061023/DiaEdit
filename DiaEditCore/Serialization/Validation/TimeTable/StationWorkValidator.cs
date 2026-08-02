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

        // ★追加：StartOpConsist/CutPointsは、それぞれ対応するTypeでのみ使用可能
        //   （型分離によりStartOpConsistとCutPointsは別フィールドになったため、
        //    「片方しか値を持たない」という排他関係をここで構造的に検証する）
        if (target.Type != StationWorkType.StartOp && target.StartOpConsist.Count > 0)
            issues.Add(new ValidationIssue($"StationWork({target.Type}): StartOpConsistはStartOpでのみ使用可能"));

        var cutPointsAllowed = target.Type is StationWorkType.Coupling or StationWorkType.Decoupling;
        if (!cutPointsAllowed && target.CutPoints.Count > 0)
            issues.Add(new ValidationIssue($"StationWork({target.Type}): CutPointsはCoupling/Decouplingでのみ使用可能"));

        // 型別必須フィールド（5.11.5節のコメントに基づく）
        switch (target.Type)
        {
            case StationWorkType.StartOp:
                if (target.StartOpSeconds < 0)
                    issues.Add(new ValidationIssue("StationWork(StartOp): StartOpSecondsが未設定"));
                if (target.TrainOperationId is null)
                    issues.Add(new ValidationIssue("StationWork(StartOp): TrainOperationIdが未設定"));

                // StartOpConsist内のPosition重複禁止・0始まり連番（CarConsistValidatorと同様の検証）
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

        // CutPoints内のCarCompisitionId実在チェック（Coupling/Decouplingで使用）
        foreach (var cp in target.CutPoints)
        {
            if (!context.CarCompositions.Any(c => c.Id == cp.CarCompositionId))
                issues.Add(new ValidationIssue($"StationWork({target.Type}): CutPoints内のCarCompositionId({cp.CarCompositionId})が存在しない"));
        }

        // ★追加：StartOpConsist内のCarCompositionId実在チェック（StartOpで使用）
        foreach (var slot in target.StartOpConsist)
        {
            if (!context.CarCompositions.Any(c => c.Id == slot.CarCompositionId))
                issues.Add(new ValidationIssue($"StationWork(StartOp): StartOpConsist内のCarCompositionId({slot.CarCompositionId})が存在しない"));
        }

        return issues;
    }
}
using DiaEditCore.Model;

namespace DiaEditCore.Serialization.Validation;

/// <summary>
/// Train内のStationWork.works配列を実行順（5.11.5節Rule 4）に走査し、
/// StartOp起点からOpNumberChangeまでの運用番号推移をローカルに検証する。
///
/// スコープ（8.2節項目10）：
///   - 対象Trainが StartOp を持つ場合のみ、同一Train内で完結する検証を行う。
///   - PrevTrain経由で運用を継承したTrain（StartOpを持たないTrain）の OpNumberChange は、
///     TrainConnectionResolver（6.4節）・TrainOperationChainResolver（6.12節）が未実装のため
///     現時点では判定不能。誤検知（false positive）を避けるため検証をスキップする。
/// </summary>
public sealed class TrainOperationValidator : IValidator<Train>
{
    public IReadOnlyList<IValidationIssue> Validate(Train target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();

        // Rule 4：StopTime.worksの配列順が実行順の正。StopTime自体はStopKey.VisitSequenceで
        // Train内の訪問順に並べ替えてから走査する（Dictionary<StopKey, StopTime>はキー順序を保証しないため）。
        var orderedStopTimes = target.StopTimes
            .OrderBy(kv => kv.Key.VisitSequence)
            .ToList();

        TrainOperationId? currentOpId = null;

        foreach (var (stopKey, stopTime) in orderedStopTimes)
        {
            foreach (var work in stopTime.Works)
            {
                switch (work.Type)
                {
                    case StationWorkType.StartOp:
                        // StartOpは起点。TrainOperationIdの必須チェック自体はStationWorkValidatorの責務。
                        currentOpId = work.TrainOperationId;
                        break;

                    case StationWorkType.OpNumberChange:
                        if (work.TrainOperationId is null)
                        {
                            // 必須チェック自体はStationWorkValidatorの責務のため、ここでは重複報告しない。
                            break;
                        }

                        if (currentOpId is null)
                        {
                            // StartOpを持たない（PrevTrain経由で運用を継承した）Train。
                            // 「現在の運用番号」がTrain内では確定できないため、判定不能としてスキップする。
                            // （TrainConnectionResolver/TrainOperationChainResolver実装後に横断検証として対応、8.2節項目6）
                            break;
                        }

                        if (work.TrainOperationId.Value == currentOpId.Value)
                        {
                            issues.Add(new ValidationIssue(
                                $"Train({target.Id}).StopTimes[{stopKey}]: OpNumberChangeのTrainOperationId({work.TrainOperationId})が" +
                                $"現在の運用番号と同一（Rule 2違反：無意味な運用番号変更）"));
                        }

                        currentOpId = work.TrainOperationId;
                        break;

                    default:
                        // PrevTrain/EndOp/Shunting/Coupling/Decoupling/NextTrainはcurrentOpIdに影響しない
                        break;
                }
            }
        }

        return issues;
    }
}

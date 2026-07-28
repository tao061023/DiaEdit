using DiaEditCore.Model;
using DiaEditCore.Model.TimeTable;

namespace DiaEditCore.Serialization.Validation.Timetable;

public sealed class TrainCrossValidationData
{
    public IReadOnlyDictionary<TrainId, TrainOperationId> TrainOperationIndex { get; init; }
        = new Dictionary<TrainId, TrainOperationId>();
    public IReadOnlyDictionary<TrainId, TrainId> PrevTrainMap { get; init; }
        = new Dictionary<TrainId, TrainId>();
}

public sealed class TrainOperationValidator : IValidator<Train>
{
    // IValidator<Train>インターフェースの実装（契約を満たすために必須）。
    // 横断データが渡されないケース（単体テスト等）では判定不能として空を返す。
    public IReadOnlyList<IValidationIssue> Validate(Train target, ValidationContext context)
        => Validate(target, context, crossData: null);

    // TrainValidatorから直接呼ばれる本体。crossDataがあればRule 2を判定する。
    public IReadOnlyList<IValidationIssue> Validate(
        Train target,
        ValidationContext context,
        TrainCrossValidationData? crossData)
    {
        var issues = new List<IValidationIssue>();

        var prevTrainWork = target.StopTimes.Values
            .SelectMany(st => st.Works)
            .FirstOrDefault(w => w.Type == StationWorkType.PrevTrain);

        if (prevTrainWork?.TrainOperationId is not { } newOpId)
            return issues; // 省略＝継承のみ。変更なしなのでRule 2の対象外

        if (crossData is null)
            return issues; // 横断情報が無ければ判定不能

        if (crossData.PrevTrainMap.TryGetValue(target.Id, out var prevTrainId) &&
            crossData.TrainOperationIndex.TryGetValue(prevTrainId, out var prevOpId) &&
            newOpId.Value == prevOpId.Value)
        {
            issues.Add(new ValidationIssue(
                $"Train({target.Id}): PrevTrainのTrainOperationId({newOpId})が直前のTrain({prevTrainId})の運用番号と同一" +
                $"（Rule 2違反：無意味な運用番号変更）"));
        }

        return issues;
    }
}
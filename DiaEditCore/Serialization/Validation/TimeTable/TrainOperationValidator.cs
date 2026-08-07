using DiaEditCore.Model;
using DiaEditCore.Model.TimeTable;

namespace DiaEditCore.Serialization.Validation.Timetable;

public sealed class TrainCrossValidationData
{
    // v11.44改訂：CarComposition単位に変更。TrainOperationChainResolver.Resolve()の出力そのもの
    // （(Train,CarComposition)ホップごとのスナップショット）。
    public IReadOnlyDictionary<(TrainId TrainId, CarCompositionId CarCompositionId), TrainOperationId> TrainOperationIndex { get; init; }
        = new Dictionary<(TrainId, CarCompositionId), TrainOperationId>();
    public IReadOnlyDictionary<TrainId, TrainId> PrevTrainMap { get; init; }
        = new Dictionary<TrainId, TrainId>();
}

/// <summary>
/// Rule 2（改訂）：PrevTrainOperationOverrideの各NewOperationIdは、直前Trainにおける同一
/// CarCompositionIdの運用（TrainOperationIndex、Composition単位）と異なっていなければならない。
/// v11.44改訂前はTrain単位のスカラー比較だったが、Composition単位のリスト比較に変更した。
/// </summary>
public sealed class TrainOperationValidator : IValidator<Train>
{
    public IReadOnlyList<IValidationIssue> Validate(Train target, ValidationContext context)
        => Validate(target, context, crossData: null);

    public IReadOnlyList<IValidationIssue> Validate(
        Train target,
        ValidationContext context,
        TrainCrossValidationData? crossData)
    {
        var issues = new List<IValidationIssue>();

        var prevTrainWork = target.StopTimes.Values
            .SelectMany(st => st.Works)
            .FirstOrDefault(w => w.Type == StationWorkType.PrevTrain);

        if (prevTrainWork is null || prevTrainWork.PrevTrainOperationOverrides.Count == 0)
            return issues; // 省略＝全Composition継承のみ。変更なしなのでRule 2の対象外

        if (crossData is null)
            return issues; // 横断情報が無ければ判定不能

        if (!crossData.PrevTrainMap.TryGetValue(target.Id, out var prevTrainId))
            return issues; // 直前Trainが特定できない（起点Train等）

        foreach (var ovr in prevTrainWork.PrevTrainOperationOverrides)
        {
            if (crossData.TrainOperationIndex.TryGetValue((prevTrainId, ovr.CarCompositionId), out var prevOpId) &&
                ovr.NewOperationId.Value == prevOpId.Value)
            {
                issues.Add(new ValidationIssue(
                    $"Train({target.Id}): PrevTrainOperationOverride(CarCompositionId={ovr.CarCompositionId})の" +
                    $"NewOperationId({ovr.NewOperationId})が直前のTrain({prevTrainId})における同一Compositionの" +
                    $"運用番号と同一（Rule 2違反：無意味な運用番号変更）"));
            }
        }

        return issues;
    }
}
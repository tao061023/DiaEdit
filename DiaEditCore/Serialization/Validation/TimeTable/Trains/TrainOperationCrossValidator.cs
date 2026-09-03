namespace DiaEditCore.Serialization.Validation.TimeTable.Trains;

using DiaEditCore.Algorithm;
using DiaEditCore.Algorithm.CacheBuilder;
using DiaEditCore.Model;

/// <summary>
/// Rule 2（5.11.5節）専用の横断検証ランナー。TrainOperationValidatorの3引数版が要求する
/// TrainCrossValidationData（TrainOperationChainResolver・TrainConnectionResolverの全Train横断出力）を
/// 1回だけ構築し、全Trainに対して適用する。単一オブジェクトValidator（IValidator&lt;T&gt;）の
/// 契約に収まらない検証のための、保存時に個別呼び出しする専用ランナーであり、他Validatorを
/// 束ねる汎用SaveValidationRunnerではない（8.2節項目4はStationConnectionSegmentValidator、
/// 項目5はServiceRouteValidatorへ直接実装済みのため、横断検証として残るのはRule 2のみ）。
/// </summary>

public static class TrainOperationCrossValidator
{
    public static IReadOnlyList<IValidationIssue> Run(ValidationContext context, ProjectSettings settings)
    {
        var departureIndex = DepartureByStationTrackIndexBuilder.Build(context.Trains);
        var prevTrainMap = TrainConnectionResolver.ResolveUniquePrevTrainMap(context.Trains, departureIndex, settings);
        var trainOperationIndex = TrainOperationChainResolver.Resolve(
            context.Trains, departureIndex, context.TrainOperations, settings);

        var crossData = new TrainCrossValidationData
        {
            TrainOperationIndex = trainOperationIndex,
            PrevTrainMap = prevTrainMap,
            TrainOperationsById = context.TrainOperations.ToDictionary(o => o.Id),
        };

        var issues = new List<IValidationIssue>();
        var validator = new TrainOperationValidator();
        foreach (var train in context.Trains)
            issues.AddRange(validator.Validate(train, context, crossData));

        return issues;
    }
}
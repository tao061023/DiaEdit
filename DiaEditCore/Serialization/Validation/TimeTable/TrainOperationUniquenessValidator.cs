using DiaEditCore.Algorithm;
using DiaEditCore.Model;
using DiaEditCore.Model.TimeTable;

namespace DiaEditCore.Serialization.Validation.Timetable;

/// <summary>
/// §8.3項目1の方針（operationNumberは同一TimeTableSet内で常に一意）に基づく横断検証。
/// TrainOperation.OperationNumberの一意性はTimeTableSet単位でのみ意味を持ち、TrainOperation自体は
/// どのTimeTableSetに属するか正データを持たない（Train.StopTimes[].Works[].TrainOperationId経由の
/// 間接参照）ため、TrainOperationCrossValidatorと同じ「単一オブジェクトValidatorの契約に収まらない
/// 検証」専用の個別呼び出しランナーとして実装する。TimeTableSetCache.TrainOperationIndexは非永続の
/// 導出キャッシュであり保存時検証の入力に使わず、TrainOperationChainResolver.Resolveをその場で
/// （対象TimeTableSetのTrainのみに限定して）呼び出す。
/// </summary>
public static class TrainOperationUniquenessValidator
{
    public static IReadOnlyList<IValidationIssue> Run(ValidationContext context, ProjectSettings settings)
    {
        var issues = new List<IValidationIssue>();
        var trainOperationsById = context.TrainOperations.ToDictionary(o => o.Id);

        foreach (var timeTableSet in context.TimeTableSets)
        {
            var setTrains = context.Trains
                .Where(t => timeTableSet.TrainIds.Contains(t.Id))
                .ToList();
            if (setTrains.Count == 0)
                continue;

            var departureIndex = TrainConnectionResolver.BuildDepartureIndex(setTrains);
            var trainOperationIndex = TrainOperationChainResolver.Resolve(setTrains, departureIndex, settings);

            var seenByNumber = new Dictionary<string, TrainOperationId>();

            foreach (var opId in trainOperationIndex.Values.Distinct())
            {
                if (!trainOperationsById.TryGetValue(opId, out var op))
                    continue; // 参照整合性エラーは別途（TrainOperationId自体の実在確認は今回スコープ外）
                if (string.IsNullOrWhiteSpace(op.OperationNumber))
                    continue; // 非空チェックはTrainOperationNonEmptyValidator側の責務

                if (seenByNumber.TryGetValue(op.OperationNumber, out var existingId))
                {
                    issues.Add(new ValidationIssue(
                        $"TimeTableSet({timeTableSet.Id}): OperationNumber({op.OperationNumber})が" +
                        $"TrainOperation({existingId})とTrainOperation({opId})で重複している"));
                }
                else
                {
                    seenByNumber[op.OperationNumber] = opId;
                }
            }
        }

        return issues;
    }
}
namespace DiaEditCore.Serialization.Validation.TimeTable.Trains;

using DiaEditCore.Algorithm.CacheBuilder;
using DiaEditCore.Model;
using DiaEditCore.Model.TimeTable.Trains;

/// <summary>
/// 5.6.1節：RunTimeCalculatorの基準実績選定における一意性制約
/// （同一選定キー(StationConnectionSegmentId, FromIsStop, ToIsStop, DefaultVehicleTypeId)に
/// 該当するTrainがBaseTimeTableSet内に2件以上存在してはならない）を検証する。
///
/// StationConnectionSegmentOverlapCrossValidator／TrainOperationCrossValidatorと同じ
/// 「単一オブジェクトValidatorの契約（IValidator&lt;T&gt;）に収まらない検証」向けの
/// 静的Runパターンを踏襲する。ProjectSettingsへの依存が無いため、
/// StationConnectionSegmentOverlapCrossValidatorと同様RunはValidationContextのみを引数に取る。
///
/// ホップ→StationConnectionSegmentId解決は BaseRunTimeIndexBuilder.ResolveHopSegmentId を
/// 共用する（DRY原則。BaseRunTimeIndexBuilder自体は一意性違反時「後勝ち」で黙って上書きする
/// 防御的実装のため、一意性の強制自体は本Validatorの責務として分離されている）。
///
/// 対象は DiagramRevision.BaseTimeTableSetId が設定されている全DiagramRevisionそれぞれについて、
/// 独立に検証する（同一TimeTableSetが複数DiagramRevisionからBaseTimeTableSetIdとして参照される
/// ケースは現行モデル上想定しにくいが、DiagramRevision単位で走査することで自然にカバーされる）。
/// </summary>
public static class BaseTimeTableSetTrainDuplicationCrossValidator
{
    public static IReadOnlyList<IValidationIssue> Run(ValidationContext context)
    {
        var issues = new List<IValidationIssue>();
        var scById = context.StationConnections.ToDictionary(sc => sc.Id);

        foreach (var revision in context.DiagramRevisions)
        {
            if (revision.BaseTimeTableSetId is not { } baseTimeTableSetId) continue;

            var baseTrains = context.Trains
                .Where(t => t.TimeTableSetId == baseTimeTableSetId)
                .ToList();
            if (baseTrains.Count == 0) continue;

            // 選定キー → 該当TrainId一覧（同一Trainが同一キーへ複数ホップで寄与しても1件として数える）
            var seen = new Dictionary<BaseRunTimeIndexBuilder.SelectionKey, List<TrainId>>();

            foreach (var train in baseTrains)
            {
                if (train.RunSegments.Count == 0) continue;

                var visitedKeys = StopKeySequenceBuilder.BuildVisitedStopKeys(train);
                if (visitedKeys.Count != train.RunSegments.Count + 1) continue; // 不整合データはスキップ（別Validator管轄）

                for (var i = 0; i < train.RunSegments.Count; i++)
                {
                    var hop = train.RunSegments[i];
                    if (!scById.TryGetValue(hop.StationConnectionId, out var sc)) continue;

                    var scsId = BaseRunTimeIndexBuilder.ResolveHopSegmentId(
                        sc, hop.FromStationId, hop.ToStationId, context.StationConnectionSegments);
                    if (scsId is null) continue;

                    if (!train.StopTimes.TryGetValue(visitedKeys[i], out var fromStopTime)) continue;
                    if (!train.StopTimes.TryGetValue(visitedKeys[i + 1], out var toStopTime)) continue;

                    var key = new BaseRunTimeIndexBuilder.SelectionKey(
                        scsId.Value, fromStopTime.IsStop, toStopTime.IsStop, train.DefaultVehicleTypeId);

                    if (!seen.TryGetValue(key, out var trainIds))
                    {
                        trainIds = new List<TrainId>();
                        seen[key] = trainIds;
                    }
                    if (!trainIds.Contains(train.Id))
                    {
                        trainIds.Add(train.Id);
                    }
                }
            }

            foreach (var (key, trainIds) in seen)
            {
                if (trainIds.Count < 2) continue;

                issues.Add(new ValidationIssue(
                    $"DiagramRevision({revision.Id.Value})のBaseTimeTableSet({baseTimeTableSetId.Value})内で、" +
                    $"StationConnectionSegment({key.SegmentId.Value})・" +
                    $"停車パターン(From={(key.FromIsStop ? "停車" : "通過")}, To={(key.ToIsStop ? "停車" : "通過")})・" +
                    $"VehicleType({(key.VehicleTypeId is { } vt ? vt.Value.ToString() : "未設定")})" +
                    $"が一致する基準Trainが{trainIds.Count}件" +
                    $"（{string.Join(",", trainIds.Select(id => id.Value))}）存在します。" +
                    $"RunTimeCalculatorの基準実績選定には一意性が必要です（5.6.1節）。" +
                    $"性能差・停車パターン差のある基準列車をそれぞれ1本ずつ用意してください。"));
            }
        }

        return issues;
    }
}

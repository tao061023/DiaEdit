using DiaEditCore.Algorithm;
using DiaEditCore.Model.TimeTable.Trains;

namespace DiaEditCore.Serialization.Validation.Timetable;

public sealed class TrainValidator : IValidator<Train>
{
    private readonly StopTimeValidator _stopTimeValidator = new();
    // private readonly TrainOperationValidator _trainOperationValidator = new();
    private readonly DisplayNameValidator _displayNameValidator = new();

    public IReadOnlyList<IValidationIssue> Validate(Train target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();
        issues.AddRange(_displayNameValidator.Validate(target.TrainTypeName, context));

        if (string.IsNullOrWhiteSpace(target.TrainNumber))
            issues.Add(new ValidationIssue($"Train({target.Id}): TrainNumberが空"));

        // trainNumber一意性：本来はTimeTableSetCache.trainNumberIndex（5.13節）経由でO(1)引き当てすべきだが、
        // TimeTableSet未実装の現時点ではcontext.Trains全走査で代替する暫定実装
        if (context.Trains.Any(t => t.Id != target.Id && t.TrainNumber == target.TrainNumber))
            issues.Add(new ValidationIssue($"Train({target.Id}): TrainNumber({target.TrainNumber})が他のTrainと重複している"));

        if (!context.ServiceRoutes.Any(sr => sr.Id == target.ServiceRouteId))
            issues.Add(new ValidationIssue($"Train({target.Id}): ServiceRouteId({target.ServiceRouteId})が存在しない"));

        if (!context.TrainTypes.Any(tt => tt.Id == target.TrainTypeId))
            issues.Add(new ValidationIssue($"Train({target.Id}): TrainTypeId({target.TrainTypeId})が存在しない"));

        if (!context.VehicleTypes.Any(vt => vt.Id == target.DefaultVehicleTypeId))
            issues.Add(new ValidationIssue($"Train({target.Id}): DefaultVehicleTypeId({target.DefaultVehicleTypeId})が存在しない"));

        // sourceTrainId：自己参照禁止・多段参照禁止（5.11.2節バリデーションルール）
        if (target.SourceTrainId is { } sourceId)
        {
            if (sourceId == target.Id)
            {
                issues.Add(new ValidationIssue($"Train({target.Id}): SourceTrainIdが自分自身を指している"));
            }
            else
            {
                var sourceTrain = context.Trains.FirstOrDefault(t => t.Id == sourceId);
                if (sourceTrain is null)
                    issues.Add(new ValidationIssue($"Train({target.Id}): SourceTrainId({sourceId})が存在しない"));
                else if (sourceTrain.SourceTrainId is not null)
                    issues.Add(new ValidationIssue($"Train({target.Id}): SourceTrainId({sourceId})がさらにSourceTrainIdを持っている（多段参照禁止）"));
            }

            // baseTimeTableSet内のTrainはSourceTrainId=null必須（8.2節項目6、v11.24でDiagramRevision.BaseTimeTableSetId確定に伴い実装）。
            // 所属TimeTableSetを逆引きし、いずれかのDiagramRevisionがそれをBaseTimeTableSetIdとして指していればbase扱い。
            var owningSetId = context.TimeTableSets
                .FirstOrDefault(ts => ts.TrainIds.Contains(target.Id))?.Id;

            if (owningSetId is { } setId &&
                context.DiagramRevisions.Any(dr => dr.BaseTimeTableSetId == setId))
            {
                issues.Add(new ValidationIssue(
                    $"Train({target.Id}): baseTimeTableSet（TimeTableSetId={setId}）所属のTrainはSourceTrainIdを持てない"));
            }
        }

        // RunSegments：参照StationConnectionの実在確認、および
        // fromStationId/toStationIdが参照先StationConnectionの実際の駅間ホップとして
        // 存在するかの深い整合性検証（8.2節項目8）
        for (var i = 0; i < target.RunSegments.Count; i++)
        {
            var seg = target.RunSegments[i];
            var sc = context.StationConnections.FirstOrDefault(sc => sc.Id == seg.StationConnectionId);
            if (sc is null)
            {
                issues.Add(new ValidationIssue($"Train({target.Id}).RunSegments[{i}]: StationConnectionId({seg.StationConnectionId})が存在しない"));
                continue;
            }

            var resolvedSequence = EntryPointSequenceResolver.Resolve(sc, context.StationConnectionSegments);
            var hopExists = resolvedSequence.Any(e =>
                e.FromStationId == seg.FromStationId && e.ToStationId == seg.ToStationId);
            if (!hopExists)
            {
                issues.Add(new ValidationIssue(
                    $"Train({target.Id}).RunSegments[{i}]: fromStationId({seg.FromStationId})→toStationId({seg.ToStationId})が、" +
                    $"StationConnectionId({seg.StationConnectionId})が実際にカバーする駅間ホップと一致しない"));
            }
        }

        // StopTimes：各StopTime単体の検証を委譲
        foreach (var (key, stopTime) in target.StopTimes)
        {
            foreach (var issue in _stopTimeValidator.Validate(stopTime, context))
                issues.Add(new ValidationIssue($"Train({target.Id}).StopTimes[{key}]: {issue.Message}"));
        }

        // departureSeconds：「終着駅を除き必須」検証（8.2節項目7、v11.25）。
        // isStop==falseの場合はStopTimeValidator側でローカルに検証済みのため、ここではisStop==trueのみを対象とする。
        // 「終着駅かどうか」はTrain内の走行順を要するため、CarConsistResolver.BuildVisitedStopKeysが復元する
        // 訪問StopKey列の末尾要素と一致するかで判定する（単一の真実の源、6.7節参照）。
        var visitedKeys = CarConsistResolver.BuildVisitedStopKeys(target);
        var terminalKey = visitedKeys.Count > 0 ? visitedKeys[^1] : (StopKey?)null;

        foreach (var (key, stopTime) in target.StopTimes)
        {
            if (!stopTime.IsStop) continue; // 通過はStopTimeValidator側で検証済み
            if (stopTime.DepartureSeconds >= 0) continue;
            if (terminalKey is { } tk && key.Equals(tk)) continue; // 終着駅は許容

            issues.Add(new ValidationIssue(
                $"Train({target.Id}).StopTimes[{key}]: DepartureSecondsが未設定（終着駅以外では必須）"));
        }

        // 削除
        // TrainOperation関連（Rule 2、StartOp起点のローカル検証）：追加
        // foreach (var issue in _trainOperationValidator.Validate(target, context))
            // issues.Add(issue); // TrainOperationValidator側で既にTrain/StopTimeのコンテキストをメッセージに含めているため二重prefixしない

        return issues;
    }
}
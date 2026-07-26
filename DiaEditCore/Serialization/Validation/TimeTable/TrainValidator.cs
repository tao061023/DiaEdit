using DiaEditCore.Algorithm;
using DiaEditCore.Model.TimeTable;

namespace DiaEditCore.Serialization.Validation.Timetable;

public sealed class TrainValidator : IValidator<Train>
{
    private readonly StopTimeValidator _stopTimeValidator = new();
    private readonly TrainOperationValidator _trainOperationValidator = new();

    public IReadOnlyList<IValidationIssue> Validate(Train target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();

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
        // 「baseTimeTableSet内のTrainは必ずsourceTrainId=null」はTimeTableSet未実装のため検証不可（8.2節参照）
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

        // TrainOperation関連（Rule 2、StartOp起点のローカル検証）：追加
        foreach (var issue in _trainOperationValidator.Validate(target, context))
            issues.Add(issue); // TrainOperationValidator側で既にTrain/StopTimeのコンテキストをメッセージに含めているため二重prefixしない

        return issues;
    }
}
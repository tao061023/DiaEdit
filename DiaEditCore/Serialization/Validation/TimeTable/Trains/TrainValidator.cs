using DiaEditCore.Algorithm;
using DiaEditCore.Model.TimeTable.Trains;

namespace DiaEditCore.Serialization.Validation.TimeTable.Trains;

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

        // 所属TimeTableSetの解決（TrainNumber一意性・baseTimeTableSet検証・RunSegmentsStaleRuleで共用）
        var owningTimeTableSet = context.TimeTableSets.FirstOrDefault(ts => ts.TrainIds.Contains(target.Id));

        var duplicateTrainNumberExists = owningTimeTableSet is not null
            ? owningTimeTableSet.TrainIds
                .Where(id => id != target.Id)
                .Select(id => context.Trains.FirstOrDefault(t => t.Id == id))
                .Any(t => t is not null && t.TrainNumber == target.TrainNumber)
            : context.Trains.Any(t => t.Id != target.Id && t.TrainNumber == target.TrainNumber);

        if (duplicateTrainNumberExists)
            issues.Add(new ValidationIssue($"Train({target.Id}): TrainNumber({target.TrainNumber})が同一TimeTableSet内の他のTrainと重複している"));

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

            if (owningTimeTableSet is { } owningSet &&
                context.DiagramRevisions.Any(dr => dr.BaseTimeTableSetId == owningSet.Id))
            {
                issues.Add(new ValidationIssue(
                    $"Train({target.Id}): baseTimeTableSet（TimeTableSetId={owningSet.Id}）所属のTrainはSourceTrainIdを持てない"));
            }
        }

        // RunSegments：参照StationConnectionの実在確認、および
        // fromStationId/toStationIdが参照先StationConnectionの実際の駅間ホップとして
        // 存在するかの深い整合性検証（8.2節項目8）
        //
        // v12.29：EntryPointSequenceResolver.Resolve（系統(ii)）がallMainRoutesを要求する
        // シグネチャへ変更されたため追従。TrainRunSegment.FromStationId/ToStationId
        // （改称対象外、有向のまま）とEntryPointSequenceElement.FromStationId/ToStationId
        // （向き解決済みの出力として今回From/Toへ戻した）はどちらも変更不要で、
        // Resolve呼び出しへのcontext.MainRoutes追加のみが必要。
        for (var i = 0; i < target.RunSegments.Count; i++)
        {
            var seg = target.RunSegments[i];
            var sc = context.StationConnections.FirstOrDefault(sc => sc.Id == seg.StationConnectionId);
            if (sc is null)
            {
                issues.Add(new ValidationIssue($"Train({target.Id}).RunSegments[{i}]: StationConnectionId({seg.StationConnectionId})が存在しない"));
                continue;
            }

            var resolvedSequence = EntryPointSequenceResolver.Resolve(sc, context.StationConnectionSegments, context.MainRoutes);
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
        var visitedKeys = StopKeySequenceBuilder.BuildVisitedStopKeys(target);
        var terminalKey = visitedKeys.Count > 0 ? visitedKeys[^1] : (StopKey?)null;

        foreach (var (key, stopTime) in target.StopTimes)
        {
            if (!stopTime.IsStop) continue;
            if (stopTime.DepartureSeconds >= 0) continue;
            if (terminalKey is { } tk && key.Equals(tk)) continue;

            issues.Add(new ValidationIssue(
                $"Train({target.Id}).StopTimes[{key}]: DepartureSecondsが未設定（終着駅以外では必須）"));
        }


        // RunSegmentsの陳腐化検証（v12.5新設）
        if (context.ServiceRoutes.FirstOrDefault(sr => sr.Id == target.ServiceRouteId) is { } serviceRoute)
        {
            var resolvedOrder = ServiceRouteStationOrderResolver.ResolveServiceRouteStationOrder(serviceRoute, context.MainRoutes);

            if (resolvedOrder.Count > 0)
            {
                var actualOrder = visitedKeys.Select(k => k.StationId).ToList();

                if (!resolvedOrder.SequenceEqual(actualOrder))
                {
                    issues.Add(new ValidationIssue(
                        $"Train({target.Id}): 経路のStationOrderが変更されています。Trainを同期してください"));
                }
            }
        }
        return issues;
    }
}
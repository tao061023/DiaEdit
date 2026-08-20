using DiaEditCore.Model.TimeTable.Trains;

namespace DiaEditCore.Serialization.Validation.TimeTable.Trains;

public sealed class StopTimeValidator : IValidator<StopTime>
{
    private readonly StationWorkValidator _workValidator = new();

    // Rule4の判定用：各StationWorkが持つ「開始側」「終了側」の時刻を、type別に取り出す。
    // Shunting/Coupling/Decouplingは開始・終了の両方を持つ。StartOp/EndOpは片方のみ。
    // PrevTrain/NextTrain/OpNumberChangeは時刻を持たないため順序判定の対象外とする。
    private static (int? start, int? end) TimesOf(StationWork w) => w.Type switch
    {
        StationWorkType.StartOp => (w.StartOpSeconds >= 0 ? w.StartOpSeconds : null, null),
        StationWorkType.EndOp => (null, w.EndOpSeconds >= 0 ? w.EndOpSeconds : null),
        StationWorkType.Shunting or StationWorkType.Coupling or StationWorkType.Decoupling
            => (w.StartOpSeconds >= 0 ? w.StartOpSeconds : null, w.EndOpSeconds >= 0 ? w.EndOpSeconds : null),
        _ => (null, null),
    };

    public IReadOnlyList<IValidationIssue> Validate(StopTime target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();

        // TrackRailIdの実在チェック
        if (target.TrackRailId is { } railId && !context.Rails.Any(r => r.Id == railId))
            issues.Add(new ValidationIssue($"StopTime: TrackRailId({railId})が存在しない"));

        // Arrival/Departureが両方セットされている場合、DepartureSeconds >= ArrivalSecondsであること
        //   （ArrivalSeconds == -1は始発駅、DepartureSeconds == -1は終着駅・通過駅で正当に発生しうるため、
        //    「両方セットされている場合のみ」に限定する。終着駅・始発駅の判定自体はTrain内の順序情報が
        //    必要な横断検証のため、ここでは行わない。8.2節参照）
        if (target.ArrivalSeconds >= 0 && target.DepartureSeconds >= 0
            && target.DepartureSeconds < target.ArrivalSeconds)
        {
            issues.Add(new ValidationIssue(
                $"StopTime: DepartureSeconds({target.DepartureSeconds})がArrivalSeconds({target.ArrivalSeconds})より前になっている"));
        }

        // 各StationWork単体の検証（型別必須フィールド・CutPoints参照整合性）
        for (var i = 0; i < target.Works.Count; i++)
        {
            foreach (var issue in _workValidator.Validate(target.Works[i], context))
                issues.Add(new ValidationIssue($"Works[{i}]: {issue.Message}"));
        }

        // Rule 1: StartOpとPrevTrain、EndOpとNextTrainは同一StopTime内で共存不可
        var hasStartOp = target.Works.Any(w => w.Type == StationWorkType.StartOp);
        var hasPrevTrain = target.Works.Any(w => w.Type == StationWorkType.PrevTrain);
        if (hasStartOp && hasPrevTrain)
            issues.Add(new ValidationIssue("StopTime: StartOpとPrevTrainが同一StopTime内に共存している"));

        var hasEndOp = target.Works.Any(w => w.Type == StationWorkType.EndOp);
        var hasNextTrain = target.Works.Any(w => w.Type == StationWorkType.NextTrain);
        if (hasEndOp && hasNextTrain)
            issues.Add(new ValidationIssue("StopTime: EndOpとNextTrainが同一StopTime内に共存している"));

        // Rule 4: works配列順を実行順の正とし、各要素の時刻が配列順に単調非減少であること
        int? cursor = null;
        for (var i = 0; i < target.Works.Count; i++)
        {
            var (start, end) = TimesOf(target.Works[i]);

            if (start.HasValue && end.HasValue && end.Value < start.Value)
                issues.Add(new ValidationIssue($"Works[{i}]: EndOpSecondsがStartOpSecondsより前になっている"));

            var earliest = start ?? end;
            if (earliest.HasValue && cursor.HasValue && earliest.Value < cursor.Value)
                issues.Add(new ValidationIssue($"Works[{i}]: 配列順に対して時刻が逆行している（Rule4違反）"));

            var latest = end ?? start;
            if (latest.HasValue)
                cursor = cursor.HasValue ? Math.Max(cursor.Value, latest.Value) : latest.Value;
        }

        return issues;
    }
}
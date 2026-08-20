namespace DiaEditCore.Commands.Stations;

using DiaEditCore.Algorithm.Dependency;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Session;

/// <summary>
/// Station.DisplayName / Type / OperatingCode / TelegraphCode /
/// ShowsInStationTimetableOverride のスナップショット。
///
/// DisplayNameは参照型（class）のため、CaptureSnapshot時・コンストラクタでのnewValues受け取り時の
/// 両方でDisplayName.Clone()を経由し、外部インスタンスへの参照を一切保持しない（UndoableCommand基底の
/// 「不変な値にすること」規約への対応。DisplayNameそのものをrecord化する変更は影響範囲が大きいため
/// 見送り、Clone()による防御的コピーで対応する）。
/// </summary>
public sealed record StationSnapshot(
    DisplayName DisplayName,
    StationType Type,
    string OperatingCode,
    string TelegraphCode,
    bool? ShowsInStationTimetableOverride);

/// <summary>
/// 「属性変更」パターンの最初の具象実装。
/// AffectedIdsはStation自身のObjectIdからDependencyResolver.ResolveAffectedで算出する
/// </summary>
public sealed class ChangeStationAttributesCommand : UndoableCommand<Station, StationSnapshot>
{
    private readonly StationSnapshot _newValues;

    public ChangeStationAttributesCommand(Station target, StationSnapshot newValues, ProjectSession session)
        : base(target, BuildAffectedIds(target, session))
    {
        _newValues = newValues with { DisplayName = newValues.DisplayName.Clone() };
    }

    private static IReadOnlySet<ObjectId> BuildAffectedIds(Station target, ProjectSession session)
    {
        var cache = session.GetCache();
        return DependencyResolver.ResolveAffected(
            new HashSet<ObjectId> { new StationObjectId(target.Id) }, cache);
    }

    protected override StationSnapshot CaptureSnapshot(Station target) => new(
        target.DisplayName.Clone(),
        target.Type,
        target.OperatingCode,
        target.TelegraphCode,
        target.ShowsInStationTimetableOverride);

    protected override void Apply(Station target)
    {
        target.DisplayName = _newValues.DisplayName.Clone();
        target.Type = _newValues.Type;
        target.OperatingCode = _newValues.OperatingCode;
        target.TelegraphCode = _newValues.TelegraphCode;
        target.ShowsInStationTimetableOverride = _newValues.ShowsInStationTimetableOverride;
    }

    protected override void Restore(Station target, StationSnapshot snapshot)
    {
        target.DisplayName = snapshot.DisplayName.Clone();
        target.Type = snapshot.Type;
        target.OperatingCode = snapshot.OperatingCode;
        target.TelegraphCode = snapshot.TelegraphCode;
        target.ShowsInStationTimetableOverride = snapshot.ShowsInStationTimetableOverride;
    }
}
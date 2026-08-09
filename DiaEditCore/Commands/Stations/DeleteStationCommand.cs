namespace DiaEditCore.Commands.Stations;

using DiaEditCore.Algorithm;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.TimeTable;

/// <summary>
/// 6.1節「削除（Delete）」パターンの最初の具象実装。
///
/// 参照元が残ったまま削除された場合の扱い（6.1節の未確定事項、v12.11セッションで確定）：
/// execute時点で拒否する方針を採用。コンストラクタで直接の参照元（1ホップ、
/// DependencyResolver.ResolveDirectDependentsで判定）の存在を検査し、1件でもあれば
/// InvalidOperationExceptionを送出してコマンド生成自体を失敗させる
/// （SyncRunSegmentsToTrainCommandのCreate()失敗パターンを踏襲）。
///
/// 波及的な影響先（ResolveAffectedによる多ホップ探索）ではなく直接の参照元のみを判定対象とする。
/// ResolveAffectedはあくまで「削除時に道連れで無効化されるキャッシュの洗い出し」用途であり、
/// 「削除を妨げるべき参照」とは別の関心事であるため（例：StationConnectionSegmentはStationConnection
/// を経由して間接的にStationへ辿り着くが、これを理由に削除を拒むのは過剰）。
///
/// AffectedIdsはDependencyResolver.ResolveAffectedによる通常の波及算出（6.1節の削除パターン規約通り）。
/// StationはAppy/Restoreで内部フィールドを書き換えないため、TSnapshotはStation自身の参照を
/// そのまま保持する（Cloneは不要。属性変更パターンと異なり、削除パターンでは対象を書き換えず
/// リストへの出し入れのみを行うため、参照共有による事故が起きない）。
/// </summary>
public sealed class DeleteStationCommand : UndoableCommand<List<Station>, Station>
{
    private readonly Station _stationToDelete;

    public DeleteStationCommand(List<Station> stations, Station stationToDelete, TimeTableSetCache cache)
        : base(stations, BuildAffectedIds(stationToDelete, cache))
    {
        var directDependents = DependencyResolver
            .ResolveDirectDependents(new StationObjectId(stationToDelete.Id), cache)
            .ToList();

        if (directDependents.Count > 0)
        {
            throw new InvalidOperationException(
                $"Station（Id={stationToDelete.Id.Value}）は{directDependents.Count}件のオブジェクトから" +
                $"直接参照されているため削除できません。先に参照元（StationConnection等）を削除してください。");
        }

        _stationToDelete = stationToDelete;
    }

    private static IReadOnlySet<ObjectId> BuildAffectedIds(Station station, TimeTableSetCache cache) =>
        DependencyResolver.ResolveAffected(
            new HashSet<ObjectId> { new StationObjectId(station.Id) }, cache);

    protected override Station CaptureSnapshot(List<Station> target) => _stationToDelete;

    protected override void Apply(List<Station> target)
    {
        target.Remove(_stationToDelete);
    }

    protected override void Restore(List<Station> target, Station snapshot)
    {
        target.Add(snapshot);
    }
}
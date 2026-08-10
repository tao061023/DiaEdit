namespace DiaEditCore.Commands.Stations;

using DiaEditCore.Algorithm.Dependency;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.TimeTable;

/// <summary>
/// 6.1節「削除（Delete）」パターンのRail向け実装。DeleteStationCommand（v12.11）と同じ設計。
///
/// v12.13設計セッションでの確認：現行モデルではStationPath.Waypointsを含め、いかなるオブジェクトも
/// RailIdを直接参照として保持していない（Rail.EndpointA/EndpointBはRailが他オブジェクトを参照する
/// 向きであり、逆参照ではない）。DependencyResolverのグラフ定義（RailObjectId => []）はこの実態を
/// 正しく反映しており、削除時の1ホップチェックは常に空集合を返す（バグではなく仕様通り）。
/// そのためコンストラクタでの拒否は現状発生しないが、将来Railへの逆参照を持つモデルが追加された際に
/// DependencyResolver側のルールテーブルへ追加するだけで自動的に効くよう、Stationと同一のロジックを
/// そのまま適用する（個別モデルごとに削除可否ロジックを再実装しない、という一貫性を優先）。
/// </summary>
public sealed class DeleteRailCommand : UndoableCommand<List<Rail>, Rail>
{
    private readonly Rail _railToDelete;

    public DeleteRailCommand(List<Rail> rails, Rail railToDelete, TimeTableSetCache cache)
        : base(rails, BuildAffectedIds(railToDelete, cache))
    {
        var directDependents = DependencyResolver
            .ResolveDirectDependents(new RailObjectId(railToDelete.Id), cache)
            .ToList();

        if (directDependents.Count > 0)
        {
            throw new InvalidOperationException(
                $"Rail（Id={railToDelete.Id.Value}）は{directDependents.Count}件のオブジェクトから" +
                $"直接参照されているため削除できません。");
        }

        _railToDelete = railToDelete;
    }

    private static IReadOnlySet<ObjectId> BuildAffectedIds(Rail rail, TimeTableSetCache cache) =>
        DependencyResolver.ResolveAffected(
            new HashSet<ObjectId> { new RailObjectId(rail.Id) }, cache);

    protected override Rail CaptureSnapshot(List<Rail> target) => _railToDelete;

    protected override void Apply(List<Rail> target)
    {
        target.Remove(_railToDelete);
    }

    protected override void Restore(List<Rail> target, Rail snapshot)
    {
        target.Add(snapshot);
    }
}
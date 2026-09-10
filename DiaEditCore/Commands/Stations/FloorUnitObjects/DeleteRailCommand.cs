namespace DiaEditCore.Commands.Stations.FloorUnitObjects;

using DiaEditCore.Algorithm.Dependency;
using DiaEditCore.Algorithm.Stations;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;
using DiaEditCore.Model.TimeTable;
using DiaEditCore.Model.TimeTable.Trains;
using DiaEditCore.Session;

/// <summary>
/// Railを削除するコマンド。
/// </summary>
/// <remarks>
/// 以下のいずれかに該当する場合、削除を拒否する（例外送出、コレクション状態は変化しない）：
/// <list type="bullet">
/// <item>DependencyResolverのObjectIdグラフ上で、他オブジェクトから直接参照されている場合</item>
/// <item>Platform.FacingRailIdsから参照されている場合</item>
/// <item>TemporaryRestriction.Target（RestrictionTarget.Rail）から参照されている場合</item>
/// <item>Train.StopTimes[...].TrackRailIdから参照されている場合</item>
/// </list>
/// AffectedIds（変更通知対象）には、削除対象Rail自身に加え、その所属FloorUnitのObjectIdも
/// 含まれる。
/// </remarks>
public sealed class DeleteRailCommand : UndoableCommand<List<Rail>, Rail>
{
    private readonly Rail _railToDelete;

    /// <param name="rails">削除対象を保持するRailコレクション（Undo/Redoの対象コレクション）。</param>
    /// <param name="railToDelete">削除対象のRail。</param>
    /// <param name="session">依存関係チェック・変更通知範囲の算出に使うプロジェクトセッション。</param>
    /// <param name="allPlatforms">FacingRailIds参照チェック対象の全Platform。</param>
    /// <param name="allRestrictions">Target参照チェック対象の全TemporaryRestriction。</param>
    /// <param name="allTrains">StopTime.TrackRailId参照チェック対象の全Train。</param>
    public DeleteRailCommand(
        List<Rail> rails,
        Rail railToDelete,
        ProjectSession session,
        IReadOnlyList<Platform> allPlatforms,
        IReadOnlyList<TemporaryRestriction> allRestrictions,
        IReadOnlyList<Train> allTrains)
        : base(rails, BuildAffectedIds(railToDelete, session))
    {
        var cache = session.GetCache();

        // 1. ObjectIdグラフ経由の直接参照チェック
        var directDependents = DependencyResolver
            .ResolveDirectDependents(new RailObjectId(railToDelete.Id), cache)
            .ToList();

        if (directDependents.Count > 0)
        {
            throw new InvalidOperationException(
                $"Rail（Id={railToDelete.Id.Value}）は{directDependents.Count}件のオブジェクトから" +
                $"直接参照されているため削除できません。");
        }

        // 2. ObjectIdグラフ外の生RailId参照3経路チェック
        var reasons = new List<string>();

        var referencingPlatforms = allPlatforms
            .Where(p => p.FacingRailIds.Contains(railToDelete.Id))
            .Select(p => p.Id.Value)
            .ToList();
        if (referencingPlatforms.Count > 0)
        {
            reasons.Add($"Platform（Id={string.Join(",", referencingPlatforms)}）のFacingRailIds");
        }

        var referencingRestrictions = allRestrictions
            .Where(r => r.Target is RestrictionTarget.Rail rt && rt.RailId == railToDelete.Id)
            .Select(r => r.Id.Value)
            .ToList();
        if (referencingRestrictions.Count > 0)
        {
            reasons.Add($"TemporaryRestriction（Id={string.Join(",", referencingRestrictions)}）のTarget");
        }

        var referencingTrains = allTrains
            .Where(t => t.StopTimes.Values.Any(st => st.TrackRailId == railToDelete.Id))
            .Select(t => t.Id.Value)
            .ToList();
        if (referencingTrains.Count > 0)
        {
            reasons.Add($"Train（Id={string.Join(",", referencingTrains)}）のStopTime.TrackRailId");
        }

        if (reasons.Count > 0)
        {
            throw new InvalidOperationException(
                $"Rail（Id={railToDelete.Id.Value}）は以下から参照されているため削除できません：" +
                string.Join("／", reasons));
        }

        _railToDelete = railToDelete;
    }

    /// <summary>
    /// 削除実行前の状態を基に、変更通知対象のObjectId集合を算出する。
    /// </summary>
    /// <remarks>
    /// 戻り値には削除対象Rail自身に加え、その所属FloorUnitのObjectIdが含まれる。
    /// 所属FloorUnitはEndpointA側を優先して解決し、解決できない場合のみEndpointB側を試みる。
    /// </remarks>
    /// <param name="rail">削除対象のRail。</param>
    /// <param name="session">所属FloorUnit解決・依存関係波及の算出に使うプロジェクトセッション。</param>
    private static IReadOnlySet<ObjectId> BuildAffectedIds(Rail rail, ProjectSession session)
    {
        var cache = session.GetCache();

        var changedIds = new HashSet<ObjectId> { new RailObjectId(rail.Id) };

        var floorUnitId =
            RailFloorUnitLookup.ResolveFloorUnitId(
                rail.EndpointA,
                session.Current.NoneEndpoints, session.Current.BoundaryPoints,
                session.Current.EntryPoints, session.Current.BufferStops, session.Current.Switchers)
            ?? RailFloorUnitLookup.ResolveFloorUnitId(
                rail.EndpointB,
                session.Current.NoneEndpoints, session.Current.BoundaryPoints,
                session.Current.EntryPoints, session.Current.BufferStops, session.Current.Switchers);

        if (floorUnitId is { } id)
        {
            changedIds.Add(new FloorUnitObjectId(id));
        }

        return DependencyResolver.ResolveAffected(changedIds, cache);
    }

    /// <summary>削除対象のスナップショット（Undo復元用）を取得する。</summary>
    /// <param name="target">対象コレクション。</param>
    protected override Rail CaptureSnapshot(List<Rail> target) => _railToDelete;

    /// <summary>対象コレクションからRailを取り除く。</summary>
    /// <param name="target">対象コレクション。</param>
    protected override void Apply(List<Rail> target)
    {
        target.Remove(_railToDelete);
    }

    /// <summary>Undo時、削除したRailを対象コレクションへ復元する。</summary>
    /// <param name="target">対象コレクション。</param>
    /// <param name="snapshot">復元するRailのスナップショット。</param>
    protected override void Restore(List<Rail> target, Rail snapshot)
    {
        target.Add(snapshot);
    }
}
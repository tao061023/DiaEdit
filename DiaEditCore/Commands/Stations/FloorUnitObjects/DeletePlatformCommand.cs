namespace DiaEditCore.Commands.Stations.FloorUnitObjects;

using DiaEditCore.Algorithm.Dependency;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;
using DiaEditCore.Session;

/// <summary>
/// 「削除（Delete）」パターンのPlatform向け実装（§9.2項目34）。
///
/// DeleteRailCommandと異なり、Platformを直接参照する他モデルは現行実装スコープに存在しない
/// （TemporaryRestriction.TargetはRailのみを対象、Train.StopTime.TrackRailIdもRail参照であり
/// Platformは非参照）。よってDependencyResolver.ResolveDirectDependents経由のObjectIdグラフ
/// チェック（現状PlatformObjectId => []で終端）のみで削除可否判定が完結し、DeleteRailCommandの
/// ような「グラフ外の生ID参照を直接走査する」処理は不要。
///
/// 将来Platformへの参照を持つモデル（例：StationWork等）が追加された場合は、
/// DependencyResolver側のPlatformObjectIdケースを更新するだけで本コマンド自体は
/// 変更不要という設計になっている（構造的予防の方針）。
/// </summary>
public sealed class DeletePlatformCommand : UndoableCommand<List<Platform>, Platform>
{
    private readonly Platform _platformToDelete;

    public DeletePlatformCommand(
        List<Platform> platforms,
        Platform platformToDelete,
        ProjectSession session)
        : base(platforms, BuildAffectedIds(platformToDelete, session))
    {
        var cache = session.GetCache();

        var directDependents = DependencyResolver
            .ResolveDirectDependents(new PlatformObjectId(platformToDelete.Id), cache)
            .ToList();

        if (directDependents.Count > 0)
        {
            throw new InvalidOperationException(
                $"Platform（Id={platformToDelete.Id.Value}）は{directDependents.Count}件のオブジェクトから" +
                $"直接参照されているため削除できません。");
        }

        _platformToDelete = platformToDelete;
    }

    private static IReadOnlySet<ObjectId> BuildAffectedIds(Platform platform, ProjectSession session)
    {
        var cache = session.GetCache();
        return DependencyResolver.ResolveAffected(
            new HashSet<ObjectId> { new PlatformObjectId(platform.Id) }, cache);
    }

    protected override Platform CaptureSnapshot(List<Platform> target) => _platformToDelete;

    protected override void Apply(List<Platform> target) => target.Remove(_platformToDelete);

    protected override void Restore(List<Platform> target, Platform snapshot) => target.Add(snapshot);
}
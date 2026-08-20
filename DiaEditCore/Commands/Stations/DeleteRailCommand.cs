namespace DiaEditCore.Commands.Stations;

using DiaEditCore.Algorithm.Dependency;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.TimeTable;
using DiaEditCore.Model.TimeTable.Trains;
using DiaEditCore.Session;

/// <summary>
/// 「削除（Delete）」パターンのRail向け実装。
///
/// v12.18で判明した不備の修正：旧実装（v12.13）はDependencyResolverのObjectIdグラフ
/// （RailObjectId => []）のみをチェックしていたが、Railへの逆参照3経路は
/// いずれもObjectIdグラフの外側にある生のRailId参照であり、一度もチェックされていなかった：
///   1. Platform.FacingRailIds（List&lt;RailId&gt;）
///   2. TemporaryRestriction.Target is RestrictionTarget.Rail
///   3. Train.StopTimes[...].TrackRailId（RailId?）
///
/// これら3経路は、TemporaryRestrictionBySegmentIndexBuilderのコメントで明言した方針
/// （「Rail起点の逆引き消費者はDeleteRailCommandのみであり、Rail削除時のチェックは
/// 対象コレクションを直接1回線形走査すれば足りる規模のため、専用インデックス化は見送る」）
/// に従い、専用キャッシュを設けずコンストラクタ内で直接走査する。
///
/// DependencyResolverのObjectIdグラフチェックも引き続き実施する（将来Railへの
/// ObjectId経由の逆参照を持つモデルが追加された場合に自動的に効くようにするため）。
///
/// v12.21：コンストラクタ引数をTimeTableSetCache cache → ProjectSession sessionへ移行
/// （§9.1項目5、構造的防止の方針）。Platform／TemporaryRestriction／Trainの3コレクションは
/// TimeTableSetCacheが管理する対象ではない（ProjectFileの生データ）ため、引き続き
/// 呼び出し側から個別に受け取る（ProjectSessionはこれらのコレクション自体を集約管理しない。
/// 5.14.2節：ProjectSessionの責務はTimeTableSetCacheのライフサイクル管理に限定）。
/// </summary>
public sealed class DeleteRailCommand : UndoableCommand<List<Rail>, Rail>
{
    private readonly Rail _railToDelete;

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

        // 1. ObjectIdグラフ経由の直接参照チェック（現状は常に空だが、将来のモデル追加に備えて維持）
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

    private static IReadOnlySet<ObjectId> BuildAffectedIds(Rail rail, ProjectSession session)
    {
        var cache = session.GetCache();
        return DependencyResolver.ResolveAffected(
            new HashSet<ObjectId> { new RailObjectId(rail.Id) }, cache);
    }

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
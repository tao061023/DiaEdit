namespace DiaEditCore.Commands.Stations.FloorUnitObjects;

using DiaEditCore.Algorithm.Stations.FloorUnitObjects;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;

/// <summary>
/// 収束変換ワークフロー（§9.2項目33・35）の内部ステップ：複数のRail端点参照（RailEndpointRef）を
/// 一括で張り替える専用コマンド。ChangeRailAttributesCommandのスコープ（EndpointA/Bを除く4フィールド、
/// v13.13確定のサイドパネル仕様）を拡張せず、別クラスとして切り出す。
///
/// 個々のRailごとに専用コマンドを発行せず、収束操作全体（N本のRailの該当端を1つの新しい
/// 参照先へ同時に付け替える）を1つのUndo単位として扱う。RailEndpointConvergenceResolver.
/// FindConvergingEndpointsが返す収束集合をそのまま_targetsへ渡せる形にしている。
///
/// 「収束（Converge）」＝複数Rail端点を1つの新しい参照先へ集約する操作をここで実装する。
/// 「発散（Diverge）」＝RailSplitter（§9.2項目38、RailMergerの逆操作）に相当する対称操作は
/// 未設計・未実装のため、本クラスには含めない（設計確定後に対となるコマンドとして追加する）。
///
/// AffectedIdsについて：呼び出し側（Converge*Command）が、張替え対象Rail群のRailObjectIdに加え、
/// 消滅する旧端点オブジェクトのObjectId・新規作成した端点オブジェクトのObjectIdを
/// DependencyResolver.ResolveAffectedへ渡した結果を、コンストラクタ引数として明示的に受け取る
/// （Create系コマンドと異なり、張替え対象は呼び出し時点で全て確定しているため、
/// ComputeAffectedIdsAfterApplyフックは不要）。
/// </summary>
public sealed class RailEndPointRefChanger
    : UndoableCommand<List<Rail>, IReadOnlyList<(RailId RailId, RailEnd End, RailEndpointRef OldRef)>>
{
    private readonly IReadOnlyList<(RailId RailId, RailEnd End)> _targets;
    private readonly RailEndpointRef _newRef;

    /// <summary>
    /// targets：張替え対象の(RailId, RailEnd)組（収束集合、RailEndpointConvergenceResolver.
    /// FindConvergingEndpointsの戻り値からRef部分を除いたもの）。
    /// newRef：張替え後、全対象が共通して指す新しい参照（新規BoundaryPoint／新規または拡張後Switcherの
    /// SwitcherEndpointRef＋対応PortIndexなど、呼び出し側で確定済みのもの）。
    /// </summary>
    public RailEndPointRefChanger(
        List<Rail> rails,
        IReadOnlyList<(RailId RailId, RailEnd End)> targets,
        RailEndpointRef newRef,
        IReadOnlySet<ObjectId> affectedIds)
        : base(rails, affectedIds)
    {
        if (targets.Count == 0)
            throw new ArgumentException("targetsは1件以上必要です。", nameof(targets));

        _targets = targets;
        _newRef = newRef;
    }

    protected override IReadOnlyList<(RailId RailId, RailEnd End, RailEndpointRef OldRef)> CaptureSnapshot(List<Rail> target)
    {
        var snapshot = new List<(RailId, RailEnd, RailEndpointRef)>();
        foreach (var (railId, end) in _targets)
        {
            var rail = FindRail(target, railId);
            var oldRef = end == RailEnd.A ? rail.EndpointA : rail.EndpointB;
            snapshot.Add((railId, end, oldRef));
        }
        return snapshot;
    }

    protected override void Apply(List<Rail> target)
    {
        foreach (var (railId, end) in _targets)
        {
            var rail = FindRail(target, railId);
            if (end == RailEnd.A) rail.EndpointA = _newRef;
            else rail.EndpointB = _newRef;
        }
    }

    protected override void Restore(
        List<Rail> target,
        IReadOnlyList<(RailId RailId, RailEnd End, RailEndpointRef OldRef)> snapshot)
    {
        foreach (var (railId, end, oldRef) in snapshot)
        {
            var rail = FindRail(target, railId);
            if (end == RailEnd.A) rail.EndpointA = oldRef;
            else rail.EndpointB = oldRef;
        }
    }

    private static Rail FindRail(List<Rail> rails, RailId id) =>
        rails.FirstOrDefault(r => r.Id.Equals(id))
        ?? throw new InvalidOperationException($"RailEndPointRefChanger: Rail(Id={id.Value})が見つかりません。");
}
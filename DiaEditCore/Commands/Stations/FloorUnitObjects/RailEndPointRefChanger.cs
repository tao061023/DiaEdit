namespace DiaEditCore.Commands.Stations.FloorUnitObjects;

using DiaEditCore.Algorithm.Stations.FloorUnitObjects;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;

/// <summary>
/// 収束変換ワークフローの内部ステップ：複数のRail端点参照（RailEndpointRef）を
/// 一括で張り替える専用コマンド。
/// </summary>
/// <remarks>
/// ChangeRailAttributesCommandのスコープ（EndpointA/Bを除く4フィールド、v13.13確定のサイドパネル仕様）を
/// 拡張せず、別クラスとして切り出す。
///
/// <para>
/// 個々のRailごとに専用コマンドを発行せず、収束操作全体（N本のRailの該当端を1つの新しい
/// 参照先へ同時に付け替える）を1つのUndo単位として扱う。
/// <see cref="RailEndpointConvergenceResolver.FindConvergingEndpoints"/>が返す収束集合を
/// そのまま_targetsへ渡せる形にしている。
/// </para>
///
/// <para>
/// 「収束（Converge）」＝複数Rail端点を1つの新しい参照先へ集約する操作をここで実装する。
/// 「発散（Diverge）」＝RailSplitter（RailMergerの逆操作）に相当する対称操作は
/// 未設計・未実装のため、本クラスには含めない（設計確定後に対となるコマンドとして追加する）。
/// </para>
///
/// <para>
/// AffectedIdsについて：呼び出し側（Converge*Command）が、張替え対象Rail群のRailObjectIdに加え、
/// 消滅する旧端点オブジェクトのObjectId・新規作成した端点オブジェクトのObjectIdを
/// DependencyResolver.ResolveAffectedへ渡した結果を、コンストラクタ引数として明示的に受け取る
/// （Create系コマンドと異なり、張替え対象は呼び出し時点で全て確定しているため、
/// ComputeAffectedIdsAfterApplyフックは不要）。
/// </para>
/// </remarks>
public sealed class RailEndPointRefChanger
    : UndoableCommand<List<Rail>, IReadOnlyList<(RailId RailId, RailEnd End, RailEndpointRef OldRef)>>
{
    private readonly IReadOnlyList<(RailId RailId, RailEnd End)> _targets;
    private readonly RailEndpointRef _newRef;

    /// <summary>
    /// コマンドを構築する。この時点では張替えは行われない。
    /// </summary>
    /// <param name="rails">張替え対象のRailを含むリスト。</param>
    /// <param name="targets">
    /// 張替え対象の(RailId, RailEnd)組（収束集合、
    /// <see cref="RailEndpointConvergenceResolver.FindConvergingEndpoints"/>の戻り値からRef部分を除いたもの）。
    /// </param>
    /// <param name="newRef">
    /// 張替え後、全対象が共通して指す新しい参照（新規BoundaryPoint／新規または拡張後Switcherの
    /// SwitcherEndpointRef＋対応PortIndexなど、呼び出し側で確定済みのもの）。
    /// </param>
    /// <param name="affectedIds">呼び出し元が事前に確定させたAffectedIds。</param>
    /// <exception cref="ArgumentException"><paramref name="targets"/>が空の場合。</exception>
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

    /// <summary>
    /// 張替え対象各RailについてExecute前の(RailId, RailEnd, 旧参照)の組をスナップショットとして取得する。
    /// </summary>
    /// <param name="target">Railを含むリスト。</param>
    /// <returns>張替え対象ごとの旧参照を保持するリスト。</returns>
    /// <exception cref="InvalidOperationException">対象Railが<paramref name="target"/>に見つからない場合。</exception>
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

    /// <summary>
    /// 張替え対象の全端点を<see cref="_newRef"/>へ変更する。
    /// </summary>
    /// <param name="target">Railを含むリスト。</param>
    /// <exception cref="InvalidOperationException">対象Railが<paramref name="target"/>に見つからない場合。</exception>
    protected override void Apply(List<Rail> target)
    {
        foreach (var (railId, end) in _targets)
        {
            var rail = FindRail(target, railId);
            if (end == RailEnd.A) rail.EndpointA = _newRef;
            else rail.EndpointB = _newRef;
        }
    }

    /// <summary>
    /// 張替え対象の全端点を、張替え前の参照へ戻す。
    /// </summary>
    /// <param name="target">Railを含むリスト。</param>
    /// <param name="snapshot"><see cref="CaptureSnapshot"/>が返したスナップショット。</param>
    /// <exception cref="InvalidOperationException">対象Railが<paramref name="target"/>に見つからない場合。</exception>
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

    /// <summary>
    /// リストから指定Idの<see cref="Rail"/>を線形探索する内部ヘルパー。
    /// </summary>
    /// <param name="rails">探索対象のリスト。</param>
    /// <param name="id">探索するRailId。</param>
    /// <returns>一致した<see cref="Rail"/>インスタンス。</returns>
    /// <exception cref="InvalidOperationException"><paramref name="id"/>に一致するRailが見つからない場合。</exception>
    private static Rail FindRail(List<Rail> rails, RailId id) =>
        rails.FirstOrDefault(r => r.Id.Equals(id))
        ?? throw new InvalidOperationException($"RailEndPointRefChanger: Rail(Id={id.Value})が見つかりません。");
}
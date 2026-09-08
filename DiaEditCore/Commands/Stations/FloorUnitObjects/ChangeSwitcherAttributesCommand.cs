namespace DiaEditCore.Commands.Stations.FloorUnitObjects;

using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;

/// <summary>
/// Switcherの属性変更コマンド（PortCount／Mechanism／ValidRoutesの3フィールド）。
/// </summary>
/// <remarks>
/// 「属性変更コマンドのスコープ分割方針」に基づき検討した結果、Switcherは
/// Rail（純粋属性／接続トポロジー／形状データの3分割）のような分割対象を持たない
/// （Base.Position・FloorUnitIdはSwitcher単体では変更されず、収束変換ワークフロー側の
/// 責務のため本コマンドのスコープには含めない）。3フィールドとも「フォーム一括保存で完結する」
/// 単一の編集単位と判断し、分割しない。
///
/// <para>
/// 呼び出し元は主に2通り：
/// (1) UI側の通常編集（Switcher選択→Port関連設定パネルでMechanism/ValidRoutesを編集→決定）。
/// (2) <see cref="RailEndpointConvergenceWorkflow.Reconcile"/>のSwitcherExpandケース
/// （N=3,4への収束によるPortCount拡張）。この場合はMechanism/ValidRoutesをクリア（null／空配列）した
/// 状態で一旦確定させ、ユーザーに再設定を促す（v13.13確定仕様）。この呼び出しでは新しいMechanism/ValidRoutes
/// の入力は伴わない＝クリアするだけの1回のコマンド発行で足りる。
/// </para>
/// </remarks>
public sealed class ChangeSwitcherAttributesCommand
    : UndoableCommand<List<Switcher>, (int PortCount, SwitchMechanism? Mechanism, IReadOnlyList<PortPair> ValidRoutes)>
{
    private readonly SwitcherId _switcherId;
    private readonly int _newPortCount;
    private readonly SwitchMechanism? _newMechanism;
    private readonly IReadOnlyList<PortPair> _newValidRoutes;

    /// <summary>
    /// コマンドを構築する。この時点では属性変更は行われない。
    /// </summary>
    /// <param name="switchers">変更対象のSwitcherを含むリスト。</param>
    /// <param name="switcherId">変更対象SwitcherのId。</param>
    /// <param name="newPortCount">変更後のPortCount。</param>
    /// <param name="newMechanism">変更後のMechanism。クリアする場合はnull。</param>
    /// <param name="newValidRoutes">
    /// 変更後のValidRoutes。呼び出し元がUI編集用に保持する配列と共有される可能性があるため、
    /// コンストラクタ内で防御的コピーを行う。
    /// </param>
    /// <param name="affectedIds">呼び出し元が事前に確定させたAffectedIds。</param>
    public ChangeSwitcherAttributesCommand(
        List<Switcher> switchers,
        SwitcherId switcherId,
        int newPortCount,
        SwitchMechanism? newMechanism,
        IReadOnlyList<PortPair> newValidRoutes,
        IReadOnlySet<ObjectId> affectedIds)
        : base(switchers, affectedIds)
    {
        _switcherId = switcherId;
        _newPortCount = newPortCount;
        // Mechanismはimmutableな入力値として扱う（呼び出し側が使い回さない前提、防御的コピー不要）。
        _newMechanism = newMechanism;
        // ValidRoutesは呼び出し元がUI編集用に保持する配列と共有される可能性があるため防御的コピー。
        _newValidRoutes = newValidRoutes.ToList();
    }

    /// <summary>
    /// 変更対象Switcherの現在値（PortCount／Mechanism／ValidRoutes）をスナップショットとして取得する。
    /// </summary>
    /// <param name="target">Switcherを含むリスト。</param>
    /// <returns>変更前のPortCount／Mechanism／ValidRoutes（ValidRoutesは防御的コピー済み）。</returns>
    /// <exception cref="InvalidOperationException">対象Switcherが<paramref name="target"/>に見つからない場合。</exception>
    protected override (int PortCount, SwitchMechanism? Mechanism, IReadOnlyList<PortPair> ValidRoutes) CaptureSnapshot(
        List<Switcher> target)
    {
        var switcher = FindSwitcher(target, _switcherId);
        // 既存ValidRoutesも防御的コピーしてスナップショットへ格納（Restoreで配列参照を共有しないため）。
        return (switcher.PortCount, switcher.Mechanism, switcher.ValidRoutes.ToArray());
    }

    /// <summary>
    /// 対象SwitcherのPortCount／Mechanism／ValidRoutesを新しい値へ変更する。
    /// </summary>
    /// <param name="target">Switcherを含むリスト。</param>
    /// <exception cref="InvalidOperationException">対象Switcherが<paramref name="target"/>に見つからない場合。</exception>
    protected override void Apply(List<Switcher> target)
    {
        var switcher = FindSwitcher(target, _switcherId);
        switcher.PortCount = _newPortCount;
        switcher.Mechanism = _newMechanism;
        switcher.ValidRoutes = _newValidRoutes.ToList();
    }

    /// <summary>
    /// 対象SwitcherのPortCount／Mechanism／ValidRoutesを変更前の状態へ戻す。
    /// </summary>
    /// <param name="target">Switcherを含むリスト。</param>
    /// <param name="snapshot"><see cref="CaptureSnapshot"/>が返したスナップショット。</param>
    /// <exception cref="InvalidOperationException">対象Switcherが<paramref name="target"/>に見つからない場合。</exception>
    protected override void Restore(
        List<Switcher> target,
        (int PortCount, SwitchMechanism? Mechanism, IReadOnlyList<PortPair> ValidRoutes) snapshot)
    {
        var switcher = FindSwitcher(target, _switcherId);
        switcher.PortCount = snapshot.PortCount;
        switcher.Mechanism = snapshot.Mechanism;
        switcher.ValidRoutes = snapshot.ValidRoutes.ToList();
    }

    /// <summary>
    /// リストから指定Idの<see cref="Switcher"/>を線形探索する内部ヘルパー。
    /// </summary>
    /// <param name="switchers">探索対象のリスト。</param>
    /// <param name="id">探索するSwitcherId。</param>
    /// <returns>一致した<see cref="Switcher"/>インスタンス。</returns>
    /// <exception cref="InvalidOperationException"><paramref name="id"/>に一致するSwitcherが見つからない場合。</exception>
    private static Switcher FindSwitcher(List<Switcher> switchers, SwitcherId id) =>
        switchers.FirstOrDefault(s => s.Id.Equals(id))
        ?? throw new InvalidOperationException($"ChangeSwitcherAttributesCommand: Switcher(Id={id.Value})が見つかりません。");
}
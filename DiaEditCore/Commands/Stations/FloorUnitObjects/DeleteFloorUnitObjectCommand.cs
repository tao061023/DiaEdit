namespace DiaEditCore.Commands.Stations.FloorUnitObjects;

using DiaEditCore.Model;

/// <summary>
/// CreateFloorUnitObjectCommand&lt;TId, T&gt;と対称な、汎用「削除（Delete）」パターンの実装。
/// </summary>
/// <typeparam name="TId">削除対象のId型。</typeparam>
/// <typeparam name="T">削除対象のモデル型（BoundaryPoint／BufferStop／NoneEndpoint／EntryPoint／Switcherなど）。</typeparam>
/// <remarks>
/// BoundaryPoint／BufferStop／NoneEndpoint／EntryPoint／Switcherのいずれも
/// 「Listから対象1件をRemoveするだけ」という同型の削除操作のため、個別クラスを持たず本コマンドを使う。
///
/// <para>
/// 無条件削除である点に注意：本コマンドは参照チェックを一切行わない。
/// 呼び出し元（収束変換ワークフロー、Converge*Command）が、
/// (1) <see cref="Algorithm.Stations.FloorUnitObjects.RailEndpointConvergenceResolver.FindBlockingStationPaths"/>
/// によるStationPath参照チェック、
/// (2) <see cref="RailEndPointRefChanger"/>による、削除対象を参照していたRail端点の張替え完了、
/// を本コマンドの実行前に必ず済ませておく前提とする（TransActionCommand内の実行順序：
/// 新規移行先作成 → RailEndPointRefChangerで参照を移行先へ張替え → 本コマンドで旧端点を削除）。
/// この順序を守れば、本コマンドが実行される時点で削除対象を参照するRailは存在しない
/// （DeleteRailCommandのような実行時の参照元チェックは不要、構造的に安全）。
/// </para>
///
/// <para>
/// AffectedIdsはDelete系の通常パターン通り、呼び出し元がコンストラクタで確定値を渡す
/// （Create系と異なりExecute前から削除対象のIdが判明しているため、
/// ComputeAffectedIdsAfterApplyのオーバーライドは不要）。
/// </para>
/// </remarks>
public sealed class DeleteFloorUnitObjectCommand<TId, T> : UndoableCommand<List<T>, T>
    where TId : struct, IIntId
    where T : class
{
    private readonly T _toDelete;

    /// <summary>
    /// コマンドを構築する。この時点では削除は行われない（<see cref="UndoableCommand{TTarget, TSnapshot}.Execute"/>まで副作用なし）。
    /// </summary>
    /// <param name="target">削除対象を含むリスト。</param>
    /// <param name="toDelete">削除対象のインスタンス。</param>
    /// <param name="affectedIds">
    /// 呼び出し元が事前に確定させたAffectedIds。本コマンドは参照チェックを行わないため、
    /// 影響範囲の妥当性は呼び出し元の責務。
    /// </param>
    public DeleteFloorUnitObjectCommand(
        List<T> target,
        T toDelete,
        IReadOnlySet<ObjectId> affectedIds)
        : base(target, affectedIds)
    {
        _toDelete = toDelete;
    }

    /// <summary>
    /// 削除対象インスタンス自身をスナップショットとして返す。
    /// </summary>
    /// <param name="target">削除対象を含むリスト（未使用）。</param>
    /// <returns>コンストラクタで受け取った削除対象インスタンス。</returns>
    protected override T CaptureSnapshot(List<T> target) => _toDelete;

    /// <summary>
    /// 削除対象を<paramref name="target"/>から取り除く。
    /// </summary>
    /// <param name="target">削除対象を含むリスト。</param>
    protected override void Apply(List<T> target)
    {
        target.Remove(_toDelete);
    }

    /// <summary>
    /// 削除前の状態へ戻す（削除対象インスタンスを<paramref name="target"/>へ再追加する）。
    /// </summary>
    /// <param name="target">削除対象を含むリスト。</param>
    /// <param name="snapshot"><see cref="CaptureSnapshot"/>が返したスナップショット（削除対象インスタンス）。</param>
    protected override void Restore(List<T> target, T snapshot)
    {
        target.Add(snapshot);
    }
}
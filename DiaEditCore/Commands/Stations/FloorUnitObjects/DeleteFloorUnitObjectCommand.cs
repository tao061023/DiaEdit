namespace DiaEditCore.Commands.Stations.FloorUnitObjects;

using DiaEditCore.Model;

/// <summary>
/// CreateFloorUnitObjectCommand&lt;TId, T&gt;と対称な、汎用「削除（Delete）」パターンの実装。
/// BoundaryPoint／BufferStop／NoneEndpoint／EntryPoint／Switcherのいずれも
/// 「Listから対象1件をRemoveするだけ」という同型の削除操作のため、個別クラスを持たず本コマンドを使う。
///
/// 無条件削除である点に注意：本コマンドは参照チェックを一切行わない。
/// 呼び出し元（収束変換ワークフロー、Converge*Command）が、
///   1. RailEndpointConvergenceResolver.FindBlockingStationPaths によるStationPath参照チェック
///   2. RailEndPointRefChanger による、削除対象を参照していたRail端点の張替え完了
/// を本コマンドの実行前に必ず済ませておく前提とする（TransActionCommand内の実行順序：
/// 新規移行先作成 → RailEndPointRefChangerで参照を移行先へ張替え → 本コマンドで旧端点を削除）。
/// この順序を守れば、本コマンドが実行される時点で削除対象を参照するRailは存在しない
/// （DeleteRailCommandのような実行時の参照元チェックは不要、構造的に安全）。
///
/// AffectedIdsはDelete系の通常パターン通り、呼び出し元がコンストラクタで確定値を渡す
/// （Create系と異なりExecute前から削除対象のIdが判明しているため、
/// ComputeAffectedIdsAfterApplyのオーバーライドは不要）。
/// </summary>
public sealed class DeleteFloorUnitObjectCommand<TId, T> : UndoableCommand<List<T>, T>
    where TId : struct, IIntId
    where T : class
{
    private readonly T _toDelete;

    public DeleteFloorUnitObjectCommand(
        List<T> target,
        T toDelete,
        IReadOnlySet<ObjectId> affectedIds)
        : base(target, affectedIds)
    {
        _toDelete = toDelete;
    }

    protected override T CaptureSnapshot(List<T> target) => _toDelete;

    protected override void Apply(List<T> target)
    {
        target.Remove(_toDelete);
    }

    protected override void Restore(List<T> target, T snapshot)
    {
        target.Add(snapshot);
    }
}
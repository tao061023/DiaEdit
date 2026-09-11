namespace DiaEditCore.Commands.Stations.FloorUnitObjects;

using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;

/// <summary>
/// FloorUnitObjectBase.Positionのみを変更する、端点オブジェクト共通の「移動」パターン実装。
/// </summary>
/// <remarks>
/// NoneEndpoint／BoundaryPoint／EntryPoint／BufferStop／Switcherはいずれも座標情報が
/// Base.Positionのみであるため、CreateFloorUnitObjectCommand&lt;TId,T&gt;と同じ発想で
/// 型ごとの個別クラスを持たず本コマンドを直接使う。
///
/// <para>
/// 対象オブジェクトを参照する全Railは、RailEndpointRefがId経由で対象を指す設計上、
/// 本コマンドがBase.Positionを書き換えるだけで自動的に新座標へ追従する。RailEndpointRef自体の
/// 張替え（RailEndPointRefChanger）は不要かつ対象外。
/// </para>
///
/// <para>
/// 適用条件（呼び出し元の責務、本コマンドは検証しない）：移動先座標に他の端点オブジェクトの
/// 収束が既に存在する場合は本コマンドではなくRailEndpointConvergenceWorkflow.Reconcile経由の
/// 変換ステップ一式を使うこと。本コマンドは「合流を伴わない単純移動」専用。
/// </para>
/// </remarks>
public sealed class MoveFloorUnitObjectPositionCommand<T> : UndoableCommand<T, Point>
    where T : class
{
    private readonly Func<T, FloorUnitObjectBase> _baseAccessor;
    private readonly Point _newPosition;

    /// <param name="target">移動対象のオブジェクトインスタンス。</param>
    /// <param name="baseAccessor">対象からFloorUnitObjectBaseを取り出すアクセサ（型ごとにBaseプロパティを渡す）。</param>
    /// <param name="newPosition">移動後の座標。</param>
    /// <param name="affectedIds">呼び出し元が事前に確定させたAffectedIds。</param>
    public MoveFloorUnitObjectPositionCommand(
        T target,
        Func<T, FloorUnitObjectBase> baseAccessor,
        Point newPosition,
        IReadOnlySet<ObjectId> affectedIds)
        : base(target, affectedIds)
    {
        _baseAccessor = baseAccessor;
        _newPosition = newPosition;
    }

    /// <param name="target">移動対象のオブジェクトインスタンス。</param>
    protected override Point CaptureSnapshot(T target) => _baseAccessor(target).Position;

    /// <param name="target">移動対象のオブジェクトインスタンス。</param>
    protected override void Apply(T target) => _baseAccessor(target).Position = _newPosition;

    /// <param name="target">移動対象のオブジェクトインスタンス。</param>
    /// <param name="snapshot">移動前の座標。</param>
    protected override void Restore(T target, Point snapshot) => _baseAccessor(target).Position = snapshot;
}
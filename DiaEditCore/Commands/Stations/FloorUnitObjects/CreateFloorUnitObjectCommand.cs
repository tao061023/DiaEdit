namespace DiaEditCore.Commands.Stations.FloorUnitObjects;

using DiaEditCore.Model;
using DiaEditCore.Session;

/// <summary>
/// FloorUnitObjectBase＋Idのみで構成される「新規登録（Create）」パターンの汎用実装。
/// BoundaryPoint／BufferStop／NoneEndpointはこの形に完全一致するため個別クラスを持たず本コマンドを直接使う。
/// EntryPointのみType追加フィールドを持つため、factoryのクロージャでTypeを固定して吸収する
/// （§9.2項目10横展開の一環、コード重複解消のため個別クラス3種を統合）。
/// </summary>
public sealed class CreateFloorUnitObjectCommand<TId, T> : UndoableCommand<List<T>, T?>
    where TId : struct, IIntId
    where T : class
{
    private readonly IdAllocator<TId> _idAllocator;
    private readonly Func<TId, T> _factory;
    private readonly Func<T, ObjectId> _toObjectId;

    /// <summary>Execute()実行後、生成されたオブジェクトを呼び出し元が参照するためのプロパティ。</summary>
    public T? Created { get; private set; }

    public CreateFloorUnitObjectCommand(
        List<T> target,
        IdAllocator<TId> idAllocator,
        Func<TId, T> factory,
        Func<T, ObjectId> toObjectId)
        : base(target, new HashSet<ObjectId>()) // 新規登録：AffectedIdsは空集合
    {
        _idAllocator = idAllocator;
        _factory = factory;
        _toObjectId = toObjectId;
    }

    protected override IReadOnlySet<ObjectId> ComputeAffectedIdsAfterApply(List<T> target) =>
        Created is not null
            ? new HashSet<ObjectId> { _toObjectId(Created) }
            : new HashSet<ObjectId>();

    protected override T? CaptureSnapshot(List<T> target) => null;

    protected override void Apply(List<T> target)
    {
        if (Created is not null)
        {
            // Redo経路：初回Execute時に生成・保持したインスタンスを再挿入する（§9.1項目23）。
            target.Add(Created);
            return;
        }

        Created = _factory(_idAllocator.AllocateNext());
        target.Add(Created);
    }

    protected override void Restore(List<T> target, T? snapshot)
    {
        if (Created is not null)
        {
            target.Remove(Created);
        }
    }
}
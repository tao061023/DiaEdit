namespace DiaEditCore.Session;

using DiaEditCore.Model;

/// <summary>
/// モデル種別ごとの単調カウンタ方式Id採番器。
/// Undo・削除の有無に関わらず、一度発行したIdは同一セッション内で二度と発行しない。
/// </summary>
/// <remarks>
/// ProjectSession.Load()時にモデル種別ごとに1つ生成し、既存の最大Id+1から開始する
/// </remarks>
public sealed class IdAllocator<TId> where TId : IIntId
{
    private readonly Func<int, TId> _factory;
    private int _next;

    public IdAllocator(Func<int, TId> factory, IEnumerable<int> existingIds)
    {
        _factory = factory;
        var list = existingIds as ICollection<int> ?? existingIds.ToList();
        _next = list.Count == 0 ? 1 : list.Max() + 1;
    }

    public TId AllocateNext() => _factory(_next++);
}
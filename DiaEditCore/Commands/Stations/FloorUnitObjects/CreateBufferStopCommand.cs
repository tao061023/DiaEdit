namespace DiaEditCore.Commands.Stations.FloorUnitObjects;

using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;
using DiaEditCore.Session;

/// <summary>
/// 「新規登録（Create）」パターンのBufferStop向け実装。CreateBoundaryPointCommandと同じ設計。
/// </summary>
public sealed class CreateBufferStopCommand : UndoableCommand<List<BufferStop>, BufferStop?>
{
    private readonly IdAllocator<BufferStopId> _idAllocator;
    private readonly FloorUnitId _floorUnitId;
    private readonly Point _position;
    private readonly string _name;

    /// <summary>Execute()実行後、生成されたBufferStopを呼び出し元が参照するためのプロパティ。</summary>
    public BufferStop? Created { get; private set; }

    public CreateBufferStopCommand(
        List<BufferStop> bufferStops,
        IdAllocator<BufferStopId> idAllocator,
        FloorUnitId floorUnitId,
        Point position,
        string name = "")
        : base(bufferStops, new HashSet<ObjectId>()) // 新規登録：AffectedIdsは空集合
    {
        _idAllocator = idAllocator;
        _floorUnitId = floorUnitId;
        _position = position;
        _name = name;
    }

    protected override IReadOnlySet<ObjectId> ComputeAffectedIdsAfterApply(List<BufferStop> target) =>
        Created is not null
            ? new HashSet<ObjectId> { new BufferStopObjectId(Created.Id) }
            : new HashSet<ObjectId>();

    protected override BufferStop? CaptureSnapshot(List<BufferStop> target) => null;

    protected override void Apply(List<BufferStop> target)
    {
        if (Created is not null)
        {
            // Redo経路：初回Execute時に生成・保持したインスタンスを再挿入する（§9.1項目23）。
            target.Add(Created);
            return;
        }

        Created = new BufferStop
        {
            Id = _idAllocator.AllocateNext(),
            Base = new FloorUnitObjectBase { FloorUnitId = _floorUnitId, Position = _position },
            Name = _name,
        };
        target.Add(Created);
    }

    protected override void Restore(List<BufferStop> target, BufferStop? snapshot)
    {
        if (Created is not null)
        {
            target.Remove(Created);
        }
    }
}
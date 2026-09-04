namespace DiaEditCore.Commands.Stations.FloorUnitObjects;

using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;
using DiaEditCore.Session;

/// <summary>
/// 「新規登録（Create）」パターンのBoundaryPoint向け実装。CreateRailCommandと同じ設計
/// （Redo時のインスタンス再利用、ComputeAffectedIdsAfterApplyによるAffectedIds凍結）。
///
/// §7.1確定仕様「駅構内オブジェクト新規配置」：BoundaryPoint単独の配置操作はUI上存在せず、
/// Rail端点クリック→属性タブでの端点種別選択、またはRail交差の自動検出時にのみ生成される。
/// 本コマンドはその生成契機（RailCreationWorkflow等）から呼ばれる想定であり、
/// 単独でのUI導線（ナビゲーションツリー上の「BoundaryPoint一覧」等）は持たない。
/// </summary>
public sealed class CreateBoundaryPointCommand : UndoableCommand<List<BoundaryPoint>, BoundaryPoint?>
{
    private readonly IdAllocator<BoundaryPointId> _idAllocator;
    private readonly FloorUnitId _floorUnitId;
    private readonly Point _position;
    private readonly string _name;

    /// <summary>Execute()実行後、生成されたBoundaryPointを呼び出し元が参照するためのプロパティ。</summary>
    public BoundaryPoint? Created { get; private set; }

    public CreateBoundaryPointCommand(
        List<BoundaryPoint> boundaryPoints,
        IdAllocator<BoundaryPointId> idAllocator,
        FloorUnitId floorUnitId,
        Point position,
        string name = "")
        : base(boundaryPoints, new HashSet<ObjectId>()) // 新規登録：AffectedIdsは空集合
    {
        _idAllocator = idAllocator;
        _floorUnitId = floorUnitId;
        _position = position;
        _name = name;
    }

    protected override IReadOnlySet<ObjectId> ComputeAffectedIdsAfterApply(List<BoundaryPoint> target) =>
        Created is not null
            ? new HashSet<ObjectId> { new BoundaryPointObjectId(Created.Id) }
            : new HashSet<ObjectId>();

    protected override BoundaryPoint? CaptureSnapshot(List<BoundaryPoint> target) => null;

    protected override void Apply(List<BoundaryPoint> target)
    {
        if (Created is not null)
        {
            // Redo経路：初回Execute時に生成・保持したインスタンスを再挿入する（§9.1項目23）。
            target.Add(Created);
            return;
        }

        Created = new BoundaryPoint
        {
            Id = _idAllocator.AllocateNext(),
            Base = new FloorUnitObjectBase { FloorUnitId = _floorUnitId, Position = _position },
            Name = _name,
        };
        target.Add(Created);
    }

    protected override void Restore(List<BoundaryPoint> target, BoundaryPoint? snapshot)
    {
        if (Created is not null)
        {
            target.Remove(Created);
        }
    }
}
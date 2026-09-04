namespace DiaEditCore.Commands.Stations.FloorUnitObjects;

using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;
using DiaEditCore.Session;

/// <summary>
/// 「新規登録（Create）」パターンのEntryPoint向け実装。CreateBoundaryPointCommandと同じ設計。
///
/// EntryPointType（Arrival/Departure/Both）は新規作成時に確定させる（作成後のType変更ポリシーは
/// §4.4.4「削除＋新規作成」のまま。DependencyResolverによるdirty通知だけでは、Type変更後も
/// 既存のStationPath.Waypoints／StationConnectionSegment構成がType前提のまま意味的に無効化されて
/// いないかまでは検証しないため、見直しの要否は別途§9.2項目候補として検討する。本コマンドの
/// スコープには影響しない）。
/// </summary>
public sealed class CreateEntryPointCommand : UndoableCommand<List<EntryPoint>, EntryPoint?>
{
    private readonly IdAllocator<EntryPointId> _idAllocator;
    private readonly FloorUnitId _floorUnitId;
    private readonly Point _position;
    private readonly string _name;
    private readonly EntryPointType _type;

    /// <summary>Execute()実行後、生成されたEntryPointを呼び出し元が参照するためのプロパティ。</summary>
    public EntryPoint? Created { get; private set; }

    public CreateEntryPointCommand(
        List<EntryPoint> entryPoints,
        IdAllocator<EntryPointId> idAllocator,
        FloorUnitId floorUnitId,
        Point position,
        EntryPointType type,
        string name = "")
        : base(entryPoints, new HashSet<ObjectId>()) // 新規登録：AffectedIdsは空集合
    {
        _idAllocator = idAllocator;
        _floorUnitId = floorUnitId;
        _position = position;
        _type = type;
        _name = name;
    }

    protected override IReadOnlySet<ObjectId> ComputeAffectedIdsAfterApply(List<EntryPoint> target) =>
        Created is not null
            ? new HashSet<ObjectId> { new EntryPointObjectId(Created.Id) }
            : new HashSet<ObjectId>();

    protected override EntryPoint? CaptureSnapshot(List<EntryPoint> target) => null;

    protected override void Apply(List<EntryPoint> target)
    {
        if (Created is not null)
        {
            // Redo経路：初回Execute時に生成・保持したインスタンスを再挿入する（§9.1項目23）。
            target.Add(Created);
            return;
        }

        Created = new EntryPoint
        {
            Id = _idAllocator.AllocateNext(),
            Base = new FloorUnitObjectBase { FloorUnitId = _floorUnitId, Position = _position },
            Name = _name,
            Type = _type,
        };
        target.Add(Created);
    }

    protected override void Restore(List<EntryPoint> target, EntryPoint? snapshot)
    {
        if (Created is not null)
        {
            target.Remove(Created);
        }
    }
}
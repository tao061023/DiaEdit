namespace DiaEditCore.Commands.Stations;

using DiaEditCore.Model;
using DiaEditCore.Model.Stations;

/// <summary>
/// 6.1節「新規登録（Create）」パターンのFloorUnit向け実装。CreateStationCommand（v12.11）と同型。
///
/// AffectedIdsについて：ObjectId.csにFloorUnitObjectIdが未定義のため（v12.14時点でDependencyResolver
/// のグラフにFloorUnitは組み込まれていない）、DependencyResolver.ResolveAffectedは使わず空集合を渡す。
/// 将来FloorUnitObjectIdを追加しグラフに組み込む場合は、本コマンドとDeleteFloorUnitCommand（未実装）
/// の両方でAffectedIds算出方法を見直すこと（§9.2参照）。
///
/// 単独では通常呼び出さない想定：Station作成時はStationCreationWorkflow.CreateStationWithDefaultFloorUnit
/// （TransactionCommand経由）が本コマンドを内包する形で使う（4.2節のn≥1制約：Stationは1件以上の
/// FloorUnitを持つ、を保存時検証違反にしないため）。
/// </summary>
public sealed class CreateFloorUnitCommand : UndoableCommand<List<FloorUnit>, FloorUnit?>
{
    private readonly StationId _stationId;
    private readonly string _name;
    private readonly int _displayOrder;

    /// <summary>Execute()実行後、生成されたFloorUnitを呼び出し元が参照するためのプロパティ。</summary>
    public FloorUnit? Created { get; private set; }

    public CreateFloorUnitCommand(
        List<FloorUnit> floorUnits,
        StationId stationId,
        string name = "",
        int displayOrder = 0)
        : base(floorUnits, new HashSet<ObjectId>()) // 新規登録：AffectedIdsは空集合（6.1節）
    {
        _stationId = stationId;
        _name = name;
        _displayOrder = displayOrder;
    }

    protected override FloorUnit? CaptureSnapshot(List<FloorUnit> target) => null;

    protected override void Apply(List<FloorUnit> target)
    {
        var id = AllocateNextId(target);
        Created = new FloorUnit
        {
            Id = id,
            StationId = _stationId,
            Name = _name,
            DisplayOrder = _displayOrder
        };
        target.Add(Created);
    }

    protected override void Restore(List<FloorUnit> target, FloorUnit? snapshot)
    {
        if (Created is not null)
        {
            target.Remove(Created);
        }
    }

    private static FloorUnitId AllocateNextId(IReadOnlyList<FloorUnit> existing) =>
        new(existing.Count == 0 ? 1 : existing.Max(f => f.Id.Value) + 1);
}
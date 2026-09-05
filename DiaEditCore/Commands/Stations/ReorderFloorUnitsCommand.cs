namespace DiaEditCore.Commands.Stations;

using DiaEditCore.Algorithm.Dependency;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Session;

/// <summary>
/// 「並べ替え」パターンの新規実装（§9.2項目24の解消）。
///
/// DisplayOrderを生の整数値として個別編集させず、「同一Station配下FloorUnit全件の新しい並び順」
/// という単位でのみ変更を受け付ける。これにより重複したDisplayOrderを経由する中間状態が
/// 構造的に発生し得ない（4.0節：構造的予防 over ランタイム検証）。
///
/// AffectedIdsについて：対象はStation配下の複数FloorUnitのため、それぞれのFloorUnitObjectIdを
/// changedIdsとしてDependencyResolver.ResolveAffectedへ渡し、和集合を取る
/// （ChangeStationAttributesCommand等の単一対象パターンとは異なる点に注意）。
///
/// TargetはList&lt;FloorUnit&gt;全体（該当Stationに限らずプロジェクト全FloorUnitのリスト）とする。
/// Apply/Restoreは渡されたnewOrder（またはスナップショット）に含まれるFloorUnitId群のみを
/// target内から検索して書き換えるため、リスト自体の要素追加・削除は行わない
/// （CreateFloorUnitCommand等のCreate/Deleteパターンと異なり、このコマンドは既存要素の
/// 属性書き換えのみを行う「属性変更」パターンの一種と位置づけられる）。
/// </summary>
public sealed class ReorderFloorUnitsCommand : UndoableCommand<List<FloorUnit>, IReadOnlyList<(FloorUnitId Id, int DisplayOrder)>>
{
    private readonly StationId _stationId;
    private readonly IReadOnlyList<FloorUnitId> _newOrder;

    public ReorderFloorUnitsCommand(
        List<FloorUnit> floorUnits,
        StationId stationId,
        IReadOnlyList<FloorUnitId> newOrder,
        ProjectSession session)
        : base(floorUnits, BuildAffectedIds(stationId, newOrder, session))
    {
        var siblings = floorUnits.Where(f => f.StationId == stationId).Select(f => f.Id).ToHashSet();

        if (newOrder.Count != siblings.Count || !newOrder.All(siblings.Contains))
        {
            throw new InvalidOperationException(
                $"Station（Id={stationId.Value}）配下のFloorUnit集合と、指定された新しい並び順の" +
                $"内容が一致しません（過不足があります）。並べ替えは既存FloorUnit全件を過不足なく" +
                $"含む必要があります。");
        }

        _stationId = stationId;
        _newOrder = newOrder.ToList(); // 防御的コピー
    }

    private static IReadOnlySet<ObjectId> BuildAffectedIds(
        StationId stationId, IReadOnlyList<FloorUnitId> newOrder, ProjectSession session)
    {
        var cache = session.GetCache();
        var changedIds = newOrder
            .Select(id => (ObjectId)new FloorUnitObjectId(id))
            .ToHashSet();
        return DependencyResolver.ResolveAffected(changedIds, cache);
    }

    protected override IReadOnlyList<(FloorUnitId Id, int DisplayOrder)> CaptureSnapshot(List<FloorUnit> target) =>
        target
            .Where(f => f.StationId == _stationId)
            .Select(f => (f.Id, f.DisplayOrder))
            .ToList();

    protected override void Apply(List<FloorUnit> target)
    {
        var byId = target.Where(f => f.StationId == _stationId).ToDictionary(f => f.Id);
        for (var i = 0; i < _newOrder.Count; i++)
        {
            byId[_newOrder[i]].DisplayOrder = i;
        }
    }

    protected override void Restore(List<FloorUnit> target, IReadOnlyList<(FloorUnitId Id, int DisplayOrder)> snapshot)
    {
        var byId = target.Where(f => f.StationId == _stationId).ToDictionary(f => f.Id);
        foreach (var (id, displayOrder) in snapshot)
        {
            byId[id].DisplayOrder = displayOrder;
        }
    }
}
namespace DiaEditCore.Commands.Stations;

using DiaEditCore.Algorithm.Dependency;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Session;

/// <summary>
/// FloorUnit.Nameのスナップショット。
///
/// DisplayOrderはこのコマンドのスコープ外とする（並べ替え操作専用のReorderFloorUnitsCommandへ
/// 責務分離。「同一StationId内で一意」という制約を、単発の属性変更コマンドが個別に
/// 満たそうとするとバリデーションが必要になるが、並べ替えという専用操作の形にすることで
/// 重複したDisplayOrderを持つ中間状態自体を構造的に生成不能にできるため）。
/// </summary>
public sealed record FloorUnitSnapshot(string Name);

/// <summary>
/// 「属性変更」パターンのFloorUnit向け実装。対象フィールドはNameのみ。
/// </summary>
public sealed class ChangeFloorUnitAttributesCommand : UndoableCommand<FloorUnit, FloorUnitSnapshot>
{
    private readonly FloorUnitSnapshot _newValues;

    public ChangeFloorUnitAttributesCommand(FloorUnit target, FloorUnitSnapshot newValues, ProjectSession session)
        : base(target, BuildAffectedIds(target, session))
    {
        _newValues = newValues;
    }

    private static IReadOnlySet<ObjectId> BuildAffectedIds(FloorUnit target, ProjectSession session)
    {
        var cache = session.GetCache();
        return DependencyResolver.ResolveAffected(
            new HashSet<ObjectId> { new FloorUnitObjectId(target.Id) }, cache);
    }

    protected override FloorUnitSnapshot CaptureSnapshot(FloorUnit target) => new(target.Name);

    protected override void Apply(FloorUnit target)
    {
        target.Name = _newValues.Name;
    }

    protected override void Restore(FloorUnit target, FloorUnitSnapshot snapshot)
    {
        target.Name = snapshot.Name;
    }
}
namespace DiaEditCore.Commands.Stations;

using DiaEditCore.Algorithm.Dependency;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Session;

/// <summary>
/// 「削除（Delete）」パターンのFloorUnit向け実装。
///
/// 検査順序：①n≥1制約（Stationカーディナリティ、コマンド固有のドメインルール）→
///          ②直接参照元（DependencyResolver.ResolveDirectDependents、汎用の1ホップ拒否ロジック）。
/// 双方とも「コマンド層とUI層の責務分担」における「ハード制約」に該当するため、
/// いずれか一方でも該当すればコンストラクタで例外を送出しコマンド生成自体を失敗させる。
/// </summary>
public sealed class DeleteFloorUnitCommand : UndoableCommand<List<FloorUnit>, FloorUnit>
{
    private readonly FloorUnit _floorUnitToDelete;

    public DeleteFloorUnitCommand(List<FloorUnit> floorUnits, FloorUnit floorUnitToDelete, ProjectSession session)
        : base(floorUnits, BuildAffectedIds(floorUnitToDelete, session))
    {
        var siblingCount = floorUnits.Count(f => f.StationId == floorUnitToDelete.StationId);
        if (siblingCount <= 1)
        {
            throw new InvalidOperationException(
                $"FloorUnit（Id={floorUnitToDelete.Id.Value}）は、Station（Id={floorUnitToDelete.StationId.Value}）" +
                $"が保持する最後のFloorUnitのため削除できません（n≥1制約）。");
        }

        var cache = session.GetCache();
        var directDependents = DependencyResolver
            .ResolveDirectDependents(new FloorUnitObjectId(floorUnitToDelete.Id), cache)
            .ToList();

        if (directDependents.Count > 0)
        {
            throw new InvalidOperationException(
                $"FloorUnit（Id={floorUnitToDelete.Id.Value}）は{directDependents.Count}件の配下オブジェクト" +
                $"（BoundaryPoint／EntryPoint／BufferStop／Switcher／Platform／StationPath）が" +
                $"残っているため削除できません。先に配下オブジェクトを削除してください。");
        }

        _floorUnitToDelete = floorUnitToDelete;
    }

    private static IReadOnlySet<ObjectId> BuildAffectedIds(FloorUnit floorUnit, ProjectSession session)
    {
        var cache = session.GetCache();
        return DependencyResolver.ResolveAffected(
            new HashSet<ObjectId> { new FloorUnitObjectId(floorUnit.Id) }, cache);
    }

    protected override FloorUnit CaptureSnapshot(List<FloorUnit> target) => _floorUnitToDelete;

    protected override void Apply(List<FloorUnit> target)
    {
        target.Remove(_floorUnitToDelete);
    }

    protected override void Restore(List<FloorUnit> target, FloorUnit snapshot)
    {
        target.Add(snapshot);
    }
}
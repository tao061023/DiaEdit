namespace DiaEditCore.Commands.Stations.FloorUnitObjects;

using DiaEditCore.Algorithm.Stations.FloorUnitObjects;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;
using DiaEditCore.Model.TimeTable;
using DiaEditCore.Model.TimeTable.Trains;
using DiaEditCore.Session;

/// <summary>
/// Rail削除を「削除＋両端点の収束再照合（RailEndpointConvergenceWorkflow.Reconcile）」まで
/// 1つのUndo単位にまとめるワークフロー（RailCreationWorkflowと対称）。
///
/// 既存DeleteRailCommand自体は無改修：Platform/TemporaryRestriction/Train.StopTimeの
/// 3経路参照チェックという既存の責務単体のコマンドとして維持し、本ワークフローは
/// それを1ステップ目として呼び出すだけの薄いラッパーとする。
///
/// 呼び出し順序（TransActionCommand内、Execute順）：
///   1. DeleteRailCommand（既存の3経路チェック＋Rail削除本体）
///   2. EndpointA側の再照合（Rail削除後の状態でFindConvergingEndpointsを再計算してから呼ぶ）
///   3. EndpointB側の再照合（同上）
/// Reconcile呼び出しをFunc&lt;IUndoableCommand&gt;で遅延させているのは、ステップ2・3の
/// FindConvergingEndpointsがステップ1実行後（railToDeleteがrailsから除去された後）の
/// 状態を見る必要があるため（TransActionCommandの各ステップはExecute時に順次評価される）。
///
/// 未対応のまま残る論点（次回セッション確認事項）：
/// RailEndpointConvergenceWorkflow.Reconcile内部のStationPathブロックチェック
/// （FindBlockingStationPaths）は、BoundaryPoint/SwitcherNew/SwitcherExpandケースの
/// AddDeleteStepsForVanishingEndpoints経由でのみ呼ばれており、Vanish・Keepケースの
/// AddDeleteStepForObjectId直接呼び出し経路にはブロックチェックが無い（Reconcile実装時点の
/// 既存の設計、本ワークフロー新設に伴う新規発見）。Vanish/Keepで消滅する端点が
/// StationPath.Waypointsから参照されるケースが実際にあり得るかは未検証。
/// </summary>
public static class RailDeletionWorkflow
{
    public static IUndoableCommand Create(
        List<Rail> rails,
        Rail railToDelete,
        ProjectSession session,
        IReadOnlyList<Platform> allPlatforms,
        IReadOnlyList<TemporaryRestriction> allRestrictions,
        IReadOnlyList<Train> allTrains,
        List<NoneEndpoint> noneEndpoints,
        List<BoundaryPoint> boundaryPoints,
        List<EntryPoint> entryPoints,
        List<BufferStop> bufferStops,
        List<Switcher> switchers,
        List<StationPath> stationPaths)
    {
        // 削除前に両端点の座標・ObjectId・所属FloorUnitIdを記録
        // （削除後はrailToDelete.EndpointA/Bから辿れなくなるため）。
        var posA = RailEndpointConvergenceResolver.ResolvePosition(
            railToDelete.EndpointA, noneEndpoints, boundaryPoints, entryPoints, bufferStops, switchers);
        var posB = RailEndpointConvergenceResolver.ResolvePosition(
            railToDelete.EndpointB, noneEndpoints, boundaryPoints, entryPoints, bufferStops, switchers);
        var oldObjectIdA = railToDelete.EndpointA.ToObjectId();
        var oldObjectIdB = railToDelete.EndpointB.ToObjectId();
        var floorUnitIdA = ResolveFloorUnitId(
            railToDelete.EndpointA, noneEndpoints, boundaryPoints, entryPoints, bufferStops, switchers);
        var floorUnitIdB = ResolveFloorUnitId(
            railToDelete.EndpointB, noneEndpoints, boundaryPoints, entryPoints, bufferStops, switchers);

        // ステップ1：既存DeleteRailCommand（コンストラクタ内で3経路チェック、違反時は即例外）。
        // ここでコンストラクタが呼ばれる時点でチェックが走るため、本Createメソッド自体の
        // 呼び出し時点で例外が飛ぶ（TransActionCommand化する前に判明する、望ましい挙動）。
        var deleteCommand = new DeleteRailCommand(
            rails, railToDelete, session, allPlatforms, allRestrictions, allTrains);

        var commands = new List<Func<IUndoableCommand>> { () => deleteCommand };

        // ステップ2：EndpointA側の再照合（削除後のrailsを見て計算するため遅延評価）。
        commands.Add(() =>
        {
            var converging = RailEndpointConvergenceResolver.FindConvergingEndpoints(
                posA, rails, noneEndpoints, boundaryPoints, entryPoints, bufferStops, switchers);
            var reconcile = RailEndpointConvergenceWorkflow.Reconcile(
                posA, floorUnitIdA, converging, oldObjectIdA, session,
                rails, noneEndpoints, session.NoneEndpointIds,
                boundaryPoints, session.BoundaryPointIds,
                entryPoints, bufferStops,
                switchers, session.SwitcherIds,
                stationPaths);
            return reconcile ?? NoOpCommand.Instance;
        });

        // ステップ3：EndpointB側の再照合。
        // 自己ループ（A/Bが元々同一座標）の場合、ステップ2実行後は既にposBの収束状態も
        // 変化しているため、ステップ3のFindConvergingEndpointsはステップ2適用後の状態を見る
        // （Reconcile内のIsAlreadyReconciledにより、ステップ2で既に処理済みなら
        // ステップ3はnullを返しNoOpになる＝冪等）。
        commands.Add(() =>
        {
            var converging = RailEndpointConvergenceResolver.FindConvergingEndpoints(
                posB, rails, noneEndpoints, boundaryPoints, entryPoints, bufferStops, switchers);
            var reconcile = RailEndpointConvergenceWorkflow.Reconcile(
                posB, floorUnitIdB, converging, oldObjectIdB, session,
                rails, noneEndpoints, session.NoneEndpointIds,
                boundaryPoints, session.BoundaryPointIds,
                entryPoints, bufferStops,
                switchers, session.SwitcherIds,
                stationPaths);
            return reconcile ?? NoOpCommand.Instance;
        });

        return new TransActionCommand(commands);
    }

    private static FloorUnitId ResolveFloorUnitId(
        RailEndpointRef endpointRef,
        IReadOnlyList<NoneEndpoint> noneEndpoints,
        IReadOnlyList<BoundaryPoint> boundaryPoints,
        IReadOnlyList<EntryPoint> entryPoints,
        IReadOnlyList<BufferStop> bufferStops,
        IReadOnlyList<Switcher> switchers)
    {
        return endpointRef switch
        {
            NoneEndpointRef n => noneEndpoints.First(x => x.Id.Equals(n.Id)).Base.FloorUnitId,
            BoundaryPointEndpointRef b => boundaryPoints.First(x => x.Id.Equals(b.Id)).Base.FloorUnitId,
            EntryPointEndpointRef e => entryPoints.First(x => x.Id.Equals(e.Id)).Base.FloorUnitId,
            BufferStopEndpointRef bs => bufferStops.First(x => x.Id.Equals(bs.Id)).Base.FloorUnitId,
            SwitcherEndpointRef sw => switchers.First(x => x.Id.Equals(sw.Id)).Base.FloorUnitId,
            _ => throw new NotSupportedException($"未知のRailEndpointRef型: {endpointRef.GetType().Name}"),
        };
    }

    /// <summary>
    /// Reconcileがnull（既に整合済み・何もしない）を返した際のプレースホルダー。
    /// IUndoableCommand.Execute()/Undo()はIReadOnlySet&lt;ObjectId&gt;を返す設計のため、
    /// 何もしない場合は空集合を返す（呼び出し側のChangeNotificationBridge等が
    /// 空集合を渡された場合に何も通知しないのは、他の変更なしコマンドと同じ挙動のはず）。
    /// </summary>
    private sealed class NoOpCommand : IUndoableCommand
    {
        public static readonly NoOpCommand Instance = new();
        private static readonly IReadOnlySet<ObjectId> Empty = new HashSet<ObjectId>();
        private NoOpCommand() { }

        public IReadOnlySet<ObjectId> Execute() => Empty;
        public IReadOnlySet<ObjectId> Undo() => Empty;
    }
}
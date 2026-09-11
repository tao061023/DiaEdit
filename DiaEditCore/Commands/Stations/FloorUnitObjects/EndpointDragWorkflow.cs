namespace DiaEditCore.Commands.Stations.FloorUnitObjects;

using DiaEditCore.Algorithm.Dependency;
using DiaEditCore.Algorithm.Stations.FloorUnitObjects;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;
using DiaEditCore.Session;

/// <summary>
/// 端点ドラッグ（基本フロー：移動対象を参照する全Railが追従する方式）の入口ワークフロー。
/// </summary>
/// <remarks>
/// 「端点選択→Rail個別選択→部分デタッチ移動」（選択的デタッチフロー）は対象外（今回スコープ外、
/// Tao様確認済み）。共有端点をドラッグした場合、それを参照する全Railが常に追従する。
/// </remarks>
public static class EndpointDragWorkflow
{
    /// <summary>
    /// 端点オブジェクトのドラッグ移動を解決し、発行すべきコマンドを返す。
    /// </summary>
    /// <param name="movedObjectId">ドラッグ対象の端点オブジェクトId。</param>
    /// <param name="oldPosition">ドラッグ開始時の座標。</param>
    /// <param name="newPosition">ドロップ時の座標。</param>
    /// <param name="floorUnitId">対象が属するFloorUnitId（合流時、新規作成する端点オブジェクトに使う）。</param>
    /// <param name="session">AffectedIds算出に使うプロジェクトセッション。</param>
    /// <param name="rails">全Rail。</param>
    /// <param name="noneEndpoints">現在のNoneEndpointコレクション。</param>
    /// <param name="noneEndpointIds">NoneEndpoint用のId採番器。</param>
    /// <param name="boundaryPoints">現在のBoundaryPointコレクション。</param>
    /// <param name="boundaryPointIds">BoundaryPoint用のId採番器。</param>
    /// <param name="entryPoints">現在のEntryPointコレクション。</param>
    /// <param name="bufferStops">現在のBufferStopコレクション。</param>
    /// <param name="switchers">現在のSwitcherコレクション。</param>
    /// <param name="switcherIds">Switcher用のId採番器。</param>
    /// <param name="stationPaths">StationPathブロックチェック対象の全StationPath。</param>
    /// <returns>
    /// 発行すべきコマンド。oldPositionとnewPositionが一致する場合はnull（無操作）。
    /// </returns>
    /// <exception cref="NotSupportedException"><paramref name="movedObjectId"/>が未対応の型の場合。</exception>
    /// <exception cref="InvalidOperationException">
    /// 合流先の収束数が5以上の場合、または合流によって消滅する既存端点がStationPathから
    /// 参照されている場合（いずれもRailEndpointConvergenceWorkflow.Reconcile経由）。
    /// </exception>
    public static IUndoableCommand? ResolveMove(
        ObjectId movedObjectId,
        Point oldPosition,
        Point newPosition,
        FloorUnitId floorUnitId,
        ProjectSession session,
        List<Rail> rails,
        List<NoneEndpoint> noneEndpoints, IdAllocator<NoneEndpointId> noneEndpointIds,
        List<BoundaryPoint> boundaryPoints, IdAllocator<BoundaryPointId> boundaryPointIds,
        List<EntryPoint> entryPoints,
        List<BufferStop> bufferStops,
        List<Switcher> switchers, IdAllocator<SwitcherId> switcherIds,
        List<StationPath> stationPaths)
    {
        if (oldPosition.Equals(newPosition)) return null;

        var convergingAtNew = RailEndpointConvergenceResolver.FindConvergingEndpoints(
            newPosition, rails, noneEndpoints, boundaryPoints, entryPoints, bufferStops, switchers);

        if (convergingAtNew.Count == 0)
        {
            // 単純移動：新座標に既存の収束なし。RailEndpointRefは一切変更しない。
            var cache = session.GetCache();
            var affected = DependencyResolver.ResolveAffected(
                new HashSet<ObjectId> { movedObjectId }, cache);

            return movedObjectId switch
            {
                NoneEndpointObjectId n => new MoveFloorUnitObjectPositionCommand<NoneEndpoint>(
                    noneEndpoints.First(x => x.Id.Equals(n.Id)), x => x.Base, newPosition, affected),
                BoundaryPointObjectId b => new MoveFloorUnitObjectPositionCommand<BoundaryPoint>(
                    boundaryPoints.First(x => x.Id.Equals(b.Id)), x => x.Base, newPosition, affected),
                EntryPointObjectId e => new MoveFloorUnitObjectPositionCommand<EntryPoint>(
                    entryPoints.First(x => x.Id.Equals(e.Id)), x => x.Base, newPosition, affected),
                BufferStopObjectId bs => new MoveFloorUnitObjectPositionCommand<BufferStop>(
                    bufferStops.First(x => x.Id.Equals(bs.Id)), x => x.Base, newPosition, affected),
                SwitcherObjectId sw => new MoveFloorUnitObjectPositionCommand<Switcher>(
                    switchers.First(x => x.Id.Equals(sw.Id)), x => x.Base, newPosition, affected),
                _ => throw new NotSupportedException($"未対応のObjectId型: {movedObjectId.GetType().Name}"),
            };
        }

        // 合流あり：移動対象を参照する全Railを、新座標の収束集合へ加えて再分類する。
        var attachedRails = RailEndpointConvergenceResolver.FindRailsReferencing(movedObjectId, rails);
        var converging = convergingAtNew.Concat(attachedRails).ToList();

        return RailEndpointConvergenceWorkflow.Reconcile(
            newPosition, floorUnitId, converging, oldObjectId: null, session,
            rails, noneEndpoints, noneEndpointIds, boundaryPoints, boundaryPointIds,
            entryPoints, bufferStops, switchers, switcherIds, stationPaths);
    }
}
namespace DiaEditCore.Commands.Stations.FloorUnitObjects;

using DiaEditCore.Algorithm.Stations.FloorUnitObjects;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;
using DiaEditCore.Session;

/// <summary>
/// ある座標における収束状態（<see cref="RailEndpointConvergenceResolver.Classify"/>の結果）と、
/// その座標に現在存在する端点オブジェクトの型が一致しているかを比較し、不一致の場合のみ
/// 「新規作成→<see cref="RailEndPointRefChanger"/>で参照張替え→旧端点削除」の変換ステップを組み立てる。
/// </summary>
/// <remarks>
/// 増加方向（Ctrl+ドラッグでの新規Rail作成・既存端点へのドラッグ接続）・
/// 減少方向（DeleteRailCommandによるRail削除）のいずれから呼ばれても同一ロジックで動作する
/// （Converge/Divergeを別実装にしない）。
///
/// <para>
/// 前提：converging（<see cref="RailEndpointConvergenceResolver.FindConvergingEndpoints"/>の戻り値）は
/// 呼び出し時点で最新の状態を反映していること（DeleteRailCommandから呼ぶ場合は、対象RailをTargetから
/// 除去した後に再計算すること）。
/// </para>
///
/// <para>
/// StationPathブロックチェック（<see cref="RailEndpointConvergenceResolver.FindBlockingStationPaths"/>）は
/// 本ワークフローの責務外：呼び出し元（実際にコマンドをExecuteする側）が事前に呼び、
/// ブロックされていれば本ワークフローを呼ばずに操作自体を中断すること。
/// </para>
/// </remarks>
public static class RailEndpointConvergenceWorkflow
{
    /// <summary>
    /// 現在の収束状態が既に分類結果と整合しているか（＝何もしなくてよいか）を判定する。
    /// </summary>
    /// <param name="kind"><see cref="RailEndpointConvergenceResolver.Classify"/>が返した分類結果。</param>
    /// <param name="converging">現在の収束集合。</param>
    /// <returns>変換不要（既に整合済み）ならtrue。</returns>
    /// <remarks>
    /// Vanishのみ、呼び出し元が「削除前にそこに何らかの端点オブジェクトが存在したか」を
    /// 別途知っている前提のため、ここでは判定しない（常にfalse＝要変換とみなす。
    /// 実際に消すべきオブジェクトが無ければ呼び出し元がスキップすればよい）。
    /// </remarks>
    private static bool IsAlreadyReconciled(ConvergenceKind kind, IReadOnlyList<RailEndpointLocation> converging)
    {
        switch (kind)
        {
            case ConvergenceKind.Vanish:
                return false;

            case ConvergenceKind.Keep:
                // N=1。既にNone/EntryPoint/BufferStopのいずれか（＝BoundaryPoint/Switcherではない）なら整合済み。
                return converging.Count == 1
                    && converging[0].Ref is not BoundaryPointEndpointRef
                    && converging[0].Ref is not SwitcherEndpointRef;

            case ConvergenceKind.BoundaryPoint:
                // N=2。両方が同一BoundaryPointIdを指していれば整合済み。
                return converging.Count == 2
                    && converging[0].Ref is BoundaryPointEndpointRef b0
                    && converging[1].Ref is BoundaryPointEndpointRef b1
                    && b0.Id.Equals(b1.Id);

            case ConvergenceKind.SwitcherExpand:
            case ConvergenceKind.SwitcherNew:
                // N=3,4。全員が同一SwitcherIdを指し、かつPortIndexが重複していなければ整合済み。
                if (converging.Any(c => c.Ref is not SwitcherEndpointRef))
                    return false;
                var refs = converging.Select(c => (SwitcherEndpointRef)c.Ref).ToList();
                var sameSwitcher = refs.Select(r => r.Id).Distinct().Count() == 1;
                var distinctPorts = refs.Select(r => r.PortIndex).Distinct().Count() == refs.Count;
                return sameSwitcher && distinctPorts;

            default:
                return false; // Error等
        }
    }

    /// <summary>
    /// 再照合を実行する。既に整合していればnullを返す（TransActionCommandを組まない＝Undo単位を作らない）。
    /// </summary>
    /// <param name="position">再照合対象の座標。</param>
    /// <param name="floorUnitId">新規作成する端点オブジェクトが属するFloorUnitId。</param>
    /// <param name="converging">現在の収束集合。</param>
    /// <param name="oldObjectId">
    /// この座標に元々存在していた端点オブジェクトのObjectId（Vanish判定・削除対象特定に使用）。
    /// 元々何も存在しなかった座標（真の新規収束）ではnullを渡す。
    /// </param>
    /// <param name="session">
    /// 呼び出し規約上受け取るが、本メソッド内部では未使用（将来の拡張余地として引数のみ保持）。
    /// </param>
    /// <param name="rails">Rail端点張替え対象となる全Rail。</param>
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
    /// 変換が必要な場合は、その変換ステップ一式を束ねた<see cref="TransActionCommand"/>。
    /// 既に整合済み、またはVanishかつ<paramref name="oldObjectId"/>がnull（元々何も存在しなかった）の場合はnull。
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// <see cref="RailEndpointConvergenceResolver.Classify"/>が<see cref="ConvergenceKind.Error"/>を返した場合
    /// （収束数N&gt;=5）。または、収束変換によって消滅する端点が既存StationPathから参照されており
    /// （<see cref="RailEndpointConvergenceResolver.FindBlockingStationPaths"/>で検出）実行できない場合。
    /// </exception>
    public static IUndoableCommand? Reconcile(
        Point position,
        FloorUnitId floorUnitId,
        IReadOnlyList<RailEndpointLocation> converging,
        ObjectId? oldObjectId,
        ProjectSession session,
        List<Rail> rails,
        List<NoneEndpoint> noneEndpoints, IdAllocator<NoneEndpointId> noneEndpointIds,
        List<BoundaryPoint> boundaryPoints, IdAllocator<BoundaryPointId> boundaryPointIds,
        List<EntryPoint> entryPoints,
        List<BufferStop> bufferStops,
        List<Switcher> switchers, IdAllocator<SwitcherId> switcherIds,
        List<StationPath> stationPaths)
    {
        var classification = RailEndpointConvergenceResolver.Classify(converging);

        if (classification.Kind == ConvergenceKind.Error)
            throw new InvalidOperationException(classification.ErrorMessage);

        if (IsAlreadyReconciled(classification.Kind, converging))
            return null;

        // Vanishかつ元々何も存在しなかった（oldObjectId無し）場合も何もしない。
        if (classification.Kind == ConvergenceKind.Vanish && oldObjectId is null)
            return null;

        var targets = converging.Select(c => (c.RailId, c.End)).ToList();
        var affectedRailIds = targets.Select(t => (ObjectId)new RailObjectId(t.RailId)).ToHashSet();

        var commands = new List<Func<IUndoableCommand>>();

        switch (classification.Kind)
        {
            case ConvergenceKind.Vanish:
            {
                // 削除対象オブジェクトを特定して消すだけ（張替え不要：参照するRailがもう無い）。
                AddDeleteStepForObjectId(oldObjectId!, noneEndpoints, entryPoints, bufferStops, boundaryPoints, switchers, commands);
                break;
            }

            case ConvergenceKind.Keep:
            {
                // N=1への縮退（BoundaryPoint/Switcherからの降格）。NoneEndpointへ差し戻す。
                var createNone = new CreateFloorUnitObjectCommand<NoneEndpointId, NoneEndpoint>(
                    noneEndpoints, noneEndpointIds,
                    id => new NoneEndpoint { Id = id, Base = new FloorUnitObjectBase { FloorUnitId = floorUnitId, Position = position }, Name = "" },
                    created => new NoneEndpointObjectId(created.Id));
                commands.Add(() => createNone);

                commands.Add(() => new RailEndPointRefChanger(
                    rails, targets,
                    new NoneEndpointRef(createNone.Created!.Id),
                    affectedRailIds));

                if (oldObjectId is not null)
                    AddDeleteStepForObjectId(oldObjectId, noneEndpoints, entryPoints, bufferStops, boundaryPoints, switchers, commands);
                break;
            }

            case ConvergenceKind.BoundaryPoint:
            {
                var createBoundary = new CreateFloorUnitObjectCommand<BoundaryPointId, BoundaryPoint>(
                    boundaryPoints, boundaryPointIds,
                    id => new BoundaryPoint { Id = id, Base = new FloorUnitObjectBase { FloorUnitId = floorUnitId, Position = position }, Name = "" },
                    created => new BoundaryPointObjectId(created.Id));
                commands.Add(() => createBoundary);

                commands.Add(() => new RailEndPointRefChanger(
                    rails, targets,
                    new BoundaryPointEndpointRef(createBoundary.Created!.Id),
                    affectedRailIds));

                AddDeleteStepsForVanishingEndpoints(converging, noneEndpoints, entryPoints, bufferStops, boundaryPoints, switchers, stationPaths, commands);
                break;
            }

            case ConvergenceKind.SwitcherNew:
            {
                var ports = RailEndpointConvergenceResolver.AssignSwitcherPorts(converging);
                var portCount = ports.Count;

                var createSwitcher = new CreateFloorUnitObjectCommand<SwitcherId, Switcher>(
                    switchers, switcherIds,
                    id => new Switcher { Id = id, Base = new FloorUnitObjectBase { FloorUnitId = floorUnitId, Position = position }, PortCount = portCount },
                    created => new SwitcherObjectId(created.Id));
                commands.Add(() => createSwitcher);

                foreach (var (loc, portIndex) in ports)
                {
                    var single = new[] { (loc.RailId, loc.End) };
                    var singleAffected = new HashSet<ObjectId> { new RailObjectId(loc.RailId) };
                    commands.Add(() => new RailEndPointRefChanger(
                        rails, single,
                        new SwitcherEndpointRef(createSwitcher.Created!.Id, portIndex),
                        singleAffected));
                }

                AddDeleteStepsForVanishingEndpoints(converging, noneEndpoints, entryPoints, bufferStops, boundaryPoints, switchers, stationPaths, commands);
                break;
            }

            case ConvergenceKind.SwitcherExpand:
            {
                // 既存Switcherを特定（収束集合中のSwitcherEndpointRefのいずれかから）。
                var existingSwitcherId = converging
                    .Select(c => c.Ref).OfType<SwitcherEndpointRef>()
                    .First().Id;

                var ports = RailEndpointConvergenceResolver.AssignSwitcherPorts(converging);
                var newPortCount = ports.Count;

                commands.Add(() => new ChangeSwitcherAttributesCommand(
                    switchers, existingSwitcherId,
                    newPortCount,
                    newMechanism: null,
                    newValidRoutes: [],
                    affectedIds: new HashSet<ObjectId> { new SwitcherObjectId(existingSwitcherId) }));

                foreach (var (loc, portIndex) in ports)
                {
                    var single = new[] { (loc.RailId, loc.End) };
                    var singleAffected = new HashSet<ObjectId> { new RailObjectId(loc.RailId) };
                    commands.Add(() => new RailEndPointRefChanger(
                        rails, single,
                        new SwitcherEndpointRef(existingSwitcherId, portIndex),
                        singleAffected));
                }

                var verticesExcludingSurvivingSwitcher = converging
                    .Where(c => c.Ref is not SwitcherEndpointRef sameSwitcher || !sameSwitcher.Id.Equals(existingSwitcherId))
                    .ToList();

                AddDeleteStepsForVanishingEndpoints(
                    verticesExcludingSurvivingSwitcher,
                    noneEndpoints, entryPoints, bufferStops, boundaryPoints, switchers, stationPaths, commands);
                break;
            }
        }

        return new TransActionCommand(commands);
    }

    /// <summary>
    /// 収束変換により消滅する既存端点をStationPathブロックチェックの上で削除ステップとして積む。
    /// </summary>
    /// <param name="converging">
    /// 削除候補の元となる収束集合。呼び出し元は、拡張後も存続するオブジェクト（SwitcherExpandケースの
    /// 拡張対象Switcher自身など）をあらかじめ除外したリストを渡すこと。
    /// </param>
    /// <param name="noneEndpoints">現在のNoneEndpointコレクション。</param>
    /// <param name="entryPoints">現在のEntryPointコレクション。</param>
    /// <param name="bufferStops">現在のBufferStopコレクション。</param>
    /// <param name="boundaryPoints">現在のBoundaryPointコレクション。</param>
    /// <param name="switchers">現在のSwitcherコレクション。</param>
    /// <param name="stationPaths">ブロックチェック対象の全StationPath。</param>
    /// <param name="commands">削除ステップの追加先となるコマンドファクトリ列。</param>
    /// <exception cref="InvalidOperationException">
    /// 消滅対象のいずれかが既存StationPathから参照されている場合
    /// （<see cref="RailEndpointConvergenceResolver.FindBlockingStationPaths"/>で検出）。
    /// </exception>
    private static void AddDeleteStepsForVanishingEndpoints(
        IReadOnlyList<RailEndpointLocation> converging,
        List<NoneEndpoint> noneEndpoints,
        List<EntryPoint> entryPoints,
        List<BufferStop> bufferStops,
        List<BoundaryPoint> boundaryPoints,
        List<Switcher> switchers,
        List<StationPath> stationPaths,
        List<Func<IUndoableCommand>> commands)
    {
        // タスク4：ここで消える既存端点（NoneEndpoint除く）がStationPathから参照されていないか確認。
        var disappearing = converging
            .Select(c => c.Ref.ToObjectId())
            .Where(id => id is not null)
            .Select(id => id!)
            .Distinct()
            .ToList();

        var blocking = RailEndpointConvergenceResolver.FindBlockingStationPaths(disappearing, stationPaths);
        if (blocking.Count > 0)
        {
            throw new InvalidOperationException(
                $"収束によって消滅する端点が{blocking.Count}件のStationPathから参照されているため実行できません：" +
                string.Join(", ", blocking.Select(id => id.Value)));
        }

        foreach (var id in disappearing)
        {
            AddDeleteStepForObjectId(id, noneEndpoints, entryPoints, bufferStops, boundaryPoints, switchers, commands);
        }
    }

    /// <summary>
    /// ObjectIdの実行時型に応じた削除ステップ（<see cref="DeleteFloorUnitObjectCommand{TId,T}"/>）を1件積む。
    /// </summary>
    /// <param name="objectId">削除対象のObjectId。</param>
    /// <param name="noneEndpoints">現在のNoneEndpointコレクション。</param>
    /// <param name="entryPoints">現在のEntryPointコレクション。</param>
    /// <param name="bufferStops">現在のBufferStopコレクション。</param>
    /// <param name="boundaryPoints">現在のBoundaryPointコレクション。</param>
    /// <param name="switchers">現在のSwitcherコレクション。</param>
    /// <param name="commands">削除ステップの追加先となるコマンドファクトリ列。</param>
    /// <exception cref="NotSupportedException"><paramref name="objectId"/>が未対応の型の場合。</exception>
    /// <exception cref="InvalidOperationException">対応するコレクション内に該当オブジェクトが見つからない場合。</exception>
    private static void AddDeleteStepForObjectId(
        ObjectId objectId,
        List<NoneEndpoint> noneEndpoints,
        List<EntryPoint> entryPoints,
        List<BufferStop> bufferStops,
        List<BoundaryPoint> boundaryPoints,
        List<Switcher> switchers,
        List<Func<IUndoableCommand>> commands)
    {
        switch (objectId)
        {
            case NoneEndpointObjectId n:
                var noneObj = noneEndpoints.First(x => x.Id.Equals(n.Id));
                commands.Add(() => new DeleteFloorUnitObjectCommand<NoneEndpointId, NoneEndpoint>(
                    noneEndpoints, noneObj, new HashSet<ObjectId> { n }));
                break;
            case EntryPointObjectId e:
                var epObj = entryPoints.First(x => x.Id.Equals(e.Id));
                commands.Add(() => new DeleteFloorUnitObjectCommand<EntryPointId, EntryPoint>(
                    entryPoints, epObj, new HashSet<ObjectId> { e }));
                break;
            case BufferStopObjectId bs:
                var bsObj = bufferStops.First(x => x.Id.Equals(bs.Id));
                commands.Add(() => new DeleteFloorUnitObjectCommand<BufferStopId, BufferStop>(
                    bufferStops, bsObj, new HashSet<ObjectId> { bs }));
                break;
            case BoundaryPointObjectId b:
                var bpObj = boundaryPoints.First(x => x.Id.Equals(b.Id));
                commands.Add(() => new DeleteFloorUnitObjectCommand<BoundaryPointId, BoundaryPoint>(
                    boundaryPoints, bpObj, new HashSet<ObjectId> { b }));
                break;
            case SwitcherObjectId sw:
                var swObj = switchers.First(x => x.Id.Equals(sw.Id));
                commands.Add(() => new DeleteFloorUnitObjectCommand<SwitcherId, Switcher>(
                    switchers, swObj, new HashSet<ObjectId> { sw }));
                break;
            default:
                throw new NotSupportedException($"未対応のObjectId型: {objectId.GetType().Name}");
        }
    }
}
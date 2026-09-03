namespace DiaEditCore.Model;

using DiaEditCore.Algorithm.CacheBuilder;
using DiaEditCore.Model.Stations.FloorUnitObjects;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.TimeTable;
using DiaEditCore.Model.TimeTable.Trains;

public sealed class TimeTableSetCache
{
    // -----------------------------
    // (a-1)/(a-2) 軽量インデックス系
    // -----------------------------

    public Dictionary<string, TrainId> TrainNumberIndex { get; } = new();
    public Dictionary<EntryPointId, List<StationConnectionId>> EntryPointConnectionIndex { get; } = new();
    public Dictionary<StationId, List<StationConnectionId>> StationConnectionIndex { get; } = new();
    public Dictionary<MainRouteId, List<StationConnectionId>> MainRouteConnectionIndex { get; } = new();
    public Dictionary<StationConnectionSegmentId, List<StationConnectionId>> ScsUsedByIndex { get; } = new();
    public Dictionary<StationConnectionSegmentId, List<TemporaryRestrictionId>> TemporaryRestrictionBySegmentIndex { get; } = new();
    public Dictionary<TrainId, List<TrainId>> DerivedTrainsBySourceId { get; } = new();

    // (TrainId, StopKey) → その停車を外部から参照しているTrainの一覧
    // （SplitOriginRef.OriginStopKey／CouplingWork.PartnerStopKey経由）。
    // 構築はStopKeyReferenceIndexBuilder.Build()側の責務とする（DepartureByStationTrackIndex等と同じ責務分離）。
    // 用途：①RunSegments編集コマンドのAffectedIds算出、②Cross Validatorの実在性検証対象の絞り込み。
    // DependencyResolverのObjectIdグラフとは別枠（StopKeyはRunSegments編集で値が変わりうる不安定キーのため）。
    public Dictionary<(TrainId TrainId, StopKey StopKey), List<StopKeyReferrer>> StopKeyReferenceIndex { get; } = new();

    // 駅×番線をキーに、発車時刻昇順のTrainを引けるようにするインデックス（6.4節TrainConnectionResolverが使用）。
    // 構築はTrainConnectionResolver.BuildDepartureIndex()側の責務とする（TrainOperationIndex等と同じ責務分離）。
    public Dictionary<(StationId StationId, RailId RailId), List<(int DepartureSeconds, TrainId TrainId)>> DepartureByStationTrackIndex { get; } = new();

    // MainRouteId → それを経由するServiceRouteの一覧。UI表示専用（MainRouteのStationOrder変更時、
    // 影響を受けるServiceRouteの一覧表示に使う）。DependencyResolverのAffectedIds算出には使わない。
    // 構築はServiceRouteStationOrderResolver.BuildServiceRoutesByMainRouteIndex()側の責務とする。
    public Dictionary<MainRouteId, List<ServiceRouteId>> ServiceRoutesByMainRouteIndex { get; } = new();

    // FloorUnitId → そのFloorUnit配下に属するオブジェクト（BoundaryPoint／EntryPoint／BufferStop／
    // Switcher／Platform／StationPath）のObjectId一覧（v12.16新設）。
    // 前5者はFloorUnitObjectBase.FloorUnitId経由、StationPathのみFloorUnitIdを直接保持するが、
    // いずれも「FloorUnit削除時に直接参照元として存在チェックすべき対象」という点で同列に扱う。
    // 構築はFloorUnitDependentIndexBuilder.Build()側の責務とする。
    // 用途：DependencyResolver.ResolveDirectDependents（FloorUnitObjectIdケース）、
    // DeleteFloorUnitCommandの削除可否判定（6.1節ハード制約）。
    public Dictionary<FloorUnitId, List<ObjectId>> FloorUnitDependentIndex { get; } = new();

    // StationId → それをStationOrderに含むMainRouteの一覧（§9.1項目6新設）。
    // StationConnectionIndex（StationConnection経由の間接参照）では捉えられない、
    // MainRoute.StationOrderからの直接参照をDeleteStationCommandが検知できるようにする。
    // 構築はStationUsedByMainRouteIndexBuilder.Build()側の責務とする。
    public Dictionary<StationId, List<MainRouteId>> StationUsedByMainRouteIndex { get; } = new();

    // StationId → それをFrom/ToStationIdに持つStationConnectionSegmentの一覧（§9.1項目6新設）。
    // どのStationConnectionにも属さない孤立したSegmentからのStation直接参照を捕捉するために新設。
    // 構築はStationUsedBySegmentIndexBuilder.Build()側の責務とする。
    public Dictionary<StationId, List<StationConnectionSegmentId>> StationUsedBySegmentIndex { get; } = new();
    // EntryPointId → それをFrom/ToEntryPointIdに持つStationConnectionSegmentの一覧（グラフ完成セッションで新設）。
    // EntryPointConnectionIndex（StationConnection経由の間接参照）では捉えられない、
    // StationConnectionSegmentからの直接参照をDependencyResolverが検知できるようにする。
    // 構築はEntryPointUsedBySegmentIndexBuilder.Build()側の責務とする。
    public Dictionary<EntryPointId, List<StationConnectionSegmentId>> EntryPointUsedBySegmentIndex { get; } = new();

    // MainRouteId → それをMainRouteIdに持つStationConnectionSegmentの一覧（グラフ完成セッションで新設）。
    // MainRouteConnectionIndex（StationConnection経由の間接参照）では捉えられない、
    // StationConnectionSegmentからの直接参照をDependencyResolverが検知できるようにする。
    // 構築はMainRouteUsedBySegmentIndexBuilder.Build()側の責務とする。
    public Dictionary<MainRouteId, List<StationConnectionSegmentId>> MainRouteUsedBySegmentIndex { get; } = new();

    public Dictionary<StationConnectionId, List<ServiceRouteId>> StationConnectionUsedByServiceRouteIndex { get; } = new();

    // -----------------------------
    // (b) 重量キャッシュ系（遅延再構築）
    // -----------------------------
    public static ObjectId ToObjectId(StationPathWaypoint wp) =>
        wp switch
        {
            BoundaryPointWaypoint x => new BoundaryPointObjectId(x.Id),
            EntryPointWaypoint    x => new EntryPointObjectId(x.Id),
            BufferStopWaypoint    x => new BufferStopObjectId(x.Id),
            SwitcherWaypoint      x => new SwitcherObjectId(x.Id),
            _ => throw new InvalidOperationException("Unknown waypoint type")
        };

    public Dictionary<ObjectId, List<StationPathId>> ConflictObjectGroupingCache { get; } = new();
    private readonly HashSet<ObjectId> _conflictDirty = new();

    // -----------------------------
    // invalidate / rebuild
    // -----------------------------

    public void InvalidateConflictCache(ObjectId id)
    {
        _conflictDirty.Add(id);
    }

    public IReadOnlyList<StationPathId> GetConflictGroup(ObjectId id, Func<ObjectId, List<StationPathId>> rebuildFunc)
    {
        if (_conflictDirty.Contains(id))
        {
            ConflictObjectGroupingCache[id] = rebuildFunc(id);
            _conflictDirty.Remove(id);
        }

        return ConflictObjectGroupingCache.TryGetValue(id, out var list)
            ? list
            : Array.Empty<StationPathId>();
    }

    // -----------------------------
    // フルリビルド（ファイルロード時）
    // -----------------------------

    public void RebuildAll(
        IEnumerable<Train> trains,
        IEnumerable<StationConnection> stationConnections,
        IEnumerable<StationConnectionSegment> segments,
        IEnumerable<TemporaryRestriction> restrictions,
        IEnumerable<MainRoute> mainRoutes,
        IEnumerable<ServiceRoute> serviceRoutes)
    {
        TrainNumberIndex.Clear();
        EntryPointConnectionIndex.Clear();
        StationConnectionIndex.Clear();
        MainRouteConnectionIndex.Clear();
        ScsUsedByIndex.Clear();
        TemporaryRestrictionBySegmentIndex.Clear();
        DerivedTrainsBySourceId.Clear();
        DepartureByStationTrackIndex.Clear();
        ServiceRoutesByMainRouteIndex.Clear();
        StopKeyReferenceIndex.Clear();
        FloorUnitDependentIndex.Clear();
        StationUsedByMainRouteIndex.Clear();
        StationUsedBySegmentIndex.Clear();
        ConflictObjectGroupingCache.Clear();
        _conflictDirty.Clear();
        EntryPointUsedBySegmentIndex.Clear();
        MainRouteUsedBySegmentIndex.Clear();
        StationConnectionUsedByServiceRouteIndex.Clear();

        // TrainNumberIndex
        foreach (var train in trains)
        {
            if (!string.IsNullOrEmpty(train.TrainNumber))
                TrainNumberIndex[train.TrainNumber] = train.Id;
        }


        var stationConnectionsList = stationConnections as IReadOnlyList<StationConnection> ?? stationConnections.ToList();
        var segmentsList = segments as IReadOnlyList<StationConnectionSegment> ?? segments.ToList();

        foreach (var (mainRouteId, list) in
            MainRouteConnectionIndexBuilder.Build(stationConnectionsList))
        {
            MainRouteConnectionIndex[mainRouteId] = list;
        }

        foreach (var (segId, list) in
            ScsUsedByIndexBuilder.Build(stationConnectionsList))
        {
            ScsUsedByIndex[segId] = list;
        }

        var mainRoutesList = mainRoutes as IReadOnlyList<MainRoute> ?? mainRoutes.ToList();
        var (stationIdx, entryPointIdx) =
            StationAndEntryPointConnectionIndexBuilder.Build(
                stationConnectionsList, segmentsList, mainRoutesList);

        foreach (var (stationId, list) in stationIdx) StationConnectionIndex[stationId] = list;

        foreach (var (entryPointId, list) in entryPointIdx) EntryPointConnectionIndex[entryPointId] = list;

        foreach (var (trainId, list) in
            DerivedTrainsBySourceIdIndexBuilder.Build(trains))
        {
            DerivedTrainsBySourceId[trainId] = list;
        }

        foreach (var (segId, list) in
            TemporaryRestrictionBySegmentIndexBuilder.Build(restrictions))
        {
            TemporaryRestrictionBySegmentIndex[segId] = list;
        }

        // StationUsedByMainRouteIndex／StationUsedBySegmentIndex
        foreach (var (stationId, list) in
            StationUsedByMainRouteIndexBuilder.Build(mainRoutes))
        {
            StationUsedByMainRouteIndex[stationId] = list;
        }

        foreach (var (stationId, list) in
            StationUsedBySegmentIndexBuilder.Build(segmentsList))
        {
            StationUsedBySegmentIndex[stationId] = list;
        }

        foreach (var (entryPointId, list) in
            EntryPointUsedBySegmentIndexBuilder.Build(segmentsList))
        {
            EntryPointUsedBySegmentIndex[entryPointId] = list;
        }

        foreach (var (mainRouteId, list) in
            MainRouteUsedBySegmentIndexBuilder.Build(segmentsList))
        {
            MainRouteUsedBySegmentIndex[mainRouteId] = list;
        }

        foreach (var (scId, list) in
        StationConnectionUsedByServiceRouteIndexBuilder.Build(serviceRoutes))
    {
        StationConnectionUsedByServiceRouteIndex[scId] = list;
    }

        // TrainOperationIndex は TrainOperationChainResolver が構築する（重複プロパティにつき将来削除予定）
        // DepartureByStationTrackIndex は DepartureByStationTrackIndexBuilder.Build() が構築する
        // ServiceRoutesByMainRouteIndex は ServiceRouteStationOrderResolver.BuildServiceRoutesByMainRouteIndex() が構築する
        // StopKeyReferenceIndex は StopKeyReferenceIndexBuilder.Build() が構築する
        // FloorUnitDependentIndex は FloorUnitDependentIndexBuilder.Build() が構築する
        // （上記5つはいずれも既に実装済み。ProjectSession.RebuildCacheIfDirty実装時に、
        // 本メソッドとあわせて呼び出し元で一括配線する方針）
    }
}
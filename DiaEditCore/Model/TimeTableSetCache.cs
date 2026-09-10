namespace DiaEditCore.Model;

using DiaEditCore.Algorithm.CacheBuilder;
using DiaEditCore.Model.Stations.FloorUnitObjects;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.TimeTable;
using DiaEditCore.Model.TimeTable.Trains;

/// <summary>
/// 時刻表セットに関するキャッシュを保持する。
/// </summary>
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
    public Dictionary<(TrainId TrainId, StopKey StopKey), List<StopKeyReferrer>> StopKeyReferenceIndex { get; } = new();
    public Dictionary<(StationId StationId, RailId RailId), List<(int DepartureSeconds, TrainId TrainId)>> DepartureByStationTrackIndex { get; } = new();
    public Dictionary<MainRouteId, List<ServiceRouteId>> ServiceRoutesByMainRouteIndex { get; } = new();
    public Dictionary<FloorUnitId, List<ObjectId>> FloorUnitDependentIndex { get; } = new();
    public Dictionary<StationId, List<MainRouteId>> StationUsedByMainRouteIndex { get; } = new();
    public Dictionary<StationId, List<StationConnectionSegmentId>> StationUsedBySegmentIndex { get; } = new();
    public Dictionary<EntryPointId, List<StationConnectionSegmentId>> EntryPointUsedBySegmentIndex { get; } = new();
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

        // MainRouteConnectionIndex
        foreach (var (mainRouteId, list) in
            MainRouteConnectionIndexBuilder.Build(stationConnectionsList))
        {
            MainRouteConnectionIndex[mainRouteId] = list;
        }

        // ScsUsedByIndex
        foreach (var (segId, list) in
            ScsUsedByIndexBuilder.Build(stationConnectionsList))
        {
            ScsUsedByIndex[segId] = list;
        }

        var mainRoutesList = mainRoutes as IReadOnlyList<MainRoute> ?? mainRoutes.ToList();
        var (stationIdx, entryPointIdx) =
            StationAndEntryPointConnectionIndexBuilder.Build(
                stationConnectionsList, segmentsList, mainRoutesList);

        // StationConnectionIndex
        foreach (var (stationId, list) in stationIdx) StationConnectionIndex[stationId] = list;

        // EntryPointConnectionIndex
        foreach (var (entryPointId, list) in entryPointIdx) EntryPointConnectionIndex[entryPointId] = list;

        foreach (var (trainId, list) in
            DerivedTrainsBySourceIdIndexBuilder.Build(trains))
        {
            DerivedTrainsBySourceId[trainId] = list;
        }

        // DepartureByStationTrackIndex
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
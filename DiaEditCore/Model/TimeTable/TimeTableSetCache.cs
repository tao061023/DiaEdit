namespace DiaEditCore.Model.TimeTable;

using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.TimeTable.Trains;

public sealed class TimeTableSetCache
{
    // -----------------------------
    // (a-1)/(a-2) 軽量インデックス系
    // -----------------------------

    public Dictionary<string, TrainId> TrainNumberIndex { get; } = new();
    public Dictionary<TrainId, TrainOperationId> TrainOperationIndex { get; } = new();
    public Dictionary<EntryPointId, List<StationConnectionId>> EntryPointConnectionIndex { get; } = new();
    public Dictionary<StationId, List<StationConnectionId>> StationConnectionIndex { get; } = new();
    public Dictionary<MainRouteId, List<StationConnectionId>> MainRouteConnectionIndex { get; } = new();
    public Dictionary<StationConnectionSegmentId, List<StationConnectionId>> ScsUsedByIndex { get; } = new();
    public Dictionary<StationConnectionSegmentId, List<TemporaryRestrictionId>> TemporaryRestrictionBySegmentIndex { get; } = new();
    public Dictionary<TrainId, List<TrainId>> DerivedTrainsBySourceId { get; } = new();

    // 駅×番線をキーに、発車時刻昇順のTrainを引けるようにするインデックス（6.4節TrainConnectionResolverが使用）。
    // 構築はTrainConnectionResolver.BuildDepartureIndex()側の責務とする（TrainOperationIndex等と同じ責務分離）。
    public Dictionary<(StationId StationId, RailId RailId), List<(int DepartureSeconds, TrainId TrainId)>> DepartureByStationTrackIndex { get; } = new();

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
        IEnumerable<TemporaryRestriction> restrictions)
    {
        TrainNumberIndex.Clear();
        TrainOperationIndex.Clear();
        EntryPointConnectionIndex.Clear();
        StationConnectionIndex.Clear();
        MainRouteConnectionIndex.Clear();
        ScsUsedByIndex.Clear();
        TemporaryRestrictionBySegmentIndex.Clear();
        DerivedTrainsBySourceId.Clear();
        DepartureByStationTrackIndex.Clear();
        ConflictObjectGroupingCache.Clear();
        _conflictDirty.Clear();

        // 以下、各種インデックス構築（省略）
        // TrainNumberIndex
        foreach (var train in trains)
        {
            if (!string.IsNullOrEmpty(train.TrainNumber))
                TrainNumberIndex[train.TrainNumber] = train.Id;
        }

        // TrainOperationIndex は TrainOperationChainResolver が構築する
        // DepartureByStationTrackIndex は TrainConnectionResolver.BuildDepartureIndex() が構築する
        // 他のインデックスも同様に Builder が構築する
    }
}
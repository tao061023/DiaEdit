namespace DiaEditCore.Algorithm.Dependency;

using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.TimeTable;

/// <summary>
/// TimeTableSetCache.FloorUnitDependentIndex（v12.16新設）の構築を担う。
/// DepartureByStationTrackIndex等と同じ責務分離規約（構築ロジックをキャッシュ本体から切り離す）に従う。
///
/// 対象6種：BoundaryPoint／EntryPoint／BufferStop／Switcher／Platform（いずれも
/// FloorUnitObjectBase.FloorUnitId経由）／StationPath（FloorUnitIdを直接保持）。
/// </summary>
public static class FloorUnitDependentIndexBuilder
{
    public static void Build(
        TimeTableSetCache cache,
        IEnumerable<BoundaryPoint> boundaryPoints,
        IEnumerable<EntryPoint> entryPoints,
        IEnumerable<BufferStop> bufferStops,
        IEnumerable<Switcher> switchers,
        IEnumerable<Platform> platforms,
        IEnumerable<StationPath> stationPaths)
    {
        cache.FloorUnitDependentIndex.Clear();

        void Add(FloorUnitId floorUnitId, ObjectId objectId)
        {
            if (!cache.FloorUnitDependentIndex.TryGetValue(floorUnitId, out var list))
            {
                list = new List<ObjectId>();
                cache.FloorUnitDependentIndex[floorUnitId] = list;
            }
            list.Add(objectId);
        }

        foreach (var b in boundaryPoints) Add(b.Base.FloorUnitId, new BoundaryPointObjectId(b.Id));
        foreach (var e in entryPoints) Add(e.Base.FloorUnitId, new EntryPointObjectId(e.Id));
        foreach (var bs in bufferStops) Add(bs.Base.FloorUnitId, new BufferStopObjectId(bs.Id));
        foreach (var sw in switchers) Add(sw.Base.FloorUnitId, new SwitcherObjectId(sw.Id));
        foreach (var p in platforms) Add(p.Base.FloorUnitId, new PlatformObjectId(p.Id));
        foreach (var sp in stationPaths) Add(sp.FloorUnitId, new StationPathObjectId(sp.Id));
    }
}
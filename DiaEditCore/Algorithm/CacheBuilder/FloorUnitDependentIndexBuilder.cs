namespace DiaEditCore.Algorithm.CacheBuilder;

using DiaEditCore.Model;
using DiaEditCore.Model.Stations;

/// <summary>
/// TimeTableSetCache.FloorUnitDependentIndex（v12.16新設）の構築を担う。
/// DepartureByStationTrackIndex等と同じ責務分離規約（構築ロジックをキャッシュ本体から切り離す）に従う。
///
/// 対象6種：BoundaryPoint／EntryPoint／BufferStop／Switcher／Platform（いずれも
/// FloorUnitObjectBase.FloorUnitId経由）／StationPath（FloorUnitIdを直接保持）。
///
/// v12.18：cacheへの直接書き込みから戻り値を返す方式へ変更（Builder間の方式統一。
/// TrainConnectionResolver.BuildDepartureIndex／StopKeyReferenceIndexBuilder.Buildと同じ形にすることで、
/// cacheのモック不要でBuilder単体をユニットテストできるようにするため）。
/// </summary>
public static class FloorUnitDependentIndexBuilder
{
    // 変更前: public static void Build(TimeTableSetCache cache, IEnumerable<...> ...)
    // 変更後: cache引数を廃止し、Dictionaryを戻り値として返す
    public static Dictionary<FloorUnitId, List<ObjectId>> Build(
        IEnumerable<BoundaryPoint> boundaryPoints,
        IEnumerable<EntryPoint> entryPoints,
        IEnumerable<BufferStop> bufferStops,
        IEnumerable<Switcher> switchers,
        IEnumerable<Platform> platforms,
        IEnumerable<StationPath> stationPaths)
    {
        var index = new Dictionary<FloorUnitId, List<ObjectId>>();

        void Add(FloorUnitId floorUnitId, ObjectId objectId)
        {
            if (!index.TryGetValue(floorUnitId, out var list))
            {
                list = new List<ObjectId>();
                index[floorUnitId] = list;
            }
            list.Add(objectId);
        }

        foreach (var b in boundaryPoints) Add(b.Base.FloorUnitId, new BoundaryPointObjectId(b.Id));
        foreach (var e in entryPoints) Add(e.Base.FloorUnitId, new EntryPointObjectId(e.Id));
        foreach (var bs in bufferStops) Add(bs.Base.FloorUnitId, new BufferStopObjectId(bs.Id));
        foreach (var sw in switchers) Add(sw.Base.FloorUnitId, new SwitcherObjectId(sw.Id));
        foreach (var p in platforms) Add(p.Base.FloorUnitId, new PlatformObjectId(p.Id));
        foreach (var sp in stationPaths) Add(sp.FloorUnitId, new StationPathObjectId(sp.Id));

        return index;
    }
}
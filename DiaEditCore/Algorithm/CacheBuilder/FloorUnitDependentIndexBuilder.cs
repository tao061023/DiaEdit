namespace DiaEditCore.Algorithm.CacheBuilder;

using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;

/// <summary>
/// TimeTableSetCache.FloorUnitDependentIndexの構築。FloorUnit配下のObjectの依存関係解決。
/// 
/// 対象7種：NoneEndpoint／BoundaryPoint／EntryPoint／BufferStop／Switcher／Platform
/// （いずれもFloorUnitObjectBase.FloorUnitId経由）／StationPath（FloorUnitIdを直接保持）。
/// </summary>
public static class FloorUnitDependentIndexBuilder
{
    public static Dictionary<FloorUnitId, List<ObjectId>> Build(
        IEnumerable<NoneEndpoint> noneEndpoints,
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

        foreach (var n in noneEndpoints) Add(n.Base.FloorUnitId, new NoneEndpointObjectId(n.Id));
        foreach (var b in boundaryPoints) Add(b.Base.FloorUnitId, new BoundaryPointObjectId(b.Id));
        foreach (var e in entryPoints) Add(e.Base.FloorUnitId, new EntryPointObjectId(e.Id));
        foreach (var bs in bufferStops) Add(bs.Base.FloorUnitId, new BufferStopObjectId(bs.Id));
        foreach (var sw in switchers) Add(sw.Base.FloorUnitId, new SwitcherObjectId(sw.Id));
        foreach (var p in platforms) Add(p.Base.FloorUnitId, new PlatformObjectId(p.Id));
        foreach (var sp in stationPaths) Add(sp.FloorUnitId, new StationPathObjectId(sp.Id));

        return index;
    }
}
namespace DiaEditCore.Algorithm.Stations;

using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;

/// <summary>
/// RailはFloorUnitIdを直接持たない（§4.4.3、EndpointA/Bの接続先端点オブジェクト経由で導出される
/// 派生関係）ため、「指定FloorUnitに属するRail」を都度算出する共有ロジック。
/// 元はFloorUnitDetailViewModel.ResolveFloorUnitId／RailsBelongingToThisFloorUnitとして
/// ViewModel内に閉じていたが、FloorUnitSummaryResolverからも必要になったため
/// Core層へ切り出した（単一の情報源原則）。専用逆引きIndexは新設しない
/// （DeleteRailCommandと同じ判断基準：消費者が少数・件数規模も小さいため線形走査で足りる）。
/// </summary>
public static class RailFloorUnitLookup
{
    public static FloorUnitId? ResolveFloorUnitId(
        RailEndpointRef endpoint,
        IReadOnlyList<NoneEndpoint> noneEndpoints,
        IReadOnlyList<BoundaryPoint> boundaryPoints,
        IReadOnlyList<EntryPoint> entryPoints,
        IReadOnlyList<BufferStop> bufferStops,
        IReadOnlyList<Switcher> switchers) => endpoint switch
    {
        NoneEndpointRef n => noneEndpoints.FirstOrDefault(x => x.Id == n.Id)?.Base.FloorUnitId,
        BoundaryPointEndpointRef b => boundaryPoints.FirstOrDefault(x => x.Id == b.Id)?.Base.FloorUnitId,
        EntryPointEndpointRef e => entryPoints.FirstOrDefault(x => x.Id == e.Id)?.Base.FloorUnitId,
        BufferStopEndpointRef bs => bufferStops.FirstOrDefault(x => x.Id == bs.Id)?.Base.FloorUnitId,
        SwitcherEndpointRef sw => switchers.FirstOrDefault(x => x.Id == sw.Id)?.Base.FloorUnitId,
        _ => null,
    };

    public static IEnumerable<Rail> RailsBelongingTo(
        FloorUnitId floorUnitId,
        IReadOnlyList<Rail> allRails,
        IReadOnlyList<NoneEndpoint> noneEndpoints,
        IReadOnlyList<BoundaryPoint> boundaryPoints,
        IReadOnlyList<EntryPoint> entryPoints,
        IReadOnlyList<BufferStop> bufferStops,
        IReadOnlyList<Switcher> switchers) =>
        allRails.Where(r =>
            ResolveFloorUnitId(r.EndpointA, noneEndpoints, boundaryPoints, entryPoints, bufferStops, switchers) == floorUnitId ||
            ResolveFloorUnitId(r.EndpointB, noneEndpoints, boundaryPoints, entryPoints, bufferStops, switchers) == floorUnitId);
}
namespace DiaEditCore.Algorithm;

using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;

/// <summary>
/// FloorUnit詳細編集画面（UI設計書§4.2.2改訂）の右ペイン概要表示用。
/// 保存はしない都度算出の派生値（Pattern C：ProjectSessionのdirty管理下に置かない）。
///
/// 「番線数」＝旅客案内上の番線ではなく、在線・占有チェック対象（RailRole.Track）の本数
/// （2026-09セッションでTao様と合意した定義。Platform件数とは意図的に一致させていない）。
/// </summary>
public sealed record FloorUnitSummary(
    int TrackRailCount,
    int ShuntingRailCount,
    int TotalRailCount,
    int StationPathCount,
    bool HasUnreferencedTrackEndpoints
);

public static class FloorUnitSummaryResolver
{
    public static FloorUnitSummary Build(
        FloorUnitId floorUnitId,
        IReadOnlyList<Rail> allRails,
        IReadOnlyList<StationPath> allStationPaths,
        IReadOnlyList<NoneEndpoint> noneEndpoints,
        IReadOnlyList<BoundaryPoint> boundaryPoints,
        IReadOnlyList<EntryPoint> entryPoints,
        IReadOnlyList<BufferStop> bufferStops,
        IReadOnlyList<Switcher> switchers)
    {
        var rails = RailFloorUnitLookup.RailsBelongingTo(
            floorUnitId, allRails, noneEndpoints, boundaryPoints, entryPoints, bufferStops, switchers).ToList();

        var trackCount = rails.Count(r => r.Role == RailRole.Track);
        var shuntingCount = rails.Count(r => r.Role == RailRole.Shunting);
        var stationPathCount = allStationPaths.Count(sp => sp.FloorUnitId == floorUnitId);

        // このFloorUnitに属するBoundaryPoint/EntryPointのうち、いずれのRail端点からも
        // 参照されていないもの＝§4.2.3「未参照のTrack端部（BufferStop除く）」の検出。
        var referencedIds = rails
            .SelectMany(r => new[] { r.EndpointA.ToObjectId(), r.EndpointB.ToObjectId() })
            .Where(id => id is not null)
            .Select(id => id!)
            .ToHashSet();

        bool hasUnreferenced =
            boundaryPoints.Any(b => b.Base.FloorUnitId == floorUnitId
                && !referencedIds.Contains(new BoundaryPointObjectId(b.Id)))
            || entryPoints.Any(e => e.Base.FloorUnitId == floorUnitId
                && !referencedIds.Contains(new EntryPointObjectId(e.Id)));

        return new FloorUnitSummary(trackCount, shuntingCount, rails.Count, stationPathCount, hasUnreferenced);
    }
}
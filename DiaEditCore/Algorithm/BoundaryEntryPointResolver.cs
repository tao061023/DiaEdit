namespace DiaEditCore.Algorithm;

using DiaEditCore.Model;
using DiaEditCore.Model.Routes;

/// <summary>
/// 境界駅のEPを、既存のEntryPointSequenceResolverの結果から取り出すためのラッパー。 <br/>
/// ServiceRoutePathResolver・ReversalResolverが下請けとして利用する。 <br/>
/// 都度導出・非保存。複々線等で対応するStationConnectionが複数存在しうるため、 <br/>
/// 該当する全候補を返す（列車種別ごとに利用可能なEPが異なりうるため、この段階では絞り込まない）。
/// </summary>
public static class BoundaryEntryPointResolver
{
    /// <summary>
    /// mainRouteId上のfromIndex→toIndexが示す駅間に対応するStationConnectionを探索し、
    /// 各候補について境界駅（toIndex側）に該当するEntryPointSequenceElementを返す。
    /// </summary>
    /// <param name="mainRouteId">対象MainRoute</param>
    /// <param name="fromIndex">MainRoute.StationOrder上の開始インデックス</param>
    /// <param name="toIndex">MainRoute.StationOrder上の終了インデックス（from &lt; toなら下り、from &gt; toなら上り）</param>
    /// <param name="allMainRoutes">MainRoute全体（StationOrder参照用）</param>
    /// <param name="allStationConnections">StationConnection全体</param>
    /// <param name="allSegments">StationConnectionSegment全体</param>
    /// <returns>
    /// 一致するStationConnectionそれぞれについて、境界駅（toIndex側）に該当するEntryPointSequenceElementを1件ずつ含むリスト。<br/>
    /// 一致するStationConnectionが存在しない場合は空リスト（呼び出し側で「対応するStationConnectionが実在しない」として扱う）。
    /// </returns>
    public static IReadOnlyList<EntryPointSequenceElement> ResolveBoundaryEntryPoint(
        MainRouteId mainRouteId,
        int fromIndex,
        int toIndex,
        IReadOnlyList<MainRoute> allMainRoutes,
        IReadOnlyList<StationConnection> allStationConnections,
        IReadOnlyList<StationConnectionSegment> allSegments)
    {
        var mainRoute = allMainRoutes.FirstOrDefault(mr => mr.Id == mainRouteId);
        if (mainRoute is null) return Array.Empty<EntryPointSequenceElement>();

        var stationOrder = mainRoute.StationOrder;
        if (fromIndex < 0 || fromIndex >= stationOrder.Count ||
            toIndex < 0 || toIndex >= stationOrder.Count ||
            fromIndex == toIndex)
        {
            return Array.Empty<EntryPointSequenceElement>();
        }

        // fromIndex < toIndex ： 下り（Down）／ fromIndex > toIndex ： 上り（Up）
        var direction = fromIndex < toIndex
            ? StationConnectionDirection.Down
            : StationConnectionDirection.Up;

        // 走行方向に沿った期待駅列（fromIndex→toIndexの経路上の全駅、境界含む）
        var expectedStations = BuildExpectedStations(stationOrder, fromIndex, toIndex);

        var result = new List<EntryPointSequenceElement>();
        foreach (var sc in allStationConnections)
        {
            if (sc.MainRouteId != mainRouteId || sc.Direction != direction) continue;

            var seq = EntryPointSequenceResolver.Resolve(sc, allSegments);
            if (!MatchesExpectedStations(seq, expectedStations)) continue;

            // 境界駅（toIndex側）に該当する要素は、期待駅列と一致した列の末尾要素
            result.Add(seq[^1]);
        }

        return result;
    }
    /// <summary>
    /// ResolveBoundaryEntryPointと同じ照合ロジックで、一致したStationConnection自体のIdを返す版。
    /// SyncRunSegmentsToTrainCommand等、ホップ単位でどのStationConnectionを使うか確定させたい
    /// 呼び出し元向け。
    /// </summary>
    public static IReadOnlyList<StationConnectionId> ResolveBoundaryStationConnection(
        MainRouteId mainRouteId,
        int fromIndex,
        int toIndex,
        IReadOnlyList<MainRoute> allMainRoutes,
        IReadOnlyList<StationConnection> allStationConnections,
        IReadOnlyList<StationConnectionSegment> allSegments)
    {
        var mainRoute = allMainRoutes.FirstOrDefault(mr => mr.Id == mainRouteId);
        if (mainRoute is null) return Array.Empty<StationConnectionId>();

        var stationOrder = mainRoute.StationOrder;
        if (fromIndex < 0 || fromIndex >= stationOrder.Count ||
            toIndex < 0 || toIndex >= stationOrder.Count ||
            fromIndex == toIndex)
        {
            return Array.Empty<StationConnectionId>();
        }

        var direction = fromIndex < toIndex
            ? StationConnectionDirection.Down
            : StationConnectionDirection.Up;

        var expectedStations = BuildExpectedStations(stationOrder, fromIndex, toIndex);

        var result = new List<StationConnectionId>();
        foreach (var sc in allStationConnections)
        {
            if (sc.MainRouteId != mainRouteId || sc.Direction != direction) continue;

            var seq = EntryPointSequenceResolver.Resolve(sc, allSegments);
            if (!MatchesExpectedStations(seq, expectedStations)) continue;

            result.Add(sc.Id);
        }

        return result;
    }

    private static List<StationId> BuildExpectedStations(IReadOnlyList<StationId> stationOrder, int fromIndex, int toIndex)
    {
        var stations = new List<StationId>();
        if (fromIndex < toIndex)
        {
            for (var i = fromIndex; i <= toIndex; i++) stations.Add(stationOrder[i]);
        }
        else
        {
            for (var i = fromIndex; i >= toIndex; i--) stations.Add(stationOrder[i]);
        }
        return stations;
    }

    /// <summary>
    /// EntryPointSequenceElement列が示す駅の連鎖（Fromの先頭→各要素のTo）が
    /// expectedStationsと完全一致するかを検証する。
    /// </summary>
    private static bool MatchesExpectedStations(
        IReadOnlyList<EntryPointSequenceElement> seq,
        IReadOnlyList<StationId> expectedStations)
    {
        // 期待される区間数はexpectedStations.Count - 1（駅数 - 1 = ホップ数）
        if (seq.Count != expectedStations.Count - 1) return false;
        if (seq.Count == 0) return false;

        if (seq[0].FromStationId != expectedStations[0]) return false;

        for (var i = 0; i < seq.Count; i++)
        {
            if (seq[i].ToStationId != expectedStations[i + 1]) return false;
            if (i > 0 && seq[i].FromStationId != seq[i - 1].ToStationId) return false;
        }

        return true;
    }
}

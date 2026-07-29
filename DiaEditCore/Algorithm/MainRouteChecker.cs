using DiaEditCore.Model;
using DiaEditCore.Model.Stations;

namespace DiaEditCore.Algorithm;

/// <summary>
/// 5.7節「MainRoute整合性」・5.8節「境界駅の整合性条件」共通ロジック。
/// EntryPointId列（長さ2N、[From_0,To_0,From_1,To_1,...]）を受け取り、
/// 隣接するSCS境界（EP[2i+1]⇔EP[2i+2]）で到着側Track集合と出発側Track集合が
/// 1件以上重複することを検証する。isLoopの場合はEP[0]⇔EP[^1]境界も追加検証する。
/// 都度導出・非保存。単一StationConnection（MainRoute整合性）・ServiceRoute結合列
/// （ServiceRoute整合性）の両方から共通して呼ばれる。
/// </summary>
public static class MainRouteChecker
{
    public sealed record BoundaryCheckResult(int BoundaryIndex, bool IsSatisfied);

    /// <param name="entryPointSequence">
    /// [From_0, To_0, From_1, To_1, ..., From_{N-1}, To_{N-1}]（長さ2N、N=SCS数）
    /// </param>
    public static IReadOnlyList<BoundaryCheckResult> CheckBoundaryConnectivity(
        IReadOnlyList<EntryPointId> entryPointSequence,
        bool isLoop,
        IReadOnlyDictionary<(StationPathTrackIndexBuilder.BoundaryTerminal, RailId), StationPathId> arrivalIndex,
        IReadOnlyDictionary<(RailId, StationPathTrackIndexBuilder.BoundaryTerminal), StationPathId> departureIndex)
    {
        if (entryPointSequence.Count % 2 != 0 || entryPointSequence.Count < 2)
            throw new ArgumentException("entryPointSequenceは長さ2N（N>=1）である必要があります。");

        var results = new List<BoundaryCheckResult>();
        var n = entryPointSequence.Count / 2;

        for (var i = 0; i < n - 1; i++)
        {
            var arrivalEp = entryPointSequence[2 * i + 1];   // SCS[i]の到着側
            var departureEp = entryPointSequence[2 * i + 2]; // SCS[i+1]の出発側
            results.Add(new BoundaryCheckResult(i, HasOverlap(arrivalEp, departureEp, arrivalIndex, departureIndex)));
        }

        if (isLoop)
        {
            var arrivalEp = entryPointSequence[^1]; // 末尾SCSの到着側
            var departureEp = entryPointSequence[0]; // 先頭SCSの出発側
            results.Add(new BoundaryCheckResult(n - 1, HasOverlap(arrivalEp, departureEp, arrivalIndex, departureIndex)));
        }

        return results;
    }

    private static bool HasOverlap(
        EntryPointId arrivalEp,
        EntryPointId departureEp,
        IReadOnlyDictionary<(StationPathTrackIndexBuilder.BoundaryTerminal, RailId), StationPathId> arrivalIndex,
        IReadOnlyDictionary<(RailId, StationPathTrackIndexBuilder.BoundaryTerminal), StationPathId> departureIndex)
    {
        var arrivalTerminal = StationPathTrackIndexBuilder.BoundaryTerminal.FromEntryPoint(arrivalEp);
        var departureTerminal = StationPathTrackIndexBuilder.BoundaryTerminal.FromEntryPoint(departureEp);

        var arrivalTracks = arrivalIndex.Keys
            .Where(k => k.Item1 == arrivalTerminal)
            .Select(k => k.Item2)
            .ToHashSet();

        var departureTracks = departureIndex.Keys
            .Where(k => k.Item2 == departureTerminal)
            .Select(k => k.Item1)
            .ToHashSet();

        return arrivalTracks.Overlaps(departureTracks);
    }
}
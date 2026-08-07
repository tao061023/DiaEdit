using DiaEditCore.Model.Routes;
using DiaEditCore.Model.Stations;

namespace DiaEditCore.Algorithm;

public enum AnchorMode
{
    Auto,      // 自動調整：アンカーの実測値でsegmentTimeを書き換える
    Manual,    // 手動：Autoと同じ差分計算を行うが、確定はせず提案のみ返す
    Disabled   // 無効：baseline値のみ返す。実測がderivedを下回っていれば警告
}

/// <summary>
/// 所要時分計算のアンカー（実測値が既知の地点）。
/// StationIndexはstationPaths配列内のindexを指す。
/// 駅0（出発駅）はIsArrival=trueを持てない。最終駅（stationPaths[^1]）はIsArrival=falseを持てない。
/// </summary>
public sealed record RunTimeAnchor(int StationIndex, bool IsArrival, int ActualElapsedSec);

/// <summary>
/// Manualモードで提案される、特定区間への差分適用案。
/// </summary>
public sealed record ProposedAdjustment(
    int SegmentIndex,
    int OriginalSeconds,
    int AdjustedSeconds,
    RunTimeAnchor Anchor
);

/// <summary>
/// Disabledモードで検出される警告：実測経過秒数がderived（baseline累積）を下回っている。
/// </summary>
public sealed record RunTimeWarning(
    RunTimeAnchor Anchor,
    int DerivedElapsedSec,
    int ActualElapsedSec
);

public sealed record RunTimeCalculationResult(
    IReadOnlyList<int> SegmentSeconds,
    IReadOnlyList<ProposedAdjustment> ProposedAdjustments,
    IReadOnlyList<RunTimeWarning> Warnings
);

// DiaEditCore/Algorithm/RunTimeCalculator.cs
// ... (前半は変更なし：AnchorMode, RunTimeAnchor, ProposedAdjustment, RunTimeWarning, RunTimeCalculationResult は同じ)

public static class RunTimeCalculator
{
    public static RunTimeCalculationResult Calculate(
        IReadOnlyList<StationPath> stationPaths,
        IReadOnlyList<StationConnectionSegment> segments,
        IReadOnlyList<RunTimeAnchor> anchors,
        AnchorMode mode)
    {
        if (stationPaths is null) throw new ArgumentNullException(nameof(stationPaths));
        if (segments is null) throw new ArgumentNullException(nameof(segments));
        if (anchors is null) throw new ArgumentNullException(nameof(anchors));

        if (stationPaths.Count < 2)
            throw new ArgumentException(
                "stationPaths must contain at least 2 elements (departure and arrival).",
                nameof(stationPaths));

        if (stationPaths.Count != segments.Count + 1)
            throw new ArgumentException(
                $"stationPaths.Count ({stationPaths.Count}) must equal segments.Count + 1 ({segments.Count + 1}).",
                nameof(segments));

        int lastStationIndex = stationPaths.Count - 1;
        int segCount = segments.Count;

        var baseline = new int[segCount];
        for (int k = 0; k < segCount; k++)
        {
            baseline[k] = segments[k].BaseRunTimeSec
                        + stationPaths[k].AdjustmentSec
                        + stationPaths[k + 1].AdjustmentSec;
        }

        foreach (var a in anchors)
        {
            if (a.StationIndex < 0 || a.StationIndex > lastStationIndex)
                throw new ArgumentOutOfRangeException(
                    nameof(anchors),
                    $"Anchor StationIndex {a.StationIndex} is out of range [0, {lastStationIndex}].");

            if (a.IsArrival && a.StationIndex == 0)
                throw new ArgumentException(
                    "出発駅（StationIndex == 0）に到着アンカーは設定できません。",
                    nameof(anchors));

            if (!a.IsArrival && a.StationIndex == lastStationIndex)
                throw new ArgumentException(
                    $"到着駅（StationIndex == {lastStationIndex}）に出発アンカーは設定できません。",
                    nameof(anchors));
        }

        var sortedAnchors = anchors
            .OrderBy(a => a.StationIndex)
            .ThenBy(a => a.IsArrival ? 0 : 1) // 同一駅内は到着→出発
            .ToList();

        if (mode == AnchorMode.Disabled)
        {
            var warnings = new List<RunTimeWarning>();
            foreach (var a in sortedAnchors)
            {
                int derived = SumRange(baseline, 0, a.StationIndex);
                if (a.ActualElapsedSec < derived)
                    warnings.Add(new RunTimeWarning(a, derived, a.ActualElapsedSec));
            }
            return new RunTimeCalculationResult(baseline.ToList(), Array.Empty<ProposedAdjustment>(), warnings);
        }

        var adjusted = (int[])baseline.Clone();
        var proposals = new List<ProposedAdjustment>();

        int lastAnchorIndex = 0; // 次に確定させるべき区間の先頭index（＝現在の基準点が指す駅index）
        int lastAnchorTime = 0;

        foreach (var a in sortedAnchors)
        {
            if (a.IsArrival)
            {
                // 到着アンカー：区間 [lastAnchorIndex .. StationIndex-1] を1本の区間として閉じる
                int targetSegment = a.StationIndex - 1;

                if (targetSegment >= lastAnchorIndex)
                {
                    int naiveSum = SumRange(baseline, lastAnchorIndex, targetSegment + 1);
                    int derivedElapsed = lastAnchorTime + naiveSum;
                    int diff = a.ActualElapsedSec - derivedElapsed;

                    if (diff != 0)
                    {
                        int original = adjusted[targetSegment];
                        adjusted[targetSegment] = original + diff;
                        proposals.Add(new ProposedAdjustment(targetSegment, original, adjusted[targetSegment], a));
                    }

                    lastAnchorIndex = targetSegment + 1;
                }
                // else: 既に出発アンカー等で先の基準点が確定済み → 何もしない

                lastAnchorTime = a.ActualElapsedSec;
            }
            else
            {
                // 出発アンカー：区間を書き換えない。以後の計算の「新しい基準点」を置くだけ
                // （到着〜出発の差分＝停車時分は走行区間に無関係なため）
                lastAnchorIndex = a.StationIndex;
                lastAnchorTime = a.ActualElapsedSec;
            }
        }

        var finalSegments = mode == AnchorMode.Auto ? adjusted.ToList() : baseline.ToList();
        var finalProposals = mode == AnchorMode.Manual
            ? (IReadOnlyList<ProposedAdjustment>)proposals
            : Array.Empty<ProposedAdjustment>();

        return new RunTimeCalculationResult(finalSegments, finalProposals, Array.Empty<RunTimeWarning>());
    }

    private static int SumRange(int[] arr, int startInclusive, int endExclusive)
    {
        int sum = 0;
        for (int i = startInclusive; i < endExclusive; i++) sum += arr[i];
        return sum;
    }
}
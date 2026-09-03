namespace DiaEditCore.Algorithm;

using DiaEditCore.Algorithm.CacheBuilder;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;

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

/// <summary>
/// ホップ単位の所要時分判定結果。EffectiveLengthCheckerの
/// LengthCheckOk／LengthCheckNotApplicable／LengthCheckOverflowと同型の判別共用体パターンを踏襲する。
/// </summary>
public abstract record HopRunTimeResult;

/// <summary>基準実績が見つかり、区間所要時分（アンカー調整適用後、モードに応じた値）が確定した。</summary>
public sealed record HopRunTimeOk(int Seconds) : HopRunTimeResult;

/// <summary>
/// 基準Train実績が見つからない（BaseRunTimeIndexBuilder.SelectionKeyに一致するTrainが
/// BaseTimeTableSet内に存在しない、またはDiagramRevision.BaseTimeTableSetId自体が未設定）。
/// UI上はOuDiaSecond互換の薄黄背景で表現する想定。
/// </summary>
public sealed record HopRunTimeUndefined : HopRunTimeResult;

public sealed record RunTimeCalculationResult(
    IReadOnlyList<HopRunTimeResult> Hops,
    IReadOnlyList<ProposedAdjustment> ProposedAdjustments,
    IReadOnlyList<RunTimeWarning> Warnings
);

/// <summary>
/// あるホップ（隣接駅1区間）の基準実績照合に必要な入力。
/// StationConnectionSegmentIdへの解決は呼び出し側（Train編集コマンド等）の責務とする
/// （BaseRunTimeIndexBuilderのコメント参照：TrainRunSegment.StationConnectionIdからの解決は
/// StationConnection.SegmentsとallSegmentsの突き合わせを要するため、Calculate自体はSCSId確定後の
/// 単純な辞書引きに専念させ、責務を分離する）。
/// </summary>
public sealed record RunTimeHopInput(
    StationConnectionSegmentId SegmentId,
    bool FromIsStop,
    bool ToIsStop);

/// <summary>
/// 区間所要時分算出：DiagramRevision.BaseTimeTableSetIdが指すTimeTableSet内のTrain実績から都度導出する。
/// baselineIndexの構築はBaseRunTimeIndexBuilder(Algorithm/CacheBuilder）の責務とし、
/// 本クラスは「確定済みindexを引いてアンカー調整を適用する」計算処理に専念する（責務分離）。
///
/// 基準実績が見つからないホップ（Undefined）は、アンカー調整の対象から除外する：
///   - baseline未確定のためAuto/Manualの差分計算そのものが定義できない
///   - そのホップを含む区間へのアンカー到達判定（SumRange）は、Undefinedホップを跨ぐ場合
///     「その区間全体もUndefined」として扱い、アンカーによる自動調整・警告の対象から除外する
///     （区間ごとに個別判定する方針。「区間ごとに他区間はOkのまま」方針に従い、Undefinedの伝播は当該アンカー区間内に限定する）
/// </summary>
public static class RunTimeCalculator
{
    public static RunTimeCalculationResult Calculate(
        IReadOnlyList<StationPath> stationPaths,
        IReadOnlyList<RunTimeHopInput> hops,
        IReadOnlyDictionary<BaseRunTimeIndexBuilder.SelectionKey, int> baseRunTimeIndex,
        Model.VehicleTypeId? vehicleTypeId,
        IReadOnlyList<RunTimeAnchor> anchors,
        AnchorMode mode)
    {
        if (stationPaths is null) throw new ArgumentNullException(nameof(stationPaths));
        if (hops is null) throw new ArgumentNullException(nameof(hops));
        if (baseRunTimeIndex is null) throw new ArgumentNullException(nameof(baseRunTimeIndex));
        if (anchors is null) throw new ArgumentNullException(nameof(anchors));

        if (stationPaths.Count < 2)
            throw new ArgumentException(
                "stationPaths must contain at least 2 elements (departure and arrival).",
                nameof(stationPaths));

        if (stationPaths.Count != hops.Count + 1)
            throw new ArgumentException(
                $"stationPaths.Count ({stationPaths.Count}) must equal hops.Count + 1 ({hops.Count + 1}).",
                nameof(hops));

        int lastStationIndex = stationPaths.Count - 1;
        int hopCount = hops.Count;

        // baseline[k]：基準実績が見つかった場合のみ値を持つ（null＝Undefined）
        var baseline = new int?[hopCount];
        for (int k = 0; k < hopCount; k++)
        {
            var key = new BaseRunTimeIndexBuilder.SelectionKey(
                hops[k].SegmentId, hops[k].FromIsStop, hops[k].ToIsStop, vehicleTypeId);

            baseline[k] = baseRunTimeIndex.TryGetValue(key, out var sec)
                ? sec + stationPaths[k].AdjustmentSec + stationPaths[k + 1].AdjustmentSec
                : null;
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
                var derived = SumRange(baseline, 0, a.StationIndex);
                if (derived is { } d && a.ActualElapsedSec < d)
                    warnings.Add(new RunTimeWarning(a, d, a.ActualElapsedSec));
            }
            return new RunTimeCalculationResult(ToHopResults(baseline), Array.Empty<ProposedAdjustment>(), warnings);
        }

        var adjusted = (int?[])baseline.Clone();
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
                    var naiveSum = SumRange(baseline, lastAnchorIndex, targetSegment + 1);

                    // Undefinedホップを跨ぐ区間は、そのアンカーによる調整自体を行わない
                    // （方針：Undefinedの伝播は当該アンカー区間内に限定し、
                    // 他のホップのbaseline確定状態には影響させない）。
                    if (naiveSum is { } sum)
                    {
                        int derivedElapsed = lastAnchorTime + sum;
                        int diff = a.ActualElapsedSec - derivedElapsed;

                        if (diff != 0 && adjusted[targetSegment] is { } originalValue)
                        {
                            var newValue = originalValue + diff;
                            adjusted[targetSegment] = newValue;
                            proposals.Add(new ProposedAdjustment(targetSegment, originalValue, newValue, a));
                        }
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

        var finalHops = mode == AnchorMode.Auto ? adjusted : baseline;
        var finalProposals = mode == AnchorMode.Manual
            ? (IReadOnlyList<ProposedAdjustment>)proposals
            : Array.Empty<ProposedAdjustment>();

        return new RunTimeCalculationResult(ToHopResults(finalHops), finalProposals, Array.Empty<RunTimeWarning>());
    }

    private static IReadOnlyList<HopRunTimeResult> ToHopResults(int?[] values)
        => values.Select(v => v is { } sec
                ? (HopRunTimeResult)new HopRunTimeOk(sec)
                : new HopRunTimeUndefined())
            .ToList();

    /// <summary>
    /// [startInclusive, endExclusive)の合計を返す。範囲内に1つでもUndefined（null）が
    /// 含まれる場合はnullを返す（そのアンカー区間全体をUndefined扱いとする）。
    /// </summary>
    private static int? SumRange(int?[] arr, int startInclusive, int endExclusive)
    {
        int sum = 0;
        for (int i = startInclusive; i < endExclusive; i++)
        {
            if (arr[i] is not { } v) return null;
            sum += v;
        }
        return sum;
    }
}
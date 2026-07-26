using DiaEditCore.Model.Routes;

namespace DiaEditCore.Algorithm;

/// <summary>
/// 6.2節：ServiceRoute.segmentsをたどり、各境界駅のEntryPointSequenceElementを連結した
/// 経路全体のEntryPointSequenceを導出する。出力型は5.7.1節のEntryPointSequenceElementを
/// そのまま再利用する（境界駅だけを含む部分列として扱う。新しい型は起こさない）。
/// 都度導出・非保存。
/// </summary>
public static class ServiceRoutePathResolver
{
    /// <summary>
    /// BoundaryEntryPointResolverが複々線等で複数候補を返した場合に、どれを採用するかを
    /// 呼び出し側に委ねるためのdelegate。候補が1件のみの場合は呼ばれない（自動採用）。
    /// </summary>
    public delegate EntryPointSequenceElement CandidateSelector(
        ServiceRouteSegment segment,
        IReadOnlyList<EntryPointSequenceElement> candidates);

    /// <summary>
    /// ServiceRoute全体のEntryPointSequenceを導出する。
    /// 全segmentがpaired情報（IsUnidirectional=true かつ PairedMainRouteId等が設定済み）を
    /// 持つ場合のみ [primarySeq, pairedSeq] の2本を返す。1つでも非pairedのsegmentが
    /// 混ざる場合は、pairedSeqを一切生成せず [primarySeq] の1本のみを返す
    /// （Pairedは「異なる路線を経由して同じ始終着駅を持つ運転系統」を表現するための仕組みであり、
    /// 一部segmentだけpaired側の値を持つ中途半端な状態は許容しない）。
    /// 各列の要素は、対応するBoundaryEntryPointResolverの候補が0件だった場合はnull
    /// （「対応するStationConnectionが実在しない」の判定・エラー化は呼び出し側の責務とする。
    /// 6.1節BoundaryEntryPointResolverと同じ責務分離方針）。
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<EntryPointSequenceElement?>> ResolveServiceRoutePath(
        ServiceRoute sr,
        IReadOnlyList<MainRoute> allMainRoutes,
        IReadOnlyList<StationConnection> allStationConnections,
        IReadOnlyList<StationConnectionSegment> allSegments,
        CandidateSelector selector)
    {
        // 「Pairedは異なる路線を経由して同じ始終着駅を持つ運転系統を表現するためのもの」
        // という前提のため、1つでも非pairedのsegmentが混ざる場合は全体を非paired扱いとする
        // （一部segmentだけpaired側の値を持つ、という中途半端な状態を許容しない）。
        var allSegmentsPaired = sr.Segments.Count > 0 && sr.Segments.All(IsPaired);

        var primarySeq = new List<EntryPointSequenceElement?>(sr.Segments.Count);
        var pairedSeq = allSegmentsPaired ? new List<EntryPointSequenceElement?>(sr.Segments.Count) : null;

        foreach (var seg in sr.Segments)
        {
            var primaryCandidates = BoundaryEntryPointResolver.ResolveBoundaryEntryPoint(
                seg.MainRouteId, seg.FromStationIndex, seg.ToStationIndex,
                allMainRoutes, allStationConnections, allSegments);
            primarySeq.Add(SelectCandidate(seg, primaryCandidates, selector));

            if (pairedSeq is not null)
            {
                // allSegmentsPaired == true の場合のみここに入るため、
                // PairedMainRouteId等はIsPaired()により全segmentで設定済みであることが保証されている
                var pairedCandidates = BoundaryEntryPointResolver.ResolveBoundaryEntryPoint(
                    seg.PairedMainRouteId!.Value, seg.PairedFromStationIndex!.Value, seg.PairedToStationIndex!.Value,
                    allMainRoutes, allStationConnections, allSegments);
                pairedSeq.Add(SelectCandidate(seg, pairedCandidates, selector));
            }
        }

        return pairedSeq is not null
            ? new List<IReadOnlyList<EntryPointSequenceElement?>> { primarySeq, pairedSeq }
            : new List<IReadOnlyList<EntryPointSequenceElement?>> { primarySeq };
    }

    private static bool IsPaired(ServiceRouteSegment segment)
        => segment.IsUnidirectional
           && segment.PairedMainRouteId.HasValue
           && segment.PairedFromStationIndex.HasValue
           && segment.PairedToStationIndex.HasValue;

    private static EntryPointSequenceElement? SelectCandidate(
        ServiceRouteSegment segment,
        IReadOnlyList<EntryPointSequenceElement> candidates,
        CandidateSelector selector)
    {
        return candidates.Count switch
        {
            0 => null, // 対応するStationConnectionが実在しない。エラー化は呼び出し側の責務。
            1 => candidates[0], // 候補が1件のみなら自動採用（selectorを呼ばない）
            _ => selector(segment, candidates),
        };
    }
}
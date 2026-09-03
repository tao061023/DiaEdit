namespace DiaEditCore.Algorithm;

using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.TimeTable.Trains;

/// <summary>
/// ホップ（隣接駅1区間）ごとの解決結果。3種のケースを判別共用体で表現する。
/// </summary>
public abstract record HopResolution;

public sealed record HopResolved(TrainRunSegment Segment) : HopResolution;

public sealed record HopUnresolved(StationId FromStationId, StationId ToStationId) : HopResolution;

public sealed record HopTransferBlocked(
    StationId FromStationId,
    StationId ToStationId,
    StationConnectionId AttemptedScId,
    StationConnectionId PreviousScId) : HopResolution;

/// <summary>
/// ServiceRoute＋方向（上り/下り）から、ホップ単位でStationConnectionを確定させた
/// TrainRunSegment列を導出する。新規Train追加（4.9.2節 経路③）専用。都度導出・非保存。
///
/// v12.29 SCS direction-agnostic renameセッションでの変更点：
///   - ResolveHopCandidatesを、StationConnectionSegment.StationIdA/StationIdBに対する
///     無向マッチングへ変更した。旧実装は「SegmentのFrom/Toは走行方向と厳密に一致し、
///     方向ごとに別Segmentエンティティを用意する」前提だったため、双単線区間で同一SCSを
///     上り方向SCと下り方向SCの双方が参照するケースを取りこぼしていた
///     （fromStationId/toStationIdを逆に指定すると0件になっていた）。今回の変更により、
///     このケースが正しく候補として拾われるようになる（本セッションの主目的）。
///   - CanTransferAtはEntryPointSequenceResolver.Resolve（系統(ii)）の新シグネチャ
///     （allMainRoutes追加）に追従した。
/// </summary>
public static class ServiceRouteToRunSegmentsResolver
{
    public delegate StationConnectionId? HopCandidateSelector(
        StationId fromStationId,
        StationId toStationId,
        IReadOnlyList<StationConnectionId> candidates,
        StationConnectionId? previousHopSelection);

    public static IReadOnlyList<HopResolution> Resolve(
        ServiceRoute sr,
        bool isUpDirection,
        IReadOnlyList<MainRoute> allMainRoutes,
        IReadOnlyList<StationConnection> allStationConnections,
        IReadOnlyList<StationConnectionSegment> allSegments,
        IReadOnlyList<StationPath> allStationPaths,
        IReadOnlyList<Rail> allRails,
        HopCandidateSelector selector)
    {
        var result = new List<HopResolution>();
        StationConnectionId? previousHopSc = null;

        var (arrivalIndex, departureIndex) =
            StationPathTrackIndexBuilder.BuildWithBoundaryTerminals(allStationPaths, allRails);

        var effectiveSegments = BuildEffectiveSegments(sr.Segments, isUpDirection);

        foreach (var seg in effectiveSegments)
        {
            var mainRoute = allMainRoutes.FirstOrDefault(mr => mr.Id == seg.MainRouteId);
            if (mainRoute is null)
            {
                return result;
            }

            var stationOrder = mainRoute.StationOrder;
            var stepDown = Math.Sign(seg.ToStationIndex - seg.FromStationIndex);
            if (stepDown == 0)
            {
                return result;
            }

            var direction = stepDown > 0 ? StationConnectionDirection.Down : StationConnectionDirection.Up;

            var i = seg.FromStationIndex;
            while (i != seg.ToStationIndex)
            {
                var nextI = i + stepDown;
                var fromStationId = stationOrder[i];
                var toStationId = stationOrder[nextI];

                var candidates = ResolveHopCandidates(
                    seg.MainRouteId, fromStationId, toStationId, direction,
                    allStationConnections, allSegments);

                if (candidates.Count == 0)
                {
                    result.Add(new HopUnresolved(fromStationId, toStationId));
                    return result;
                }

                StationConnectionId? chosenSc;

                if (candidates.Count == 1)
                {
                    chosenSc = candidates[0];
                }
                else if (previousHopSc is { } inheritedSc && candidates.Contains(inheritedSc))
                {
                    chosenSc = inheritedSc;
                }
                else
                {
                    var selected = selector(fromStationId, toStationId, candidates, previousHopSc);

                    if (selected is null)
                    {
                        result.Add(new HopUnresolved(fromStationId, toStationId));
                        previousHopSc = null;
                        i = nextI;
                        continue;
                    }

                    if (previousHopSc is { } prevSc && selected.Value != prevSc)
                    {
                        if (!CanTransferAt(fromStationId, prevSc, selected.Value,
                                allStationConnections, allSegments, allMainRoutes, arrivalIndex, departureIndex))
                        {
                            result.Add(new HopTransferBlocked(fromStationId, toStationId, selected.Value, prevSc));
                            i = nextI;
                            continue;
                        }
                    }

                    chosenSc = selected.Value;
                }

                result.Add(new HopResolved(new TrainRunSegment
                {
                    FromStationId = fromStationId,
                    ToStationId = toStationId,
                    StationConnectionId = chosenSc.Value,
                    IsOverriddenFromTemplate = false,
                }));

                previousHopSc = chosenSc;
                i = nextI;
            }
        }

        return result;
    }

    /// <summary>
    /// あるホップ（fromStationId→toStationId、direction指定の走行方向）に該当する
    /// StationConnectionSegment（MainRouteId一致・StationIdA/StationIdBへの無向マッチング）を
    /// 1つでもSegmentsに含むStationConnection（MainRouteId一致・Direction一致）を、
    /// 包含関係で列挙する。
    ///
    /// v12.29変更：StationIdA/StationIdBは無向ペアのため、fromStationId/toStationIdの
    /// どちらの順序で一致してもよい（RailSequenceResolver.FindRailBetweenと同じ精神）。
    /// これにより双単線区間で共有されるSCSも正しく候補として拾われる。
    /// </summary>
    private static IReadOnlyList<StationConnectionId> ResolveHopCandidates(
        MainRouteId mainRouteId,
        StationId fromStationId,
        StationId toStationId,
        StationConnectionDirection direction,
        IReadOnlyList<StationConnection> allStationConnections,
        IReadOnlyList<StationConnectionSegment> allSegments)
    {
        var matchingSegIds = allSegments
            .Where(s => s.MainRouteId == mainRouteId
                     && ((s.StationIdA == fromStationId && s.StationIdB == toStationId)
                      || (s.StationIdA == toStationId && s.StationIdB == fromStationId)))
            .Select(s => s.Id)
            .ToHashSet();

        if (matchingSegIds.Count == 0) return Array.Empty<StationConnectionId>();

        return allStationConnections
            .Where(sc => sc.MainRouteId == mainRouteId
                      && sc.Direction == direction
                      && sc.Segments.Any(matchingSegIds.Contains))
            .Select(sc => sc.Id)
            .ToList();
    }

    private static List<ServiceRouteSegment> BuildEffectiveSegments(
        IReadOnlyList<ServiceRouteSegment> segments, bool isUpDirection)
    {
        if (!isUpDirection) return segments.ToList();

        var result = new List<ServiceRouteSegment>(segments.Count);
        for (var i = segments.Count - 1; i >= 0; i--)
        {
            var s = segments[i];
            result.Add(new ServiceRouteSegment
            {
                MainRouteId = s.MainRouteId,
                FromStationIndex = s.ToStationIndex,
                ToStationIndex = s.FromStationIndex,
                IsUnidirectional = s.IsUnidirectional,
                PairedMainRouteId = s.PairedMainRouteId,
                PairedFromStationIndex = s.PairedFromStationIndex,
                PairedToStationIndex = s.PairedToStationIndex,
                ReversesAtBoundary = s.ReversesAtBoundary,
                SelectedStationConnectionId = s.SelectedStationConnectionId,
                PairedSelectedStationConnectionId = s.PairedSelectedStationConnectionId,
            });
        }
        return result;
    }

    /// <summary>
    /// transferStationIdにおいて、prevScでの到着EntryPointとnewScでの出発EntryPointが
    /// Track集合として重複する（＝物理的に乗換可能）かをMainRouteChecker.CanTransferで判定する。
    ///
    /// v12.29変更：EntryPointSequenceResolver.Resolve（系統(ii)）がallMainRoutesを要求する
    /// 新シグネチャに追従。フィールド名はFromStationId/ToStationId/FromEntryPointId/ToEntryPointId
    /// に戻ったため（EntryPointSequenceElementは向き解決済みの出力である旨を明示する改名）、
    /// e.ToStationId等の参照自体は変更不要。
    /// </summary>
    private static bool CanTransferAt(
        StationId transferStationId,
        StationConnectionId prevScId,
        StationConnectionId newScId,
        IReadOnlyList<StationConnection> allStationConnections,
        IReadOnlyList<StationConnectionSegment> allSegments,
        IReadOnlyList<MainRoute> allMainRoutes,
        IReadOnlyDictionary<(StationPathTrackIndexBuilder.BoundaryTerminal, RailId), StationPathId> arrivalIndex,
        IReadOnlyDictionary<(RailId, StationPathTrackIndexBuilder.BoundaryTerminal), StationPathId> departureIndex)
    {
        var prevSc = allStationConnections.FirstOrDefault(sc => sc.Id == prevScId);
        var newSc = allStationConnections.FirstOrDefault(sc => sc.Id == newScId);
        if (prevSc is null || newSc is null) return false;

        var prevSeq = EntryPointSequenceResolver.Resolve(prevSc, allSegments, allMainRoutes);
        var newSeq = EntryPointSequenceResolver.Resolve(newSc, allSegments, allMainRoutes);

        var arrivalElem = prevSeq.LastOrDefault(e => e.ToStationId == transferStationId);
        var departureElem = newSeq.FirstOrDefault(e => e.FromStationId == transferStationId);
        if (arrivalElem is null || departureElem is null) return false;

        return MainRouteChecker.CanTransfer(
            arrivalElem.ToEntryPointId, departureElem.FromEntryPointId, arrivalIndex, departureIndex);
    }
}
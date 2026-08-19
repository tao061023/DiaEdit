using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.TimeTable.Trains;

namespace DiaEditCore.Algorithm;

/// <summary>
/// ホップ（隣接駅1区間）ごとの解決結果。3種のケースを判別共用体で表現する。
/// </summary>
public abstract record HopResolution;

/// <summary>ホップのStationConnectionが確定した。</summary>
public sealed record HopResolved(TrainRunSegment Segment) : HopResolution;

/// <summary>
/// 複数候補が存在し、selectorがnullを返した（＝ユーザーがその場で未確定のまま保留した）。
/// このホップ以降、previousHopSelectionはリセットされる（次ホップも改めて①から判定する）。
/// </summary>
public sealed record HopUnresolved(StationId FromStationId, StationId ToStationId) : HopResolution;

/// <summary>
/// 直前ホップと異なるSCがselectorによって選ばれたが、乗換駅（FromStationId）での
/// Track集合重複が確認できず、物理的な転線可能性が成立しなかった。
/// 呼び出し側はこのケースをエラーとしてブロックし、ユーザーに別の乗換駅の指定を促すこと。
/// previousHopSelectionは更新されない（＝直前ホップのSCのまま次ホップの継承判定に進む）。
/// </summary>
public sealed record HopTransferBlocked(
    StationId FromStationId,
    StationId ToStationId,
    StationConnectionId AttemptedScId,
    StationConnectionId PreviousScId) : HopResolution;

/// <summary>
/// ServiceRoute＋方向（上り/下り）から、ホップ単位でStationConnectionを確定させた
/// TrainRunSegment列を導出する。新規Train追加（4.9.2節 経路③）専用。
/// 都度導出・非保存。
///
/// SyncRunSegmentsToTrainCommand（Segment単位・コピーTrain用の簡易割当）とは責務が異なる別ロジック
/// として並存させる（v12.25セッション確定）。本Resolverはホップ単位でStationConnectionを解決し、
/// 複々線・双単線区間でのユーザーの乗換選択を許容する。
///
/// ホップ候補解決（ResolveHopCandidates）について：
/// BoundaryEntryPointResolver.ResolveBoundaryStationConnectionは「SC.Segments全体が指定範囲と
/// 完全一致する」ことを要求するため、複数ホップをカバーする広域SCは1ホップ幅の問い合わせでは
/// 検出できない（BoundaryEntryPointResolverTests「2ホップ全体を1本で構成するSCは1ホップ区間の
/// 問い合わせでは一致しない」で明示されている既存挙動）。そのためホップ単位の解決には別ヘルパー
/// ResolveHopCandidatesを用いる：「そのホップに一致するStationConnectionSegment（From/To厳格一致
/// ＋MainRouteId一致）を1つでもSegmentsに含むStationConnection」を包含関係で探す。
/// 広域SCの場合、そのSC区間内の全ホップで同一候補が一貫して現れるため、後述の継承ロジックにより
/// 実質的に全ホップへ同一SCが自動的に割り当てられる（Syncと同じ最終結果になる）。
///
/// 双単線区間で同一StationConnectionSegmentを上り方向SCと下り方向SCの双方が参照するケース
/// （ScsUsedByIndexBuilderのコメント参照）について：BoundaryEntryPointResolver自体が
/// 「SegmentのFrom/Toは走行方向と厳密に一致し、方向ごとに別Segmentエンティティを用意する」
/// 前提で実装・テストされているため（BoundaryEntryPointResolverTests「方向判定」参照）、
/// 本Resolverもこの既存規約に合わせる。双単線の同一SCS共有ケースが実際にどう解決されるべきかは
/// 既存コード全体に共通する未解決の設計課題であり、本Resolver独自にスコープを広げて解決すべき
/// 問題ではないと判断し、設計書側の確認事項として別途記録する（本Resolverの実装をブロックしない）。
///
/// ホップ単位SC解決規則：
///   1. 候補が1件 → 自動確定
///   2. 候補が複数件、かつ直前ホップで確定したSCが候補集合に含まれる → それを継承
///      （同一SCの継続なので転線可能性の検証は不要）
///   3. 候補が複数件、かつ直前ホップのSCが候補集合に含まれない（先頭ホップ・前ホップがUnresolved等）
///      → selectorを呼ぶ。selectorがnullを返せばHopUnresolvedとして記録し次ホップへ進む。
///      selectorが値を返し、それが直前ホップのSCと異なる場合のみ、乗換駅（このホップの
///      FromStationId）でMainRouteChecker.CanTransferによる転線可能性を検証する。
///      成立すればHopResolved、不成立ならHopTransferBlockedとして記録し
///      （previousHopSelectionは更新しない）次ホップへ進む。
///
/// 候補0件（区間を完全カバーするStationConnectionSegmentが存在しない）は、ServiceRouteValidatorの
/// ルール2a・ルール5が事前検出すべきデータ不整合であり、正常なプロジェクト状態では発生しない
/// 想定。防御的にHopUnresolvedを積んだ上でその時点で処理を打ち切る（以降のホップは評価しない）。
/// </summary>
public static class ServiceRouteToRunSegmentsResolver
{
    /// <summary>
    /// 候補が複数件かつ直前ホップのSCが候補集合に含まれない場合にのみ呼ばれる。
    /// previousHopSelectionは「直前ホップで確定したSC」（先頭ホップ・直前ホップがUnresolvedの
    /// 場合はnull）。呼び出し側UIが同じ値を返せば継続、異なる値を返せば乗換として扱われる。
    /// </summary>
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
                // 参照整合性エラーはServiceRouteValidator側で検出済みの想定。防御的に打ち切る。
                return result;
            }

            var stationOrder = mainRoute.StationOrder;
            var stepDown = Math.Sign(seg.ToStationIndex - seg.FromStationIndex);
            if (stepDown == 0)
            {
                // FromStationIndex == ToStationIndexはServiceRouteValidatorルール1で検出済みの想定。
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
                    // 区間をカバーするSCSを含むSCが存在しない＝データ不整合（ServiceRouteValidatorの
                    // ルール2a/5で検出済みのはず）。以降のホップも同様に破綻している可能性が高いため
                    // ここで打ち切る（他のHopUnresolved/HopTransferBlockedとは異なり継続しない）。
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
                        previousHopSc = null; // 継承が途切れたため次ホップは改めて①から判定させる
                        i = nextI;
                        continue;
                    }

                    if (previousHopSc is { } prevSc && selected.Value != prevSc)
                    {
                        if (!CanTransferAt(fromStationId, prevSc, selected.Value,
                                allStationConnections, allSegments, arrivalIndex, departureIndex))
                        {
                            result.Add(new HopTransferBlocked(fromStationId, toStationId, selected.Value, prevSc));
                            // previousHopScは更新しない（次ホップは引き続きprevScの継承を試みる）
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
    /// StationConnectionSegment（MainRouteId一致・From/To厳格一致）を1つでもSegmentsに含む
    /// StationConnection（MainRouteId一致・Direction一致）を、包含関係で列挙する。
    ///
    /// BoundaryEntryPointResolver.ResolveBoundaryStationConnectionと異なり「SC.Segments全体が
    /// 指定範囲と完全一致する」ことを要求しない点が肝要（複数ホップをカバーする広域SCも、
    /// 該当SCSを含んでさえいれば候補として拾う）。
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
                     && s.FromStationId == fromStationId
                     && s.ToStationId == toStationId)
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

    /// <summary>
    /// isUpDirection=trueの場合、ServiceRoute.Segments列全体を「物理的な逆走経路」に変換する：
    /// Segment列の順序を反転し、各Segment内のFromStationIndex/ToStationIndexをswapする
    /// （MainRouteIdが変わる以上、Segment境界を跨いだ継承は元々起こりえないため、
    /// 境界を特別扱いする必要はない。v12.25セッション確定）。
    /// Paired系フィールドはRunSegments解決に使わないため素通しする。
    /// </summary>
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
    /// 対応するEntryPointSequenceElementが見つからない場合はfalse（転線不可）として扱う。
    /// </summary>
    private static bool CanTransferAt(
        StationId transferStationId,
        StationConnectionId prevScId,
        StationConnectionId newScId,
        IReadOnlyList<StationConnection> allStationConnections,
        IReadOnlyList<StationConnectionSegment> allSegments,
        IReadOnlyDictionary<(StationPathTrackIndexBuilder.BoundaryTerminal, RailId), StationPathId> arrivalIndex,
        IReadOnlyDictionary<(RailId, StationPathTrackIndexBuilder.BoundaryTerminal), StationPathId> departureIndex)
    {
        var prevSc = allStationConnections.FirstOrDefault(sc => sc.Id == prevScId);
        var newSc = allStationConnections.FirstOrDefault(sc => sc.Id == newScId);
        if (prevSc is null || newSc is null) return false;

        var prevSeq = EntryPointSequenceResolver.Resolve(prevSc, allSegments);
        var newSeq = EntryPointSequenceResolver.Resolve(newSc, allSegments);

        var arrivalElem = prevSeq.LastOrDefault(e => e.ToStationId == transferStationId);
        var departureElem = newSeq.FirstOrDefault(e => e.FromStationId == transferStationId);
        if (arrivalElem is null || departureElem is null) return false;

        return MainRouteChecker.CanTransfer(
            arrivalElem.ToEntryPointId, departureElem.FromEntryPointId, arrivalIndex, departureIndex);
    }
}
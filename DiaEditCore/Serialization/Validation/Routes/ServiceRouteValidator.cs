using DiaEditCore.Algorithm;

using DiaEditCore.Model;
using DiaEditCore.Model.Routes;

namespace DiaEditCore.Serialization.Validation.Routes;
 
public sealed class ServiceRouteValidator : IValidator<ServiceRoute>
{
    private readonly DisplayNameValidator _displayNameValidator = new();

    // Direction判定はv12.24でBoundaryEntryPointResolver.ResolveBoundaryStationConnection内へ集約し、
    // 本Validator内での直接算出（旧DirectionOf）は不要になった。
 
    // fromIndex/toIndexで指定される区間（MainRoute.StationOrder上の生インデックス範囲）に
    // 実際に含まれるSCSだけを抽出する。SCS自体のFromStationId/ToStationIdをStationOrder上の
    // インデックスに逆引きして判定するため、Up/Down方向でのsc.Segments内の並び順（reverse有無）
    // を意識する必要がない。
    private static List<StationConnectionSegmentId> SegmentsInRange(
        StationConnection sc, MainRoute mainRoute, int fromIndex, int toIndex, ValidationContext context)
    {
        var lo = Math.Min(fromIndex, toIndex);
        var hi = Math.Max(fromIndex, toIndex);
        var result = new List<StationConnectionSegmentId>();
 
        foreach (var segId in sc.Segments)
        {
            var seg = context.StationConnectionSegments.FirstOrDefault(s => s.Id == segId);
            if (seg is null)
                continue;
 
            var fIdx = mainRoute.StationOrder.IndexOf(seg.StationIdA);
            var tIdx = mainRoute.StationOrder.IndexOf(seg.StationIdB);
            if (fIdx < 0 || tIdx < 0)
                continue;
 
            var segLo = Math.Min(fIdx, tIdx);
            var segHi = Math.Max(fIdx, tIdx);
 
            // segがクエリ区間[lo, hi]に完全に含まれる場合のみ対象とする
            if (segLo >= lo && segHi <= hi)
                result.Add(segId);
        }
 
        return result;
    }
 
    public IReadOnlyList<IValidationIssue> Validate(ServiceRoute target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>(
            _displayNameValidator.Validate(target.Name, context));
 
        for (var i = 0; i < target.Segments.Count; i++)
        {
            var seg = target.Segments[i];
 
            // 1. fromStationIndex ≠ toStationIndex
            if (seg.FromStationIndex == seg.ToStationIndex)
                issues.Add(new ValidationIssue($"ServiceRoute({target.Id}).Segments[{i}]: FromStationIndexとToStationIndexが同一"));
 
            if (seg.IsUnidirectional)
            {
                var mainRouteForSeg = context.MainRoutes.FirstOrDefault(m => m.Id == seg.MainRouteId);
 
                // 2a. mainRouteId側の、fromIndex→toIndex区間を完全にカバーするStationConnectionが
                //     実在すること（v12.24：区間完全一致を要求するBoundaryEntryPointResolverに統一。
                //     Direction一致のみの緩い判定だと、後続のValidateBoundaryConnectivity側の
                //     区間完全一致判定と基準がずれ、矛盾したエラーが出る恐れがあったため）
                var mainCandidateIds = BoundaryEntryPointResolver.ResolveBoundaryStationConnection(
                    seg.MainRouteId, seg.FromStationIndex, seg.ToStationIndex,
                    context.MainRoutes, context.StationConnections, context.StationConnectionSegments);
                var mainSc = mainCandidateIds.Count > 0
                    ? context.StationConnections.FirstOrDefault(sc => sc.Id == mainCandidateIds[0])
                    : null;
                if (mainSc is null)
                    issues.Add(new ValidationIssue($"ServiceRoute({target.Id}).Segments[{i}]: MainRouteId({seg.MainRouteId})の区間{seg.FromStationIndex}→{seg.ToStationIndex}を完全にカバーするStationConnectionが存在しない"));
 
                StationConnection? pairedSc = null;
                MainRoute? pairedRoute = null;
                int? pFrom = null, pTo = null;
 
                if (seg.PairedMainRouteId is { } pairedRouteId)
                {
                    pairedRoute = context.MainRoutes.FirstOrDefault(m => m.Id == pairedRouteId);
                    if (pairedRoute is null)
                    {
                        issues.Add(new ValidationIssue($"ServiceRoute({target.Id}).Segments[{i}]: PairedMainRouteId({pairedRouteId})が存在しない"));
                    }
                    else if (seg.PairedFromStationIndex is not { } pf || seg.PairedToStationIndex is not { } pt)
                    {
                        issues.Add(new ValidationIssue($"ServiceRoute({target.Id}).Segments[{i}]: PairedMainRouteId設定時はPairedFromStationIndex/PairedToStationIndexも必須"));
                    }
                    else if (pf < 0 || pf >= pairedRoute.StationOrder.Count || pt < 0 || pt >= pairedRoute.StationOrder.Count)
                    {
                        issues.Add(new ValidationIssue($"ServiceRoute({target.Id}).Segments[{i}]: Paired側のStationIndexがStationOrderの範囲外"));
                    }
                    else
                    {
                        pFrom = pf;
                        pTo = pt;
                        // 2a同様、v12.24で区間完全一致判定に統一
                        var pairedCandidateIds = BoundaryEntryPointResolver.ResolveBoundaryStationConnection(
                            pairedRouteId, pf, pt,
                            context.MainRoutes, context.StationConnections, context.StationConnectionSegments);
                        pairedSc = pairedCandidateIds.Count > 0
                            ? context.StationConnections.FirstOrDefault(sc => sc.Id == pairedCandidateIds[0])
                            : null;
                        if (pairedSc is null)
                            issues.Add(new ValidationIssue($"ServiceRoute({target.Id}).Segments[{i}]: PairedMainRouteId({pairedRouteId})の区間{pf}→{pt}を完全にカバーするStationConnectionが存在しない"));
                    }
 
                    // 2c. SCS重複禁止（fromIndex〜toIndexの区間に実際に含まれるSCSのみを比較対象とする）
                    if (mainSc is not null && pairedSc is not null && mainRouteForSeg is not null && pairedRoute is not null
                        && pFrom is { } pFromIdx && pTo is { } pToIdx)
                    {
                        var mainRangeSegs = SegmentsInRange(mainSc, mainRouteForSeg, seg.FromStationIndex, seg.ToStationIndex, context);
                        var pairedRangeSegs = SegmentsInRange(pairedSc, pairedRoute, pFromIdx, pToIdx, context);
                        var overlap = mainRangeSegs.Intersect(pairedRangeSegs).ToList();
                        if (overlap.Count > 0)
                            issues.Add(new ValidationIssue($"ServiceRoute({target.Id}).Segments[{i}]: mainRouteId側とpairedMainRouteId側でSCSが重複している（{string.Join(",", overlap)}）"));
                    }
                }
            }
            else
            {
                // 3. isUnidirectional = false の場合、pairedMainRouteId等は使用しない
                if (seg.PairedMainRouteId is not null || seg.PairedFromStationIndex is not null || seg.PairedToStationIndex is not null)
                    issues.Add(new ValidationIssue($"ServiceRoute({target.Id}).Segments[{i}]: IsUnidirectional=falseなのにPaired系フィールドが設定されている"));
 
                // 3. fromIndex→toIndexの両方向にStationConnectionが実在すること
                var hasDown = context.StationConnections.Any(sc => sc.MainRouteId == seg.MainRouteId && sc.Direction == StationConnectionDirection.Down);
                var hasUp = context.StationConnections.Any(sc => sc.MainRouteId == seg.MainRouteId && sc.Direction == StationConnectionDirection.Up);
                if (!hasDown || !hasUp)
                    issues.Add(new ValidationIssue($"ServiceRoute({target.Id}).Segments[{i}]: IsUnidirectional=falseだがMainRouteId({seg.MainRouteId})にUp/Down両方向のStationConnectionが揃っていない"));
            }
 
            // 4. 隣接Segment間の境界駅一致
            if (i > 0)
            {
                var prev = target.Segments[i - 1];
                var prevRoute = context.MainRoutes.FirstOrDefault(m => m.Id == prev.MainRouteId);
                var currRoute = context.MainRoutes.FirstOrDefault(m => m.Id == seg.MainRouteId);
 
                if (prevRoute is null || currRoute is null)
                {
                    issues.Add(new ValidationIssue($"ServiceRoute({target.Id}).Segments[{i}]: 参照MainRouteが存在しない"));
                }
                else if (prev.ToStationIndex < 0 || prev.ToStationIndex >= prevRoute.StationOrder.Count ||
                         seg.FromStationIndex < 0 || seg.FromStationIndex >= currRoute.StationOrder.Count)
                {
                    issues.Add(new ValidationIssue($"ServiceRoute({target.Id}).Segments[{i}]: StationIndexがStationOrderの範囲外"));
                }
                else
                {
                    var boundaryStationOfPrev = prevRoute.StationOrder[prev.ToStationIndex];
                    var boundaryStationOfCurr = currRoute.StationOrder[seg.FromStationIndex];
                    if (boundaryStationOfPrev != boundaryStationOfCurr)
                        issues.Add(new ValidationIssue(
                            $"ServiceRoute({target.Id}).Segments[{i}]: 境界駅が不一致（前Segment末尾={boundaryStationOfPrev}, 当Segment先頭={boundaryStationOfCurr}）"));
                }
            }
 
            // 5. 境界駅の整合性条件（ServiceRoutePathResolver / EntryPointSequenceベースの検証）は
            //    6.2節ServiceRoutePathResolver実装時に対応予定（意図的に未実装。8.2節参照）
        }
        // 5. 境界駅の整合性条件（ServiceRoute整合性）：mainRouteId側
        ValidateBoundaryConnectivity(
            target, context, issues,
            side: "主方向",
            mainRouteIdOf: s => s.MainRouteId,
            fromIndexOf: s => s.FromStationIndex,
            toIndexOf: s => s.ToStationIndex,
            selectedScIdOf: s => s.SelectedStationConnectionId);

        // 5. 境界駅の整合性条件（ServiceRoute整合性）：pairedMainRouteId側
        //    全segmentがpairedの場合のみ検証する（IsPaired()が1つでもfalseならpairedSeq自体を生成しない、という
        //    ServiceRoutePathResolverと同じ前提を踏襲）
        if (target.Segments.Count > 0 && target.Segments.All(s => s.IsPaired()))
        {
            ValidateBoundaryConnectivity(
                target, context, issues,
                side: "Paired方向",
                mainRouteIdOf: s => s.PairedMainRouteId!.Value,
                fromIndexOf: s => s.PairedFromStationIndex!.Value,
                toIndexOf: s => s.PairedToStationIndex!.Value,
                selectedScIdOf: s => s.PairedSelectedStationConnectionId);
        }

        var unidirectionalSegs = target.Segments.Where(s => s.IsUnidirectional).ToList();
        if (unidirectionalSegs.Count > 0)
        {
            var pairedCount = unidirectionalSegs.Count(s => s.IsPaired());
            if (pairedCount > 0 && pairedCount < unidirectionalSegs.Count)
            {
                issues.Add(new ValidationIssue(
                    $"ServiceRoute({target.Id}): IsUnidirectional=trueのSegmentでPaired設定が一部のみ存在する（{pairedCount}/{unidirectionalSegs.Count}件）",
                    ValidationSeverity.Warning));
            }
        }

        return issues;
    }


    /// <summary>
    /// ルール5：境界駅の整合性条件。mainRouteId側・pairedMainRouteId側で共通のロジック。
    /// 各segmentについて、fromIndex→toIndex区間を完全にカバーするStationConnectionを
    /// BoundaryEntryPointResolver.ResolveBoundaryStationConnectionで解決し（複数候補なら
    /// SelectedStationConnectionId必須）、EntryPointSequenceResolverの結果をそのまま結合、
    /// MainRouteCheckerで境界ごとのTrack集合重複を検証する。 <br/>
    /// v12.24：候補解決を「MainRouteId＋Direction一致（MainRoute全体走破前提）」から
    /// 「fromIndex→toIndex区間の完全一致」へ変更（§9.1項目9のDRY抽出議論で判明した設計修正）。
    /// これにより、区間が保証済みとなるためStationOrder上の位置でのスライス処理（ToPosition等）が
    /// 不要になった。SyncRunSegmentsToTrainCommand.ResolveSegmentStationConnectionIdと
    /// 同じ候補解決ロジックを共有する。
    /// </summary>
    private static void ValidateBoundaryConnectivity(
        ServiceRoute target,
        ValidationContext context,
        List<IValidationIssue> issues,
        string side,
        Func<ServiceRouteSegment, MainRouteId> mainRouteIdOf,
        Func<ServiceRouteSegment, int> fromIndexOf,
        Func<ServiceRouteSegment, int> toIndexOf,
        Func<ServiceRouteSegment, StationConnectionId?> selectedScIdOf)
    {
        var combinedEps = new List<EntryPointId>();

        for (var i = 0; i < target.Segments.Count; i++)
        {
            var s = target.Segments[i];
            var mainRouteId = mainRouteIdOf(s);
            var fromIdx = fromIndexOf(s);
            var toIdx = toIndexOf(s);

            var route = context.MainRoutes.FirstOrDefault(m => m.Id == mainRouteId);
            if (route is null)
                return; // 参照整合性エラーは他ルールで既に報告済み。ここでは検証不能として打ち切る

            var candidateIds = BoundaryEntryPointResolver.ResolveBoundaryStationConnection(
                mainRouteId, fromIdx, toIdx,
                context.MainRoutes, context.StationConnections, context.StationConnectionSegments);

            StationConnection selectedSc;
            if (candidateIds.Count == 0)
            {
                return; // ルール2で既にエラー報告済み。重複報告を避けるため打ち切る
            }
            else if (candidateIds.Count == 1)
            {
                selectedSc = context.StationConnections.First(sc => sc.Id == candidateIds[0]);
            }
            else
            {
                var selId = selectedScIdOf(s);
                if (selId is null)
                {
                    issues.Add(new ValidationIssue(
                        $"ServiceRoute({target.Id}).Segments[{i}]（{side}）: StationConnection候補が複数存在するため選択指定が必須"));
                    return;
                }

                if (!candidateIds.Contains(selId.Value))
                {
                    issues.Add(new ValidationIssue(
                        $"ServiceRoute({target.Id}).Segments[{i}]（{side}）: 選択されたStationConnection({selId.Value})が候補集合に含まれない"));
                    return;
                }
                selectedSc = context.StationConnections.First(sc => sc.Id == selId.Value);
            }

            // candidateIdsはfromIdx→toIdx区間を完全にカバーすることが既に保証されているため、
            // ToPositionによるスライスは不要。Resolveの結果をそのまま結合すればよい。
            var fullSeq = EntryPointSequenceResolver.Resolve(selectedSc, context.StationConnectionSegments, context.MainRoutes);
            foreach (var element in fullSeq)
            {
                combinedEps.Add(element.FromEntryPointId);
                combinedEps.Add(element.ToEntryPointId);
            }
        }

        if (combinedEps.Count < 2)
            return;

        var stationOrder = ServiceRouteStationOrderResolver.ResolveServiceRouteStationOrder(target, context.MainRoutes);
        var isLoop = stationOrder.Count >= 2 && stationOrder[0] == stationOrder[^1];

        var (arrivalIndex, departureIndex) = StationPathTrackIndexBuilder.BuildWithBoundaryTerminals(
            context.StationPaths, context.Rails);

        var boundaryResults = MainRouteChecker.CheckBoundaryConnectivity(
            combinedEps, isLoop, arrivalIndex, departureIndex);

        foreach (var r in boundaryResults.Where(r => !r.IsSatisfied))
        {
            issues.Add(new ValidationIssue(
                $"ServiceRoute({target.Id})（{side}）: 境界{r.BoundaryIndex}でTrack集合が重複するArrival/Departure StationPathが存在しない（境界駅の整合性条件違反）"));
        }
    }
}
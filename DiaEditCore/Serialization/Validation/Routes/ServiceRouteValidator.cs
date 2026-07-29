using DiaEditCore.Algorithm;

using DiaEditCore.Model;
using DiaEditCore.Model.Routes;

namespace DiaEditCore.Serialization.Validation.Routes;
 
public sealed class ServiceRouteValidator : IValidator<ServiceRoute>
{
    private static StationConnectionDirection DirectionOf(int fromIndex, int toIndex) =>
        fromIndex < toIndex ? StationConnectionDirection.Down : StationConnectionDirection.Up;
 
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
 
            var fIdx = mainRoute.StationOrder.IndexOf(seg.FromStationId);
            var tIdx = mainRoute.StationOrder.IndexOf(seg.ToStationId);
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
        var issues = new List<IValidationIssue>();
 
        for (var i = 0; i < target.Segments.Count; i++)
        {
            var seg = target.Segments[i];
 
            // 1. fromStationIndex ≠ toStationIndex
            if (seg.FromStationIndex == seg.ToStationIndex)
                issues.Add(new ValidationIssue($"ServiceRoute({target.Id}).Segments[{i}]: FromStationIndexとToStationIndexが同一"));
 
            if (seg.IsUnidirectional)
            {
                var mainRouteForSeg = context.MainRoutes.FirstOrDefault(m => m.Id == seg.MainRouteId);
 
                // 2a. mainRouteId側の対応StationConnectionが実在すること
                var expectedDir = DirectionOf(seg.FromStationIndex, seg.ToStationIndex);
                var mainSc = context.StationConnections.FirstOrDefault(sc => sc.MainRouteId == seg.MainRouteId && sc.Direction == expectedDir);
                if (mainSc is null)
                    issues.Add(new ValidationIssue($"ServiceRoute({target.Id}).Segments[{i}]: MainRouteId({seg.MainRouteId})のDirection={expectedDir}に対応するStationConnectionが存在しない"));
 
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
                        var pairedExpectedDir = DirectionOf(pf, pt);
                        pairedSc = context.StationConnections.FirstOrDefault(sc => sc.MainRouteId == pairedRouteId && sc.Direction == pairedExpectedDir);
                        if (pairedSc is null)
                            issues.Add(new ValidationIssue($"ServiceRoute({target.Id}).Segments[{i}]: PairedMainRouteId({pairedRouteId})のDirection={pairedExpectedDir}に対応するStationConnectionが存在しない"));
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

        // ↓ここに③のコードを挿入
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
    /// 各segmentについて対応するStationConnectionを解決し（複数候補ならSelectedStationConnectionId必須）、
    /// EntryPointSequenceResolverの結果をStationOrder上の位置でスライスして結合、
    /// MainRouteCheckerで境界ごとのTrack集合重複を検証する。
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

            var expectedDir = DirectionOf(fromIdx, toIdx);
            var candidates = context.StationConnections
                .Where(sc => sc.MainRouteId == mainRouteId && sc.Direction == expectedDir)
                .ToList();

            StationConnection selectedSc;
            if (candidates.Count == 0)
            {
                return; // ルール2で既にエラー報告済み。重複報告を避けるため打ち切る
            }
            else if (candidates.Count == 1)
            {
                selectedSc = candidates[0];
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

                var found = candidates.FirstOrDefault(sc => sc.Id == selId.Value);
                if (found is null)
                {
                    issues.Add(new ValidationIssue(
                        $"ServiceRoute({target.Id}).Segments[{i}]（{side}）: 選択されたStationConnection({selId.Value})が候補集合に含まれない"));
                    return;
                }
                selectedSc = found;
            }

            var fullSeq = EntryPointSequenceResolver.Resolve(selectedSc, context.StationConnectionSegments);
            var stationCount = route.StationOrder.Count;

            int ToPosition(int rawIndex) =>
                selectedSc.Direction == StationConnectionDirection.Down ? rawIndex : stationCount - 1 - rawIndex;

            var posFrom = ToPosition(fromIdx);
            var posTo = ToPosition(toIdx);
            var lo = Math.Min(posFrom, posTo);
            var hi = Math.Max(posFrom, posTo);

            if (lo < 0 || hi > fullSeq.Count)
            {
                return; // 範囲外は他ルール（StationIndex範囲チェック等）で既に報告済みのはず
            }

            for (var h = lo; h < hi; h++)
            {
                combinedEps.Add(fullSeq[h].FromEntryPointId);
                combinedEps.Add(fullSeq[h].ToEntryPointId);
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
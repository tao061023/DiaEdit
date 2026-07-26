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
 
        return issues;
    }
}
using DiaEditCore.Model;

namespace DiaEditCore.Serialization.Validation;

public sealed class ServiceRouteValidator : IValidator<ServiceRoute>
{
    private static StationConnectionDirection DirectionOf(int fromIndex, int toIndex) =>
        fromIndex < toIndex ? StationConnectionDirection.Down : StationConnectionDirection.Up;

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
                // 2a. mainRouteId側の対応StationConnectionが実在すること
                var expectedDir = DirectionOf(seg.FromStationIndex, seg.ToStationIndex);
                var mainSc = context.StationConnections.FirstOrDefault(sc => sc.MainRouteId == seg.MainRouteId && sc.Direction == expectedDir);
                if (mainSc is null)
                    issues.Add(new ValidationIssue($"ServiceRoute({target.Id}).Segments[{i}]: MainRouteId({seg.MainRouteId})のDirection={expectedDir}に対応するStationConnectionが存在しない"));

                StationConnection? pairedSc = null;
                if (seg.PairedMainRouteId is { } pairedRouteId)
                {
                    var pairedRoute = context.MainRoutes.FirstOrDefault(m => m.Id == pairedRouteId);
                    if (pairedRoute is null)
                    {
                        issues.Add(new ValidationIssue($"ServiceRoute({target.Id}).Segments[{i}]: PairedMainRouteId({pairedRouteId})が存在しない"));
                    }
                    else if (seg.PairedFromStationIndex is not { } pFrom || seg.PairedToStationIndex is not { } pTo)
                    {
                        issues.Add(new ValidationIssue($"ServiceRoute({target.Id}).Segments[{i}]: PairedMainRouteId設定時はPairedFromStationIndex/PairedToStationIndexも必須"));
                    }
                    else if (pFrom < 0 || pFrom >= pairedRoute.StationOrder.Count || pTo < 0 || pTo >= pairedRoute.StationOrder.Count)
                    {
                        issues.Add(new ValidationIssue($"ServiceRoute({target.Id}).Segments[{i}]: Paired側のStationIndexがStationOrderの範囲外"));
                    }
                    else
                    {
                        var pairedExpectedDir = DirectionOf(pFrom, pTo);
                        pairedSc = context.StationConnections.FirstOrDefault(sc => sc.MainRouteId == pairedRouteId && sc.Direction == pairedExpectedDir);
                        if (pairedSc is null)
                            issues.Add(new ValidationIssue($"ServiceRoute({target.Id}).Segments[{i}]: PairedMainRouteId({pairedRouteId})のDirection={pairedExpectedDir}に対応するStationConnectionが存在しない"));
                    }

                    // 2c. SCS重複禁止
                    if (mainSc is not null && pairedSc is not null)
                    {
                        var overlap = mainSc.Segments.Intersect(pairedSc.Segments).ToList();
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
        }

        return issues;
    }
}
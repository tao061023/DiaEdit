using DiaEditCore.Algorithm;

using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.Routes;

namespace DiaEditCore.Serialization.Validation.Routes;

public sealed class StationConnectionValidator : IValidator<StationConnection>
{
    public IReadOnlyList<IValidationIssue> Validate(StationConnection target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();

        // --- MainRouteId整合性検証 ---
        var resolvedSegs = new List<StationConnectionSegment>();
        foreach (var segId in target.Segments)
        {
            var seg = context.StationConnectionSegments.FirstOrDefault(s => s.Id == segId);
            if (seg is null)
            {
                issues.Add(new ValidationIssue($"StationConnection({target.Id}): Segments に存在しないSCSId({segId})が含まれる"));
                continue;
            }
            resolvedSegs.Add(seg);

            if (seg.MainRouteId != target.MainRouteId)
            {
                issues.Add(new ValidationIssue(
                    $"StationConnection({target.Id}): Segments[{segId}]（SCS {seg.Id}）のMainRouteId({seg.MainRouteId})が" +
                    $"StationConnection自身のMainRouteId({target.MainRouteId})と一致しない"));
            }
        }

        // --- EntryPoint.type整合性検証 ---
        // 向き解決済み（EntryPointSequenceResolver.Resolve、系統(ii)）のFrom/ToEntryPointIdを
        // 使って、元のFrom=出発固定・To=到着固定という非対称チェックをそのまま適用する。
        // EntryPointIdA/Bは無向ペアのため、target.Direction次第でどちらが出発側になるかが
        // 変わる。Resolve前のA/Bへ直接チェックすると双単線の上り側SCで常に誤判定するため、
        // 必ずResolve後の値で検証する。
        var resolvedSeqForEpCheck = EntryPointSequenceResolver.Resolve(target, context.StationConnectionSegments, context.MainRoutes);
        foreach (var elem in resolvedSeqForEpCheck)
        {
            var departureEp = context.EntryPoints.FirstOrDefault(e => e.Id == elem.FromEntryPointId);
            var arrivalEp = context.EntryPoints.FirstOrDefault(e => e.Id == elem.ToEntryPointId);

            if (departureEp is null || arrivalEp is null)
            {
                issues.Add(new ValidationIssue($"StationConnection({target.Id}): 向き解決済みEntryPoint（{elem.FromEntryPointId}または{elem.ToEntryPointId}）が存在しない"));
                continue;
            }

            if (departureEp.Type is not (EntryPointType.Departure or EntryPointType.Both))
                issues.Add(new ValidationIssue($"StationConnection({target.Id}): 出発側EP({departureEp.Id})のtypeがDeparture/Bothではない"));

            if (arrivalEp.Type is not (EntryPointType.Arrival or EntryPointType.Both))
                issues.Add(new ValidationIssue($"StationConnection({target.Id}): 到着側EP({arrivalEp.Id})のtypeがArrival/Bothではない"));
        }

        // --- StationOrderとの順序整合性検証 ---
        var mainRoute = context.MainRoutes.FirstOrDefault(m => m.Id == target.MainRouteId);
        if (mainRoute is null)
        {
            issues.Add(new ValidationIssue($"StationConnection({target.Id}): MainRouteId({target.MainRouteId})が存在しない"));
            return issues;
        }

        var orderedStations = target.Direction == StationConnectionDirection.Down
            ? mainRoute.StationOrder
            : mainRoute.StationOrder.AsEnumerable().Reverse().ToList();

        if (resolvedSegs.Count != orderedStations.Count - 1)
        {
            issues.Add(new ValidationIssue(
                $"StationConnection({target.Id}): Segments数({resolvedSegs.Count})がMainRouteのStationOrder({orderedStations.Count}駅)から期待される数({orderedStations.Count - 1})と一致しない"));
        }
        else
        {
            for (var i = 0; i < resolvedSegs.Count; i++)
            {
                var expectedFrom = orderedStations[i];
                var expectedTo = orderedStations[i + 1];
                var seg = resolvedSegs[i];

                // A/Bは無向ペアのため、期待される駅ペアと一致するかは順序を問わず判定する
                // （双単線区間で同一SCSを上り・下り双方のSCが共有するケースを正しく許可するため。
                // これが今回のリネーム全体の主眼）。
                var matches =
                    (seg.StationIdA == expectedFrom && seg.StationIdB == expectedTo) ||
                    (seg.StationIdA == expectedTo && seg.StationIdB == expectedFrom);

                if (!matches)
                {
                    issues.Add(new ValidationIssue(
                        $"StationConnection({target.Id}): Segments[{i}]（SCS {seg.Id}）は駅({seg.StationIdA}⇔{seg.StationIdB})だが、" +
                        $"StationOrder上のDirection={target.Direction}での期待値は({expectedFrom}⇔{expectedTo})"));
                }
            }
        }

        // --- MainRoute整合性検証（5.7節・8.2節項目5関連） ---
        // v12.29：epSequenceの手動構築（旧: seg.FromEntryPointId/ToEntryPointIdを直接連結）を廃止し、
        // 直前のEntryPoint.type検証で既に計算済みのresolvedSeqForEpCheck（EntryPointSequenceResolver.Resolve、
        // 系統(ii)・向き解決込み）をそのまま再利用する（同一計算の二重実行を避ける）。
        if (resolvedSeqForEpCheck.Count > 0)
        {
            var epSequence = new List<EntryPointId>(resolvedSeqForEpCheck.Count * 2);
            foreach (var elem in resolvedSeqForEpCheck)
            {
                epSequence.Add(elem.FromEntryPointId);
                epSequence.Add(elem.ToEntryPointId);
            }

            var (arrivalIndex, departureIndex) = StationPathTrackIndexBuilder.BuildWithBoundaryTerminals(
                context.StationPaths, context.Rails);

            var boundaryResults = MainRouteChecker.CheckBoundaryConnectivity(
                epSequence, mainRoute.IsLoop, arrivalIndex, departureIndex);

            foreach (var r in boundaryResults.Where(r => !r.IsSatisfied))
            {
                issues.Add(new ValidationIssue(
                    $"StationConnection({target.Id}): 境界{r.BoundaryIndex}でTrack集合が重複するArrival/Departure StationPathが存在しない（MainRoute整合性違反）"));
            }
        }
        else if (resolvedSegs.Count > 0)
        {
            // resolvedSegsは存在するのにresolvedSeqForEpCheck（向き解決済み）が空＝全SegmentがMainRouteId不一致
            // またはStationOrder非隣接で防御的にスキップされた、という重大なデータ不整合。
            // 上記のMainRouteId整合性検証・StationOrder順序整合性検証で個別のissueは既に
            // 報告済みのはずだが、念のためここでも検証不能だった旨を報告する。
            issues.Add(new ValidationIssue(
                $"StationConnection({target.Id}): 向き解決可能なSegmentが1件も無く、MainRoute整合性検証を実施できなかった"));
        }

        return issues;
    }
}
using DiaEditCore.Model;

namespace DiaEditCore.Serialization.Validation;

public sealed class StationConnectionValidator : IValidator<StationConnection>
{
    public IReadOnlyList<IValidationIssue> Validate(StationConnection target, ValidationContext context)
    {
        var issues = new List<IValidationIssue>();

        // --- EntryPoint.type整合性検証 ---
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

            var departureEp = context.EntryPoints.FirstOrDefault(e => e.Id == seg.FromEntryPointId);
            var arrivalEp = context.EntryPoints.FirstOrDefault(e => e.Id == seg.ToEntryPointId);

            if (departureEp is null || arrivalEp is null)
            {
                issues.Add(new ValidationIssue($"StationConnectionSegment({seg.Id}): 参照先EntryPointが存在しない"));
                continue;
            }

            if (departureEp.Type is not (EntryPointType.Departure or EntryPointType.Both))
                issues.Add(new ValidationIssue($"StationConnectionSegment({seg.Id}): 出発側EP({departureEp.Id})のtypeがDeparture/Bothではない"));

            if (arrivalEp.Type is not (EntryPointType.Arrival or EntryPointType.Both))
                issues.Add(new ValidationIssue($"StationConnectionSegment({seg.Id}): 到着側EP({arrivalEp.Id})のtypeがArrival/Bothではない"));
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

                if (seg.FromStationId != expectedFrom || seg.ToStationId != expectedTo)
                {
                    issues.Add(new ValidationIssue(
                        $"StationConnection({target.Id}): Segments[{i}]（SCS {seg.Id}）は駅({seg.FromStationId}→{seg.ToStationId})だが、" +
                        $"StationOrder上のDirection={target.Direction}での期待値は({expectedFrom}→{expectedTo})"));
                }
            }
        }

        return issues;
    }
}
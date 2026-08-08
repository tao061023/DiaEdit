using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.TimeTable.Trains;

namespace DiaEditCore.Algorithm;

public enum StopPatternMark { Stop, Pass, OutOfRange } // ●, レ, 空白

public sealed record StopPatternElement(
    StationId StationId,
    DisplayName StationDisplayName,
    StopPatternMark Mark);

/// <summary>
/// resolveServiceRouteStationOrderの結果と、対象TrainのstopTimes・TrainRunSegmentを
/// 突き合わせ、駅名付きの停車パターン列（基準列車選択UI用）を導出する。都度導出・非保存。
/// </summary>
public static class StopPatternResolver
{
    public static IReadOnlyList<StopPatternElement> ResolveStopPattern(
        Train train,
        IReadOnlyList<ServiceRoute> allServiceRoutes,
        IReadOnlyList<MainRoute> allMainRoutes,
        IReadOnlyList<Station> allStations)
    {
        var serviceRoute = allServiceRoutes.FirstOrDefault(sr => sr.Id == train.ServiceRouteId);
        if (serviceRoute is null) return Array.Empty<StopPatternElement>();

        var stationOrder = ServiceRouteStationOrderResolver.ResolveServiceRouteStationMainRoutes(
            serviceRoute, allMainRoutes);

        // Trainが実際に走行する駅集合（RunSegmentsのFrom/Toの和集合）
        var visitedStations = new HashSet<StationId>();
        foreach (var runSeg in train.RunSegments)
        {
            visitedStations.Add(runSeg.FromStationId);
            visitedStations.Add(runSeg.ToStationId);
        }

        var result = new List<StopPatternElement>(stationOrder.Count);
        foreach (var (stationId, mainRouteId) in stationOrder)
        {
            var mark = DetermineMark(train, stationId, visitedStations);
            var displayName = ResolveStationDisplayName(stationId, mainRouteId, allMainRoutes, allStations);
            if (displayName is null) continue; // 参照整合性エラーは保存時検証で別途検出する想定

            result.Add(new StopPatternElement(stationId, displayName, mark));
        }

        return result;
    }

    private static StopPatternMark DetermineMark(Train train, StationId stationId, HashSet<StationId> visitedStations)
    {
        if (!visitedStations.Contains(stationId)) return StopPatternMark.OutOfRange;

        // 同一Train内で同一駅を複数回訪問することはない前提のため、
        // StationId一致のみでStopTimeを一意に特定できる
        var stopTime = train.StopTimes
            .Where(kv => kv.Key.StationId == stationId)
            .Select(kv => kv.Value)
            .FirstOrDefault();

        return stopTime is { IsStop: true } ? StopPatternMark.Stop : StopPatternMark.Pass;
    }

    private static DisplayName? ResolveStationDisplayName(
        StationId stationId,
        MainRouteId mainRouteId,
        IReadOnlyList<MainRoute> allMainRoutes,
        IReadOnlyList<Station> allStations)
    {
        var mainRoute = allMainRoutes.FirstOrDefault(mr => mr.Id == mainRouteId);
        if (mainRoute is not null && mainRoute.StationDisplayNameOverrides.TryGetValue(stationId, out var overrideName))
        {
            return overrideName;
        }

        var station = allStations.FirstOrDefault(s => s.Id == stationId);
        return station?.DisplayName;
    }
}

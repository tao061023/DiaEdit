using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.TimeTable.Trains;
using DiaEditCore.Algorithm.CacheBuilder;

namespace DiaEditCore.Algorithm.Conflicts;

/// <summary>
/// Track（番線）用途のConflictChecker（6.5節）。
/// v12.29対応：EntryPointSequenceCache.Buildの系統(ii)化に伴い、allMainRoutesを新規に受け取る。
/// （クラス冒頭の詳細コメントは既存版から変更なし。シグネチャ・呼び出し箇所のみ更新）
/// </summary>
public static class TrackOccupancyProvider
{
    public static Dictionary<RailId, List<ConflictChecker.Occupancy>> BuildOccupancy(
        IReadOnlyList<Train> trains,
        IReadOnlyList<StationConnection> stationConnections,
        IReadOnlyList<StationConnectionSegment> allSegments,
        IReadOnlyList<MainRoute> allMainRoutes,
        IReadOnlyDictionary<StationPathId, StationPath> pathsById,
        IReadOnlyDictionary<(EntryPointId, RailId), StationPathId> arrivalIndex,
        IReadOnlyDictionary<(RailId, EntryPointId), StationPathId> departureIndex,
        IReadOnlyList<Rail> rails,
        ProjectSettings projectSettings)
    {
        var result = new Dictionary<RailId, List<ConflictChecker.Occupancy>>();
        var trainsById = trains.ToDictionary(t => t.Id);

        void Add(RailId railId, ConflictChecker.Occupancy occ)
        {
            if (!result.TryGetValue(railId, out var list))
                result[railId] = list = new List<ConflictChecker.Occupancy>();
            list.Add(occ);
        }

        var resolveEp = EntryPointSequenceCache.Build(stationConnections, allSegments, allMainRoutes);
        var trainDepartureIndex = DepartureByStationTrackIndexBuilder.Build(trains);
        var prevTrainMap = TrainConnectionResolver.ResolveUniquePrevTrainMap(trains, trainDepartureIndex, projectSettings);
        var nextTrainMap = TrainConnectionResolver.ResolveUniqueNextTrainMap(trains, trainDepartureIndex, projectSettings);

        var terminalArrivalEnd = new Dictionary<TrainId, int>();
        var initialDepartureStart = new Dictionary<TrainId, int>();

        foreach (var train in trains)
        {
            var segs = train.RunSegments;
            if (segs.Count == 0) continue;

            var initial = StopVisitOccupancyResolver.Resolve(train, 0, resolveEp, pathsById, arrivalIndex, departureIndex);
            if (initial is { DepartureStart: { } ds }) initialDepartureStart[train.Id] = ds;

            var terminal = StopVisitOccupancyResolver.Resolve(train, segs.Count, resolveEp, pathsById, arrivalIndex, departureIndex);
            if (terminal is { ArrivalEnd: { } ae }) terminalArrivalEnd[train.Id] = ae;
        }

        foreach (var train in trains)
        {
            var segs = train.RunSegments;
            if (segs.Count == 0) continue;

            for (int visitSeq = 0; visitSeq <= segs.Count; visitSeq++)
            {
                var v = StopVisitOccupancyResolver.Resolve(train, visitSeq, resolveEp, pathsById, arrivalIndex, departureIndex);
                if (v is not { } visit) continue;

                int? start = visit.ArrivalStart;
                if (start is null)
                {
                    if (TryResolveSplitOriginStart(train, trainsById, out var splitStart))
                    {
                        start = splitStart;
                    }
                    else if (prevTrainMap.TryGetValue(train.Id, out var prevTrainId) &&
                        terminalArrivalEnd.TryGetValue(prevTrainId, out var prevEnd))
                    {
                        start = prevEnd;
                    }
                    else
                    {
                        start = projectSettings.DiagramBasedTimeSec;
                    }
                }

                int? end = visit.DepartureEnd;
                if (end is null)
                {
                    if (nextTrainMap.TryGetValue(train.Id, out var nextTrainId) &&
                        initialDepartureStart.TryGetValue(nextTrainId, out var nextStart))
                    {
                        end = nextStart;
                    }
                    else
                    {
                        end = visit.ArrivalEnd ?? start;
                    }
                }

                if (start is { } s && end is { } e)
                    Add(visit.TrackRailId, new ConflictChecker.Occupancy(train.Id, s, e));
            }
        }

        AddShuntingOccupancy(trains, pathsById, rails, Add);

        return result;
    }

    private static bool TryResolveSplitOriginStart(
        Train train, IReadOnlyDictionary<TrainId, Train> trainsById, out int startSeconds)
    {
        startSeconds = 0;
        var firstWork = train.StopTimes.Values
            .SelectMany(st => st.Works)
            .FirstOrDefault(w => w.Type == StationWorkType.PrevTrain && w.SplitOrigin is not null);
        if (firstWork?.SplitOrigin is not { } origin) return false;
        if (!trainsById.TryGetValue(origin.OriginTrainId, out var originTrain)) return false;
        if (!originTrain.StopTimes.TryGetValue(origin.OriginStopKey, out var originStop)) return false;

        var decoupling = originStop.Works.FirstOrDefault(w => w.Type == StationWorkType.Decoupling);
        if (decoupling is null || decoupling.EndOpSeconds < 0) return false;

        startSeconds = decoupling.EndOpSeconds;
        return true;
    }

    private static void AddShuntingOccupancy(
        IReadOnlyList<Train> trains,
        IReadOnlyDictionary<StationPathId, StationPath> pathsById,
        IReadOnlyList<Rail> rails,
        Action<RailId, ConflictChecker.Occupancy> add)
    {
        var railSequenceResolver = new RailSequenceResolver(rails);
        var trackRailIds = rails.Where(r => r.Role == RailRole.Track).Select(r => r.Id).ToHashSet();

        var railSequenceCache = new Dictionary<StationPathId, IReadOnlyList<RailId>>();
        IReadOnlyList<RailId> ResolveRails(StationPathId spId) =>
            railSequenceCache.TryGetValue(spId, out var cached)
                ? cached
                : railSequenceCache[spId] = railSequenceResolver.Resolve(pathsById[spId]);

        foreach (var train in trains)
        {
            foreach (var stopTime in train.StopTimes.Values)
            {
                foreach (var work in stopTime.Works)
                {
                    if (work.Type != StationWorkType.Shunting) continue;
                    if (work.StationPathId is not { } spId) continue;
                    if (work.StartOpSeconds < 0 || work.EndOpSeconds < 0) continue;

                    foreach (var railId in ResolveRails(spId))
                    {
                        if (!trackRailIds.Contains(railId)) continue;
                        add(railId, new ConflictChecker.Occupancy(train.Id, work.StartOpSeconds, work.EndOpSeconds));
                    }
                }
            }
        }
    }
}
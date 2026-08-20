namespace DiaEditCore.Algorithm.CacheBuilder;

using DiaEditCore.Model;
using DiaEditCore.Model.TimeTable.Trains;

/// <summary>
/// あるStationにおけるTrainのStopTimeから出発順リストを構築する。駅時刻表の表示に利用する予定。TrackOccupancyProvider.csに利用される。
/// 
/// 始発駅（VisitSequence=0）のStopTimeが未設定／DepartureSecondsが未設定（-1）／
/// TrackRailId未設定のTrainは対象外とする。
/// </summary>
public static class DepartureByStationTrackIndexBuilder
{
    public static Dictionary<(StationId StationId, RailId RailId), List<(int DepartureSeconds, TrainId TrainId)>> Build(
        IReadOnlyList<Train> allTrains)
    {
        var index = new Dictionary<(StationId, RailId), List<(int, TrainId)>>();

        foreach (var train in allTrains)
        {
            if (train.RunSegments.Count == 0) continue;

            if (StopKeyAt(train, 0) is not { } departureStopKey) continue;
            var departureStopTime = FindStopTimeAt(train, departureStopKey);
            if (departureStopTime is null || departureStopTime.DepartureSeconds < 0 || departureStopTime.TrackRailId is null)
            {
                continue;
            }

            var key = (departureStopKey.StationId, departureStopTime.TrackRailId.Value);
            if (!index.TryGetValue(key, out var list))
            {
                list = new List<(int, TrainId)>();
                index[key] = list;
            }

            list.Add((departureStopTime.DepartureSeconds, train.Id));
        }

        foreach (var list in index.Values)
        {
            list.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        }

        return index;
    }

    private static StopTime? FindStopTimeAt(Train train, StopKey stopKey)
        => train.StopTimes.TryGetValue(stopKey, out var st) ? st : null;

    private static StopKey? StopKeyAt(Train train, int index)
    {
        var keys = StopKeySequenceBuilder.BuildVisitedStopKeys(train);
        return index >= 0 && index < keys.Count ? keys[index] : null;
    }
}
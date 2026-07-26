using DiaEditCore.Model;
using DiaEditCore.Model.TimeTable;

namespace DiaEditCore.Algorithm;

/// <summary>
/// 6.4節：駅の同一番線を使用する終着・始発列車を時刻順に走査して
/// PrevTrain/NextTrainを導出する。都度導出・非保存（Train自身はPrevTrainId/NextTrainId
/// を保持しない）。OpNumberが未設定のTrainに対しても、折り返し・接続候補をUI表示できるようにする。
///
/// パフォーマンス：TimeTableSetCache.DepartureByStationTrackIndex（駅×番線→発車時刻昇順のTrain列）
/// をBuildDepartureIndexで構築し、ResolveNextTrainCandidatesはこのインデックスを引き当てるだけで
/// 候補を求める（全Train走査のO(N)ではなく、該当駅・番線分のみのO(K)）。
/// </summary>
public static class TrainConnectionResolver
{
    public sealed record ConnectionCandidate(TrainId TrainId, int DepartureSeconds);

    /// <summary>
    /// 全Trainから「駅×番線→発車時刻昇順のTrain列」インデックスを構築する。
    /// TimeTableSetCache.DepartureByStationTrackIndexへの格納値として使う想定。
    /// 始発駅のStopTimeが未設定／DepartureSecondsが未設定（-1）／TrackRailId未設定のTrainは対象外とする。
    /// </summary>
    public static Dictionary<(StationId StationId, RailId RailId), List<(int DepartureSeconds, TrainId TrainId)>> BuildDepartureIndex(
        IReadOnlyList<Train> allTrains)
    {
        var index = new Dictionary<(StationId, RailId), List<(int, TrainId)>>();

        foreach (var train in allTrains)
        {
            if (train.RunSegments.Count == 0) continue;

            var startStationId = train.RunSegments[0].FromStationId;
            var departureStopTime = FindStopTimeForStation(train, startStationId);
            if (departureStopTime is null || departureStopTime.DepartureSeconds < 0 || departureStopTime.TrackRailId is null)
            {
                continue;
            }

            var key = (startStationId, departureStopTime.TrackRailId.Value);
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

    /// <summary>
    /// 到着列車(arrivingTrain)を起点に、接続候補となる出発列車を発車時刻の昇順で返す。
    /// 絞り込み条件：
    ///   1. arrivingTrainの終着駅（RunSegments末尾のToStationId）と、候補の始発駅が一致すること
    ///      （departureIndexのキーに含めることで担保）
    ///   2. 両者のTrackRailIdが一致すること（同一番線。departureIndexのキーに含めることで担保）
    ///   3. 発車時刻 - 到着時刻 が ProjectSettings.ValidationRules.MinTurnaroundSec 以上であること
    ///      （MinTurnaroundSecがnullの場合は下限チェックを行わず、0以上であれば候補とする）
    /// </summary>
    public static IReadOnlyList<ConnectionCandidate> ResolveNextTrainCandidates(
        Train arrivingTrain,
        IReadOnlyDictionary<(StationId StationId, RailId RailId), List<(int DepartureSeconds, TrainId TrainId)>> departureIndex,
        ProjectSettings settings)
    {
        if (arrivingTrain.RunSegments.Count == 0) return Array.Empty<ConnectionCandidate>();

        var terminalStationId = arrivingTrain.RunSegments[^1].ToStationId;
        var arrivalStopTime = FindStopTimeForStation(arrivingTrain, terminalStationId);
        if (arrivalStopTime is null || arrivalStopTime.ArrivalSeconds < 0 || arrivalStopTime.TrackRailId is null)
        {
            return Array.Empty<ConnectionCandidate>();
        }

        if (!departureIndex.TryGetValue((terminalStationId, arrivalStopTime.TrackRailId.Value), out var departures))
        {
            return Array.Empty<ConnectionCandidate>();
        }

        var minTurnaroundSec = settings.ValidationRules.MinTurnaroundSec ?? 0;
        var result = new List<ConnectionCandidate>(departures.Count);

        // departuresは既に発車時刻昇順なので、走査順そのものが結果の順序になる
        foreach (var (departureSeconds, trainId) in departures)
        {
            if (trainId == arrivingTrain.Id) continue;

            var turnaroundSec = departureSeconds - arrivalStopTime.ArrivalSeconds;
            if (turnaroundSec < minTurnaroundSec) continue;

            result.Add(new ConnectionCandidate(trainId, departureSeconds));
        }

        return result;
    }

    /// <summary>
    /// ResolveNextTrainCandidatesの先頭（最も早く発車する＝最短接続となる候補）を採用する。
    /// 候補が0件の場合はnull（運用引き継ぎなし。独立した列車として扱う）。
    /// </summary>
    public static TrainId? ResolveNextTrain(
        Train arrivingTrain,
        IReadOnlyDictionary<(StationId StationId, RailId RailId), List<(int DepartureSeconds, TrainId TrainId)>> departureIndex,
        ProjectSettings settings)
    {
        var candidates = ResolveNextTrainCandidates(arrivingTrain, departureIndex, settings);
        return candidates.Count > 0 ? candidates[0].TrainId : null;
    }

    private static StopTime? FindStopTimeForStation(Train train, StationId stationId)
        => train.StopTimes
            .Where(kv => kv.Key.StationId == stationId)
            .Select(kv => kv.Value)
            .FirstOrDefault();
}
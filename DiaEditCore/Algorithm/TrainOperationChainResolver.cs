using DiaEditCore.Model;
using DiaEditCore.Model.TimeTable;

namespace DiaEditCore.Algorithm;

/// <summary>
/// 6.12節：TrainがどのTrainOperationに属すかを、StationWork.TrainOperationId（StartOp/OpNumberChange、
/// 5.11.5節）を唯一の正データとして導出する。TrainOperation.trainIdsのような正データ配列は持たず、
/// TimeTableSetCache.TrainOperationIndex（5.13節）はこのアルゴリズムの出力キャッシュとして位置づける。
/// 都度導出・非保存。
///
/// StartOp.TrainOperationIdが未設定（null）のTrainは起点にできないため、そのチェーンはtrainOperationIndex
/// に登録されない（＝運用番号としては空欄）。折り返し（接続）自体の可視化はTrainConnectionResolver.
/// ResolveNextTrain/ResolveNextTrainCandidatesを直接使えばよく、OpNumberの有無を問わない
/// （trainOperationIndexを経由する必要はない）。
/// </summary>
public static class TrainOperationChainResolver
{
    /// <summary>
    /// 全TrainからTrainId→TrainOperationId（TimeTableSetCache.TrainOperationIndexそのもの）を導出する。
    /// </summary>
    /// <param name="allTrains">TimeTableSet内の全Train</param>
    /// <param name="departureIndex">TrainConnectionResolver.BuildDepartureIndex()で構築済みのインデックス</param>
    /// <param name="settings">MinTurnaroundSec等の判定に使うProjectSettings</param>
public static Dictionary<TrainId, TrainOperationId> Resolve(
    IReadOnlyList<Train> allTrains,
    IReadOnlyDictionary<(StationId StationId, RailId RailId), List<(int DepartureSeconds, TrainId TrainId)>> departureIndex,
    ProjectSettings settings)
{
    var trainsById = allTrains.ToDictionary(t => t.Id);
    var nextTrainMap = TrainConnectionResolver.ResolveUniqueNextTrainMap(allTrains, departureIndex, settings);
    var result = new Dictionary<TrainId, TrainOperationId>();
    var visited = new HashSet<TrainId>();

    foreach (var startTrain in allTrains)
    {
        var startOp = FindWork(startTrain, StationWorkType.StartOp);
        if (startOp?.TrainOperationId is not { } currentOpId) continue;

        var current = startTrain;
        while (true)
        {
            if (!visited.Add(current.Id)) break;
            result[current.Id] = currentOpId;

            if (!nextTrainMap.TryGetValue(current.Id, out var nextTrainId)) break;
            if (!trainsById.TryGetValue(nextTrainId, out var nextTrain)) break;

            // nextTrainのPrevTrainWork.TrainOperationIdが設定されていれば運用番号を切り替える。
            // 未設定（省略）なら現在の運用番号をそのまま継承する。
            var prevTrainWork = FindWork(nextTrain, StationWorkType.PrevTrain);
            if (prevTrainWork?.TrainOperationId is { } newOpId)
            {
                currentOpId = newOpId;
            }

            current = nextTrain;
        }
    }

    return result;
}

    private static StationWork? FindWork(Train train, StationWorkType type)
        => train.StopTimes.Values
            .SelectMany(st => st.Works)
            .FirstOrDefault(w => w.Type == type);

    private static StopTime? FindStopTimeForStation(Train train, StationId stationId)
        => train.StopTimes
            .Where(kv => kv.Key.StationId == stationId)
            .Select(kv => kv.Value)
            .FirstOrDefault();
}

using DiaEditCore.Model;
using DiaEditCore.Model.TimeTable;

namespace DiaEditCore.Algorithm;

/// <summary>
/// 6.12節：CarCompositionがどのTrainOperationに属すかを、StartOpCarSlot.OperationId
/// （ResolvedOperationRef）を起点とし、PrevTrainOperationOverrideによる明示的な変更を
/// 反映しながらNextTrainチェーンをたどって導出する。都度導出・非保存。
///
/// v11.44改訂セッションでの変更：TrainOperation所属の単位がTrain→CarCompositionへ変更されたため、
/// 出力はDictionary&lt;TrainId,TrainOperationId&gt;からDictionary&lt;(TrainId,CarCompositionId),TrainOperationId&gt;
/// へ変更した（Rule 2横断検証が「直前Trainにおける同一CarCompositionの運用」との比較を必要とするため、
/// 各Trainホップごとのスナップショットを保持する必要がある。flatなCarCompositionId単ｷｰでは
/// 最終値しか残らず情報が失われるため、当初案の§5.12「Dictionary&lt;CarCompositionId,TrainOperationId&gt;」
/// は不十分と判明。設計書側の訂正が必要）。
///
/// 既知の未解決事項（次回6.12節再設計セッションへ持ち越し）：
/// このWalkChainはNextTrainマップを機械的にたどるだけで、対象CarCompositionがDecouplingにより
/// 途中で別Trainへ離脱したケースを検知しない（離脱後も誤って追跡を継続する）。正しく扱うには
/// 各StopKeyでCarConsistResolver.ResolveConsistAtの結果と突き合わせ、対象CompositionIdが
/// 現在Trainの実編成に含まれなくなった時点でチェーンを打ち切る必要がある。
/// </summary>
public static class TrainOperationChainResolver
{
    /// <summary>
    /// (TrainId, CarCompositionId) → その時点でのTrainOperationId。
    /// </summary>
    public static Dictionary<(TrainId TrainId, CarCompositionId CarCompositionId), TrainOperationId> Resolve(
        IReadOnlyList<Train> allTrains,
        IReadOnlyDictionary<(StationId StationId, RailId RailId), List<(int DepartureSeconds, TrainId TrainId)>> departureIndex,
        ProjectSettings settings)
    {
        var trainsById = allTrains.ToDictionary(t => t.Id);
        var nextTrainMap = TrainConnectionResolver.ResolveUniqueNextTrainMap(allTrains, departureIndex, settings);
        var result = new Dictionary<(TrainId, CarCompositionId), TrainOperationId>();

        // 起点集合：全TrainのStartOpConsistに現れる (CarCompositionId, ResolvedOperationRef) の組すべて
        foreach (var startTrain in allTrains)
        {
            var startOp = FindWork(startTrain, StationWorkType.StartOp);
            if (startOp is null) continue;

            foreach (var slot in startOp.StartOpConsist)
            {
                if (slot.OperationId is not ResolvedOperationRef resolved) continue; // Provisionalは運用未確定として対象外
                WalkChain(slot.CarCompositionId, resolved.Id, startTrain, trainsById, nextTrainMap, result);
            }
        }

        return result;
    }

    private static void WalkChain(
        CarCompositionId compositionId,
        TrainOperationId currentOpId,
        Train startTrain,
        IReadOnlyDictionary<TrainId, Train> trainsById,
        IReadOnlyDictionary<TrainId, TrainId> nextTrainMap,
        Dictionary<(TrainId, CarCompositionId), TrainOperationId> result)
    {
        // 各(Train,CarComposition)ペアは高々1つのチェーンからしか訪問されない
        // （TrainConnectionResolver.ResolveUniqueNextTrainMapの一意性、および発車時刻の
        // 狭義単調増加による循環不可能性より。TrainId単位の証明をペア単位に読み替えたもの・要再確認）
        var current = startTrain;
        var visited = new HashSet<TrainId>();

        while (true)
        {
            if (!visited.Add(current.Id)) break;
            result[(current.Id, compositionId)] = currentOpId;

            if (!nextTrainMap.TryGetValue(current.Id, out var nextTrainId)) break;
            if (!trainsById.TryGetValue(nextTrainId, out var nextTrain)) break;

            var prevTrainWork = FindWork(nextTrain, StationWorkType.PrevTrain);
            var overrideEntry = prevTrainWork?.PrevTrainOperationOverrides
                .FirstOrDefault(o => o.CarCompositionId == compositionId);
            if (overrideEntry is not null)
            {
                currentOpId = overrideEntry.NewOperationId;
            }

            current = nextTrain;
        }
    }

    private static StationWork? FindWork(Train train, StationWorkType type)
        => train.StopTimes.Values
            .SelectMany(st => st.Works)
            .FirstOrDefault(w => w.Type == type);
}
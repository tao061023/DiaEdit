namespace DiaEditCore.Algorithm;

using DiaEditCore.Model;
using DiaEditCore.Model.TimeTable.Trains;

/// <summary>
/// CarCompositionがどのTrainOperationに属すかを、StartOpCarSlot.OperationId
/// （ResolvedOperationRef）を起点とし、PrevTrainOperationOverrideによる明示的な変更を
/// 反映しながらNextTrainチェーンをたどって導出する。都度導出・非保存。
///
/// vNEXT改訂：以下2つの欠落を解消した。
///   (A) Decouplingで離脱した子TrainはStartOpを持たないためチェーン起点に一切登場せず、
///       result辞書に登録されなかった。→ TryFollowDecouplingでSplitOriginRef経由の子Trainへ
///       チェーンを付け替える処理を追加。
///   (B) Couplingで自Trainが相手（Host）Trainへ合流した場合、旧実装はnextTrainMapのみに
///       依存していたため合流先を認識できず、そこでチェーンが打ち切られていた。
///       → TryFollowCouplingでHost Train側へチェーンを付け替える処理を追加
///         （OperationIdはCarComposition自身の属性であり合流によって変化しないため据え置き）。
///
/// 判定順序：毎周、Decoupling判定→Coupling判定→通常のnextTrainMapの順に確認する
/// （同一Trainが同一駅でDecoupling/Coupling双方に関与するケースを想定。visitedはcurrent.Id
/// ベースのため、host側が別チェーンで訪問済みなら合流時に正しく打ち切られる）。
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
        var splitChildIndex = BuildSplitChildIndex(allTrains);
        var couplingPartnerIndex = BuildCouplingPartnerIndex(allTrains);
        var result = new Dictionary<(TrainId, CarCompositionId), TrainOperationId>();

        foreach (var startTrain in allTrains)
        {
            var startOp = FindWork(startTrain, StationWorkType.StartOp);
            if (startOp is null) continue;

            foreach (var slot in startOp.StartOpConsist)
            {
                if (slot.OperationId is not ResolvedOperationRef resolved) continue; // Provisionalは対象外
                WalkChain(slot.CarCompositionId, resolved.Id, startTrain,
                    trainsById, nextTrainMap, splitChildIndex, couplingPartnerIndex, result);
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
        IReadOnlyDictionary<(TrainId, StopKey), Train> splitChildIndex,
        IReadOnlyDictionary<(TrainId, StopKey), (Train HostTrain, StopKey HostStopKey)> couplingPartnerIndex,
        Dictionary<(TrainId, CarCompositionId), TrainOperationId> result)
    {
        var current = startTrain;
        var visited = new HashSet<TrainId>();

        while (true)
        {
            if (!visited.Add(current.Id)) break;
            result[(current.Id, compositionId)] = currentOpId;

            var (decTrain, decOpId, decRedirected) =
                TryFollowDecoupling(current, compositionId, currentOpId, splitChildIndex);
            if (decRedirected)
            {
                if (decTrain is null) break; // 子Train未解決＝データ不整合。SplitOriginCrossValidator側検出対象
                current = decTrain;
                currentOpId = decOpId;
                continue;
            }

            var coupHit = TryFollowCoupling(current, couplingPartnerIndex);
            if (coupHit is { } hit)
            {
                // OperationIdは合流で変化しない（CarCompositionに紐づく属性のため据え置き）
                current = hit.HostTrain;
                continue;
            }

            if (!nextTrainMap.TryGetValue(current.Id, out var nextTrainId)) break;
            if (!trainsById.TryGetValue(nextTrainId, out var normalNext)) break;

            var prevTrainWork = FindWork(normalNext, StationWorkType.PrevTrain);
            var overrideEntry = prevTrainWork?.PrevTrainOperationOverrides
                .FirstOrDefault(o => o.CarCompositionId == compositionId);
            if (overrideEntry is not null) currentOpId = overrideEntry.NewOperationId;

            current = normalNext;
        }
    }

    /// <summary>
    /// currentTrain内のDecoupling作業でcompositionIdが「離脱側グループ」に含まれるかを判定する。
    /// 継続側に含まれる場合もOperationIdは更新しうる（front/rear問わずCutGroupEntry.OperationIdが
    /// 分割後の運用番号を明示するため）が、Train切替は発生しない（Redirected=false）。
    /// </summary>
    private static (Train? Train, TrainOperationId OpId, bool Redirected) TryFollowDecoupling(
        Train current,
        CarCompositionId compositionId,
        TrainOperationId currentOpId,
        IReadOnlyDictionary<(TrainId, StopKey), Train> splitChildIndex)
    {
        foreach (var (stopKey, stopTime) in current.StopTimes)
        {
            var decouplingWork = stopTime.Works.FirstOrDefault(w => w.Type == StationWorkType.Decoupling);
            if (decouplingWork?.DecouplingDetail is not { } dw) continue;

            var inFront = dw.FrontGroup.FirstOrDefault(e => e.CarCompositionId == compositionId);
            var inRear = dw.RearGroup.FirstOrDefault(e => e.CarCompositionId == compositionId);
            if (inFront is null && inRear is null) continue;

            var isContinuingSide = dw.IsRearBase ? inRear is not null : inFront is not null;
            var entry = inFront ?? inRear!;
            var newOpId = entry.OperationId is ResolvedOperationRef r ? r.Id : currentOpId; // Provisionalなら現状維持

            if (isContinuingSide) return (current, newOpId, false);

            return splitChildIndex.TryGetValue((current.Id, stopKey), out var child)
                ? (child, newOpId, true)
                : (null, newOpId, true); // 子Train未解決
        }
        return (current, currentOpId, false);
    }

    private static (Train HostTrain, StopKey HostStopKey)? TryFollowCoupling(
        Train current,
        IReadOnlyDictionary<(TrainId, StopKey), (Train HostTrain, StopKey HostStopKey)> couplingPartnerIndex)
    {
        foreach (var stopKey in current.StopTimes.Keys)
        {
            if (couplingPartnerIndex.TryGetValue((current.Id, stopKey), out var hit))
                return hit;
        }
        return null;
    }

    /// <summary>(OriginTrainId, OriginStopKey) → SplitOriginRef経由の子Train。</summary>
    private static Dictionary<(TrainId, StopKey), Train> BuildSplitChildIndex(IReadOnlyList<Train> allTrains)
    {
        var index = new Dictionary<(TrainId, StopKey), Train>();
        foreach (var train in allTrains)
        {
            var prevTrainWork = train.StopTimes.Values
                .SelectMany(st => st.Works)
                .FirstOrDefault(w => w.Type == StationWorkType.PrevTrain && w.SplitOrigin is not null);
            if (prevTrainWork?.SplitOrigin is not { } origin) continue;

            index[(origin.OriginTrainId, origin.OriginStopKey)] = train;
        }
        return index;
    }

    /// <summary>(PartnerTrainId, PartnerStopKey) → (HostTrain, HostStopKey)。</summary>
    private static Dictionary<(TrainId, StopKey), (Train, StopKey)> BuildCouplingPartnerIndex(IReadOnlyList<Train> allTrains)
    {
        var index = new Dictionary<(TrainId, StopKey), (Train, StopKey)>();
        foreach (var hostTrain in allTrains)
        {
            foreach (var (hostStopKey, stopTime) in hostTrain.StopTimes)
            {
                foreach (var work in stopTime.Works)
                {
                    if (work is not { Type: StationWorkType.Coupling, CouplingDetail: { } cw }) continue;
                    index[(cw.PartnerTrainId, cw.PartnerStopKey)] = (hostTrain, hostStopKey);
                }
            }
        }
        return index;
    }

    private static StationWork? FindWork(Train train, StationWorkType type)
        => train.StopTimes.Values
            .SelectMany(st => st.Works)
            .FirstOrDefault(w => w.Type == type);
}
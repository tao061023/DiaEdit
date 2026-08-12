using DiaEditCore.Model;
using DiaEditCore.Model.TimeTable.Trains;

namespace DiaEditCore.Algorithm;

/// <summary>
/// 駅の同一番線を使用する終着・始発列車を時刻順に走査して
/// PrevTrain/NextTrainを導出する。都度導出・非保存（Train自身はPrevTrainId/NextTrainId
/// を保持しない）。OpNumberが未設定のTrainに対しても、折り返し・接続候補をUI表示できるようにする。
///
/// パフォーマンス：TimeTableSetCache.DepartureByStationTrackIndex（駅×番線→発車時刻昇順のTrain列）
/// をBuildDepartureIndexで構築し、ResolveNextTrainCandidatesはこのインデックスを引き当てるだけで
/// 候補を求める（全Train走査のO(N)ではなく、該当駅・番線分のみのO(K)）。
///
/// 一意マッチングについて：
/// ResolveNextTrainCandidates／ResolveNextTrainは、到着列車ごとに独立して「最短接続となる
/// 出発列車」を選ぶAPIのため、複数の到着列車が同じ出発列車を候補として選びうる（非単射）。
/// これはUI上の接続候補表示（ユーザーに選択肢を見せる用途）としては正しい挙動だが、
/// TrackOccupancyProvider・TrainOperationChainResolverのように
/// 「物理的に一意な折り返しペア」を前提とする用途では非単射なマッチングをそのまま使うと
/// 誤ったTrack占有の重複や運用チェーンの上書きを引き起こす。
///
/// そのため、全体で整合する一意なマッチングをResolveUniqueNextTrainMap／
/// ResolveUniquePrevTrainMapとして別途提供する。マッチング規則：各出発列車(departure)につき、
/// それを候補とする到着列車のうち到着時刻が最も遅い（＝乗継時間が最短の）到着列車のみを
/// 唯一のPrevTrainとして確定する。敗れた到着列車は、その出発列車をNextTrainとして採用しない
/// （＝独立した列車として扱われる。他に候補がなければ接続なしとなる）。
/// </summary>
public static class TrainConnectionResolver
{
    public sealed record ConnectionCandidate(TrainId TrainId, int DepartureSeconds);

    // BuildDepartureIndex は DepartureByStationTrackIndexBuilder.Build へ移設（v12.18）。
    // 呼び出し元は Algorithm.CacheBuilder.DepartureByStationTrackIndexBuilder.Build を直接使うこと。

    /// <summary>
    /// 到着列車(arrivingTrain)を起点に、接続候補となる出発列車を発車時刻の昇順で返す。
    /// UI上の接続候補表示用途（複数候補をユーザーに提示する）。一意性は保証しない
    /// （非単射になりうる点はクラスコメント参照）。
    ///
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

        if (StopKeyAt(arrivingTrain, arrivingTrain.RunSegments.Count) is not { } arrivalStopKey)
        {
            return Array.Empty<ConnectionCandidate>();
        }
        var terminalStationId = arrivalStopKey.StationId;
        var arrivalStopTime = FindStopTimeAt(arrivingTrain, arrivalStopKey);
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
    /// ResolveNextTrainCandidatesの先頭（最も早く発車する＝最短接続となる候補）を採用する、
    /// 単一列車視点の簡易API。
    ///
    /// 注意：このAPIは非単射でありうる（クラスコメント参照）。TrackOccupancyProvider・
    /// TrainOperationChainResolverのように物理的に一意な折り返しペアを前提とする用途では、
    /// 必ずResolveUniqueNextTrainMap／ResolveUniquePrevTrainMapを使うこと。
    /// このAPIは主にUI上の単純表示（「とりあえず1つ候補を見せる」用途）に留める。
    /// </summary>
    public static TrainId? ResolveNextTrain(
        Train arrivingTrain,
        IReadOnlyDictionary<(StationId StationId, RailId RailId), List<(int DepartureSeconds, TrainId TrainId)>> departureIndex,
        ProjectSettings settings)
    {
        var candidates = ResolveNextTrainCandidates(arrivingTrain, departureIndex, settings);
        return candidates.Count > 0 ? candidates[0].TrainId : null;
    }

    /// <summary>
    /// 全体で整合する一意なNextTrainマッチングを構築する（到着列車→出発列車）。
    ///
    /// アルゴリズム：
    ///   1. 全Trainについて、ResolveNextTrainCandidatesで候補列を求める
    ///   2. 各出発列車(departure)ごとに、それを候補とする到着列車の中から
    ///      到着時刻が最も遅い（＝乗継時間が最短の）到着列車を1つだけ選ぶ
    ///      （同着の場合はTrainIdの値で決定的にタイブレークする）
    ///   3. 選ばれなかった到着列車は、その出発列車をNextTrainとしない
    ///      （他に候補があれば次点、なければ接続なし＝nullとなる。今回は
    ///      「複数出発列車を跨いだ再割当」は行わず、単純に「その出発列車は使えない」
    ///      として扱う。1出発列車=最大1到着列車という制約のみを保証する）
    ///
    /// 停止性：全Train・全候補に対する有限回の走査のみで、再帰・連鎖探索を行わないため必ず停止する。
    /// 一意性：departureごとに高々1つのarrivingTrainのみがマッチするため、本メソッドが返す
    /// マップは常に単射（NextTrainマップの値に重複がない）であることが保証される。
    /// </summary>
    public static Dictionary<TrainId, TrainId> ResolveUniqueNextTrainMap(
        IReadOnlyList<Train> allTrains,
        IReadOnlyDictionary<(StationId StationId, RailId RailId), List<(int DepartureSeconds, TrainId TrainId)>> departureIndex,
        ProjectSettings settings)
    {
        // departureTrainId -> 現時点の最有力到着列車 (arrivingTrainId, arrivalSeconds)
        var bestForDeparture = new Dictionary<TrainId, (TrainId ArrivingTrainId, int ArrivalSeconds)>();

        foreach (var arrivingTrain in allTrains)
        {
            if (arrivingTrain.RunSegments.Count == 0) continue;

            if (StopKeyAt(arrivingTrain, arrivingTrain.RunSegments.Count) is not { } arrivalStopKey) continue;
            var arrivalStopTime = FindStopTimeAt(arrivingTrain, arrivalStopKey);
            if (arrivalStopTime is null || arrivalStopTime.ArrivalSeconds < 0) continue;

            var candidates = ResolveNextTrainCandidates(arrivingTrain, departureIndex, settings);
            foreach (var candidate in candidates)
            {
                var arrivalSeconds = arrivalStopTime.ArrivalSeconds;

                if (!bestForDeparture.TryGetValue(candidate.TrainId, out var current))
                {
                    bestForDeparture[candidate.TrainId] = (arrivingTrain.Id, arrivalSeconds);
                    continue;
                }

                // 乗継時間最短優先＝到着時刻が最も遅い方を採用。同着はTrainId.Valueが小さい方を
                // 決定的に優先する（走査順に依存させないためのタイブレーク）。
                if (arrivalSeconds > current.ArrivalSeconds ||
                    (arrivalSeconds == current.ArrivalSeconds && arrivingTrain.Id.Value < current.ArrivingTrainId.Value))
                {
                    bestForDeparture[candidate.TrainId] = (arrivingTrain.Id, arrivalSeconds);
                }
            }
        }

        var result = new Dictionary<TrainId, TrainId>();
        foreach (var (departureTrainId, best) in bestForDeparture)
        {
            result[best.ArrivingTrainId] = departureTrainId;
        }

        return result;
    }

    /// <summary>
    /// ResolveUniqueNextTrainMapを反転させたPrevTrainマップ（出発列車→到着列車）を構築する。
    /// ResolveUniqueNextTrainMap自体がdepartureごとに高々1つのarrivingTrainしか持たないことを
    /// 保証しているため、反転操作は単純なDictionary変換で安全に行える（キー重複は発生しない）。
    /// </summary>
    public static Dictionary<TrainId, TrainId> ResolveUniquePrevTrainMap(
        IReadOnlyList<Train> allTrains,
        IReadOnlyDictionary<(StationId StationId, RailId RailId), List<(int DepartureSeconds, TrainId TrainId)>> departureIndex,
        ProjectSettings settings)
    {
        var nextMap = ResolveUniqueNextTrainMap(allTrains, departureIndex, settings);
        var result = new Dictionary<TrainId, TrainId>();
        foreach (var (arrivingTrainId, departureTrainId) in nextMap)
        {
            result[departureTrainId] = arrivingTrainId;
        }
        return result;
    }

    private static StopTime? FindStopTimeAt(Train train, StopKey stopKey)
        => train.StopTimes.TryGetValue(stopKey, out var st) ? st : null;

    private static StopKey? StopKeyAt(Train train, int index)
    {
        var keys = StopKeySequenceBuilder.BuildVisitedStopKeys(train);
        return index >= 0 && index < keys.Count ? keys[index] : null;
    }
}
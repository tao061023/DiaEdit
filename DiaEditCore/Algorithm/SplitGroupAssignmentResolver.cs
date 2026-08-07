using DiaEditCore.Model;
using DiaEditCore.Model.TimeTable;

namespace DiaEditCore.Algorithm;

/// <summary>
/// 新設（v11.44改訂セッション）：Decoupling後に各Trainがどの CutGroup（GroupIndex）を
/// 引き継いだかを、全Train横断で導出する。
///
/// 背景：CutGroup.GroupIndexは保存データだが「このTrainがどのGroupIndexを引き継いだか」は
/// 保存されない（SplitOriginRefはOriginTrainId/OriginStopKeyのみを持つ）。分割元Train自身の
/// 継続分と、SplitOriginRef経由の新規Train群（"兄弟"）をあわせて発車時刻順に並べ、
/// two-pointer方式でGroupIndexを割り当てる：
///   - 前提①：ランナウンド線はスコープ外（中間グループの先出し不可＝両端からしか引き出せない）
///   - 前提②：分割された編成が同時発車することは信号システム上あり得ない（時刻の同順位なし）
///   - 判定基準：当該StopTimeにShunting作業が同居しなければ手前側(lo)、同居すれば奥側(hi)から割当
///
/// 制約：候補数（origin継続1 + 兄弟数）がCutGroups.Countと一致しない場合は割当を行わない
/// （データ不整合。SplitOriginCrossValidator側で検出させる）。
/// </summary>
public static class SplitGroupAssignmentResolver
{
    /// <summary>
    /// TrainId → そのTrainが引き継いだGroupIndex。
    /// </summary>
    public static Dictionary<TrainId, int> Resolve(IReadOnlyList<Train> allTrains)
    {
        var result = new Dictionary<TrainId, int>();

        foreach (var originTrain in allTrains)
        {
            foreach (var (stopKey, stopTime) in originTrain.StopTimes)
            {
                var decoupling = stopTime.Works.FirstOrDefault(w => w.Type == StationWorkType.Decoupling);
                if (decoupling is null) continue;

                var groupCount = decoupling.CutGroups.Count;
                if (groupCount == 0) continue;

                var siblings = allTrains
                    .Select(t => (Train: t, Work: FindPrevTrainWorkWithOrigin(t, originTrain.Id, stopKey)))
                    .Where(x => x.Work is not null)
                    .ToList();

                var candidates = new List<(TrainId TrainId, int DepartureSeconds, bool Shunted)>
                {
                    (originTrain.Id, stopTime.DepartureSeconds,
                        stopTime.Works.Any(w => w.Type == StationWorkType.Shunting)),
                };

                foreach (var (sibTrain, sibWork) in siblings)
                {
                    var sibStopTime = sibTrain.StopTimes.Values.FirstOrDefault(st => st.Works.Contains(sibWork!));
                    if (sibStopTime is null) continue;

                    candidates.Add((sibTrain.Id, sibStopTime.DepartureSeconds,
                        sibStopTime.Works.Any(w => w.Type == StationWorkType.Shunting)));
                }

                // 候補数がCutGroups数と一致しない＝不整合。割当を行わない（Validator側で検出）
                if (candidates.Count != groupCount) continue;

                var ordered = candidates.OrderBy(c => c.DepartureSeconds).ToList();
                var lo = 0;
                var hi = groupCount - 1;
                foreach (var candidate in ordered)
                {
                    var index = candidate.Shunted ? hi-- : lo++;
                    result[candidate.TrainId] = index;
                }
            }
        }

        return result;
    }

    private static StationWork? FindPrevTrainWorkWithOrigin(Train train, TrainId originTrainId, StopKey originStopKey)
        => train.StopTimes.Values
            .SelectMany(st => st.Works)
            .FirstOrDefault(w => w.Type == StationWorkType.PrevTrain
                && w.SplitOrigin is { } origin
                && origin.OriginTrainId == originTrainId
                && origin.OriginStopKey == originStopKey);
}

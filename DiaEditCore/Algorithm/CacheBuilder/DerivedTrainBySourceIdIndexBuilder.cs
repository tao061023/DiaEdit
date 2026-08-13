namespace DiaEditCore.Algorithm.CacheBuilder;

using DiaEditCore.Model;
using DiaEditCore.Model.TimeTable.Trains;

/// <summary>
/// TimeTableSetCache.DerivedTrainsBySourceIdIndex（TrainId→そのTrainをSourceTrainIdとして
/// 複製されたTrainの一覧）の構築を担う。
///
/// Train.SourceTrainId（4.9.2節、Train複製Paste時に設定される派生元参照）を1回走査するだけで
/// 導出できる逆引きインデックス。v12.18で判明した「RebuildAllが空のまま」だった6インデックスのうちの1つ。
/// 消費者はDependencyResolver.ResolveDirectDependents（TrainObjectIdケース）。
///
/// 注：現行のDependencyResolver.ResolveDirectDependentsはTrainObjectId => [] と終端ノード扱いのため、
/// 本インデックスを消費する新ケース追加はTrain削除コマンド実装時にあわせて行う（本Builder自体は
/// RebuildAllの空実装解消というスコープに留め、DependencyResolver側のswitch式変更は別タスクとする）。
/// </summary>
public static class DerivedTrainsBySourceIdIndexBuilder
{
    public static Dictionary<TrainId, List<TrainId>> Build(IEnumerable<Train> allTrains)
    {
        var index = new Dictionary<TrainId, List<TrainId>>();

        foreach (var train in allTrains)
        {
            if (train.SourceTrainId is not { } sourceId) continue;

            if (!index.TryGetValue(sourceId, out var list))
            {
                list = new List<TrainId>();
                index[sourceId] = list;
            }
            list.Add(train.Id);
        }

        return index;
    }
}
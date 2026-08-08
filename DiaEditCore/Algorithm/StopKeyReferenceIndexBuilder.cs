using DiaEditCore.Model;
using DiaEditCore.Model.TimeTable.Trains;

namespace DiaEditCore.Algorithm;

/// <summary>
/// SplitOriginRef／CouplingWork.PartnerStopKeyによる、Train間のStopKey参照を逆引きできる
/// Indexを構築する。TimeTableSetCache.StopKeyReferenceIndexへの格納値として使う想定。 <br/>
///
/// 用途は2つに限定する： <br/>
///   1. RunSegments編集コマンドがAffectedIds算出時に「このStopKeyを参照しているTrain」を
///      効率的に洗い出す <br/>
///   2. Cross Validator（SplitOriginRef／CouplingWork実在性検証）が検証対象を絞り込む <br/>
///
/// 注意：StopKeyはRunSegments編集により値が変わりうる不安定なキーであり、ObjectIdグラフ
/// （DependencyResolver）とは意図的に別枠としている。このIndexをDependencyResolverの
/// ObjectId switch式に混在させないこと。
/// </summary>
public static class StopKeyReferenceIndexBuilder
{
    public static Dictionary<(TrainId, StopKey), List<StopKeyReferrer>> Build(IReadOnlyList<Train> allTrains)
    {
        var index = new Dictionary<(TrainId, StopKey), List<StopKeyReferrer>>();

        foreach (var referrerTrain in allTrains)
        {
            foreach (var stopTime in referrerTrain.StopTimes.Values)
            {
                foreach (var work in stopTime.Works)
                {
                    if (work.Type == StationWorkType.PrevTrain && work.SplitOrigin is { } origin)
                    {
                        Add(index, origin.OriginTrainId, origin.OriginStopKey,
                            new StopKeyReferrer(referrerTrain.Id, StopKeyReferenceKind.SplitOrigin));
                    }

                    if (work.Type == StationWorkType.Coupling && work.CouplingDetail is { } cw)
                    {
                        Add(index, cw.PartnerTrainId, cw.PartnerStopKey,
                            new StopKeyReferrer(referrerTrain.Id, StopKeyReferenceKind.CouplingPartner));
                    }
                }
            }
        }

        return index;
    }

    private static void Add(
        Dictionary<(TrainId, StopKey), List<StopKeyReferrer>> index,
        TrainId targetTrainId,
        StopKey targetStopKey,
        StopKeyReferrer referrer)
    {
        var key = (targetTrainId, targetStopKey);
        if (!index.TryGetValue(key, out var list))
        {
            list = new List<StopKeyReferrer>();
            index[key] = list;
        }
        list.Add(referrer);
    }
}
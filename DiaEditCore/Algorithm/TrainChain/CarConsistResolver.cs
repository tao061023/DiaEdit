using DiaEditCore.Model;
using DiaEditCore.Model.Cars;
using DiaEditCore.Model.TimeTable.Trains;

namespace DiaEditCore.Algorithm;

/// <summary>
/// 6.7節：StartOp.startOpConsist、またはPrevTrain.SplitOrigin経由で他Trainのconsistsequenceを
/// 起点として、以降のCoupling/Decouplingイベント（CutGroups）を時系列順にたどることで
/// 任意時点の実編成を復元する。都度導出・非保存。
///
/// v11.44改訂セッションでの変更：CutGroup.TrainIdが廃止されたことに伴い、「自Trainがどの
/// GroupIndexを引き継いだか」は自Trainの情報だけでは判定できなくなった（分割元Train自身の
/// 継続分と兄弟Train群の発車順比較が必要なため）。そのためSplitGroupAssignmentResolver
/// （全Train横断）の出力を必須の追加引数として受け取る形に変更した。
/// 「他Trainへのグラフ探索は行わない」という従来のスコープの一部が崩れている点に注意
/// （SplitOrigin経由の起点特定でoriginTrainの1StopTimeのみを読む、という1回限りの参照は維持）。
/// </summary>
/// <summary>
/// CarConsistResolver.ResolveConsistAtへ渡す横断参照データをまとめたもの。
/// SplitOrigin/Decoupling/Couplingを一切使わない単純なテスト・呼び出しでは
/// ConsistResolutionContext.Empty(carConsists, carCompositions)で足りる。
/// </summary>
public sealed record ConsistResolutionContext(
    IReadOnlyDictionary<CarConsistId, CarConsist> CarConsists,
    IReadOnlyDictionary<CarCompositionId, CarComposition> CarCompositions,
    IReadOnlyDictionary<TrainId, int> SplitGroupAssignments,
    IReadOnlyDictionary<TrainId, Train> AllTrainsById)
{
    public static ConsistResolutionContext Empty(
        IReadOnlyDictionary<CarConsistId, CarConsist> carConsists,
        IReadOnlyDictionary<CarCompositionId, CarComposition> carCompositions)
        => new(carConsists, carCompositions,
            new Dictionary<TrainId, int>(), new Dictionary<TrainId, Train>());
}

public static class CarConsistResolver
{
    public sealed record ResolvedConsist(
        IReadOnlyList<CarCompositionId> ConsistBlocks,
        IReadOnlyList<CarRef> Cars);

    private static readonly ResolvedConsist Empty = new(Array.Empty<CarCompositionId>(), Array.Empty<CarRef>());

    /// <summary>
    /// train自身のWorks列をStartOp（またはPrevTrain.SplitOrigin）から対象stopKeyまで時系列順にたどり、
    /// 実編成を復元する。起点が見つからない場合、または対象stopKeyが起点より先行する場合は空を返す。
    /// </summary>
    public static ResolvedConsist ResolveConsistAt(
        Train train,
        StopKey stopKey,
        ConsistResolutionContext context)
    {
        var carConsists = context.CarConsists;
        var carCompositions = context.CarCompositions;
        var splitGroupAssignments = context.SplitGroupAssignments;
        var allTrainsById = context.AllTrainsById;
        var visitedKeys = BuildVisitedStopKeys(train);
        var targetIndex = visitedKeys.IndexOf(stopKey);
        if (targetIndex < 0) return Empty;

        var startIndex = -1;
        List<CarCompositionId>? current = null;

        for (var i = 0; i <= targetIndex; i++)
        {
            if (!train.StopTimes.TryGetValue(visitedKeys[i], out var stopTime)) continue;

            foreach (var work in stopTime.Works)
            {
                switch (work.Type)
                {
                    case StationWorkType.StartOp when startIndex < 0:
                        startIndex = i;
                        current = work.StartOpConsist
                            .OrderBy(slot => slot.Position)
                            .Select(slot => slot.CarCompositionId)
                            .ToList();
                        break;

                    // 新設：分割由来の新Trainは、StartOpの代わりにPrevTrain.SplitOriginを起点とする
                    case StationWorkType.PrevTrain when startIndex < 0 && work.SplitOrigin is { } origin:
                        startIndex = i;
                        current = ResolveSplitOriginConsist(train.Id, origin, allTrainsById, splitGroupAssignments);
                        break;

                    case StationWorkType.Decoupling when startIndex >= 0:
                        // v11.44改訂：CutGroup.TrainIdが廃止されたため、自TrainがどのGroupIndexを
                        // 引き継いだかはSplitGroupAssignmentResolverの結果を参照する。
                        if (splitGroupAssignments.TryGetValue(train.Id, out var myGroupIndex))
                        {
                            current = work.CutGroups
                                .Where(cg => cg.GroupIndex == myGroupIndex)
                                .OrderBy(cg => cg.GroupIndex)
                                .Select(cg => cg.CarCompositionId)
                                .ToList();
                        }
                        else
                        {
                            // 割当が見つからない＝データ不整合。SplitOriginCrossValidator側で検出される想定。
                            current = new List<CarCompositionId>();
                        }
                        break;

                    case StationWorkType.Coupling when startIndex >= 0:
                        // 自Train内完結：CutGroups全件をGroupIndex順に連結する
                        // （他Trainの中身はCarCompositionIdに確定済み。TrainIdは元々不要）
                        current = work.CutGroups
                            .OrderBy(cg => cg.GroupIndex)
                            .Select(cg => cg.CarCompositionId)
                            .ToList();
                        break;
                }
            }
        }

        if (startIndex < 0 || current is null) return Empty;

        var cars = new List<CarRef>();
        foreach (var compositionId in current)
        {
            if (carCompositions.TryGetValue(compositionId, out var composition)
                && carConsists.TryGetValue(composition.CarConsistId, out var consist))
            {
                cars.AddRange(consist.Cars);
            }
        }

        return new ResolvedConsist(current, cars);
    }

    private static List<CarCompositionId> ResolveSplitOriginConsist(
        TrainId trainId,
        SplitOriginRef origin,
        IReadOnlyDictionary<TrainId, Train> allTrainsById,
        IReadOnlyDictionary<TrainId, int> splitGroupAssignments)
    {
        if (!allTrainsById.TryGetValue(origin.OriginTrainId, out var originTrain)) return new List<CarCompositionId>();
        if (!originTrain.StopTimes.TryGetValue(origin.OriginStopKey, out var originStop)) return new List<CarCompositionId>();

        var decoupling = originStop.Works.FirstOrDefault(w => w.Type == StationWorkType.Decoupling);
        if (decoupling is null) return new List<CarCompositionId>();
        if (!splitGroupAssignments.TryGetValue(trainId, out var myGroupIndex)) return new List<CarCompositionId>();

        return decoupling.CutGroups
            .Where(cg => cg.GroupIndex == myGroupIndex)
            .OrderBy(cg => cg.GroupIndex)
            .Select(cg => cg.CarCompositionId)
            .ToList();
    }

    internal static List<StopKey> BuildVisitedStopKeys(Train train)
    {
        var stations = new List<StationId>();
        if (train.RunSegments.Count > 0)
        {
            stations.Add(train.RunSegments[0].FromStationId);
            foreach (var segment in train.RunSegments)
            {
                stations.Add(segment.ToStationId);
            }
        }

        var visitCounts = new Dictionary<StationId, int>();
        var keys = new List<StopKey>(stations.Count);
        foreach (var stationId in stations)
        {
            var visitSequence = visitCounts.TryGetValue(stationId, out var count) ? count : 0;
            keys.Add(new StopKey(stationId, visitSequence));
            visitCounts[stationId] = visitSequence + 1;
        }

        return keys;
    }
}
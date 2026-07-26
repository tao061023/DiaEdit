using DiaEditCore.Model;
using DiaEditCore.Model.Cars;
using DiaEditCore.Model.TimeTable;

namespace DiaEditCore.Algorithm;

/// <summary>
/// 6.7節：StartOp.startOpConsist（＝出区時点のconsistSequence）を起点として、
/// 以降のCoupling/Decouplingイベント（CutPoints）を時系列順にたどることで
/// 任意時点の実編成を復元する。都度導出・非保存。
///
/// スコープ：対象Train自身のWorks列のみを走査する（他Trainを検索しない）。
/// TrainCutPoint.TrainIdは相手Trainの識別情報であり、他Trainの編成の中身は
/// CutPoint.CarConsistIdにすでに確定値として書き込まれている前提（合意済み）。
/// </summary>
public static class CarConsistResolver
{
    /// <summary>
    /// ある時点の実編成。ConsistBlocksはPosition順のCarConsistId列（編成ブロック単位）、
    /// CarsはConsistBlocksを順に展開したCarRef列（6.8節EffectiveLengthChecker等が使用）。
    /// </summary>
    public sealed record ResolvedConsist(
        IReadOnlyList<CarConsistId> ConsistBlocks,
        IReadOnlyList<CarRef> Cars);

    private static readonly ResolvedConsist Empty = new(Array.Empty<CarConsistId>(), Array.Empty<CarRef>());

    /// <summary>
    /// train自身のWorks列をStartOpから対象stopKeyまで時系列順にたどり、実編成を復元する。
    /// StartOpが見つからない場合、または対象stopKeyがStartOpより先行する場合は空を返す（例外は投げない）。
    /// </summary>
    public static ResolvedConsist ResolveConsistAt(
        Train train,
        StopKey stopKey,
        IReadOnlyDictionary<CarConsistId, CarConsist> carConsists)
    {
        var visitedKeys = BuildVisitedStopKeys(train);
        var targetIndex = visitedKeys.IndexOf(stopKey);
        if (targetIndex < 0) return Empty;

        var startIndex = -1;
        List<CarConsistId>? current = null;

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
                            .Select(slot => slot.CarConsistId)
                            .ToList();
                        break;

                    case StationWorkType.Decoupling when startIndex >= 0:
                        // 自Train内完結：CutPointsのうちTrainId == train.Idのものだけを残す
                        // （他Trainに切り出された側は以後の系列から除外。他Trainは検索しない）
                        current = work.CutPoints
                            .Where(cp => cp.TrainId == train.Id)
                            .OrderBy(cp => cp.Position)
                            .Select(cp => cp.CarConsistId)
                            .ToList();
                        break;

                    case StationWorkType.Coupling when startIndex >= 0:
                        // 自Train内完結：CutPoints全件をPosition順に連結する
                        // （TrainIdが自分か他人かは問わない。他Trainの中身はCarConsistIdに確定済み）
                        current = work.CutPoints
                            .OrderBy(cp => cp.Position)
                            .Select(cp => cp.CarConsistId)
                            .ToList();
                        break;
                }
            }
        }

        if (startIndex < 0 || current is null) return Empty;

        var cars = new List<CarRef>();
        foreach (var blockId in current)
        {
            if (carConsists.TryGetValue(blockId, out var block))
            {
                cars.AddRange(block.Cars);
            }
        }

        return new ResolvedConsist(current, cars);
    }

    /// <summary>
    /// Train.RunSegments（先頭のFromStationId＋各要素のToStationId）から訪問駅列を作り、
    /// 同一駅の再訪をVisitSequenceのインクリメントで区別したStopKey列を構築する（ループ線対応）。
    /// </summary>
    private static List<StopKey> BuildVisitedStopKeys(Train train)
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
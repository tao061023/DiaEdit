namespace DiaEditCore.Algorithm;

using DiaEditCore.Model;
using DiaEditCore.Model.Cars;
using DiaEditCore.Model.TimeTable.Trains;

/// <summary>
/// StartOp.startOpConsist、またはPrevTrain.SplitOrigin経由で他Trainのconsistsequenceを <br/>
/// 起点として、以降のCoupling/Decouplingイベントを時系列順にたどることで任意時点の実編成を復元する。 <br/>
/// 都度導出・非保存。 <br/>
///
/// DecouplingWork.IsRearBaseを直読みするだけで「自Trainがfront/rearどちらを引き継いだか」が一意に決まるため、全Train横断の事前計算は不要 <br/>
/// （train.StopTimes走査中にDecoupling作業へ遭遇した時点で、trainは常にそのDecouplingの継続側＝originであることが構造的に保証される。 <br/>
/// 子Train側はPrevTrain.SplitOrigin経由の別メソッド ResolveSplitOriginConsist でのみ解決されるため混線しない）。 <br/>
///
/// Couplingは相手Train（CouplingWork.PartnerTrainId）を再帰的にResolveConsistAtで解決する。
/// </summary>
public sealed record ConsistResolutionContext(
    IReadOnlyDictionary<CarConsistId, CarConsist> CarConsists,
    IReadOnlyDictionary<CarCompositionId, CarComposition> CarCompositions,
    IReadOnlyDictionary<TrainId, Train> AllTrainsById)
{
    public static ConsistResolutionContext Empty(
        IReadOnlyDictionary<CarConsistId, CarConsist> carConsists,
        IReadOnlyDictionary<CarCompositionId, CarComposition> carCompositions)
        => new(carConsists, carCompositions, new Dictionary<TrainId, Train>());
}

public static class CarConsistResolver
{
    public sealed record ResolvedConsist(
        IReadOnlyList<CarCompositionId> ConsistBlocks,
        IReadOnlyList<CarRef> Cars);

    private static readonly ResolvedConsist Empty = new(Array.Empty<CarCompositionId>(), Array.Empty<CarRef>());

    /// <summary>
    /// train自身のWorks列をStartOp（またはPrevTrain.SplitOrigin）から対象stopKeyまで時系列順にたどり、実編成を復元する。 <br/>
    /// 起点が見つからない場合、または対象stopKeyが起点より先行する場合は空を返す。
    /// </summary>
    public static ResolvedConsist ResolveConsistAt(
        Train train,
        StopKey stopKey,
        ConsistResolutionContext context)
    {
        var carConsists = context.CarConsists;
        var carCompositions = context.CarCompositions;
        var visitedKeys = StopKeySequenceBuilder.BuildVisitedStopKeys(train);
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

                    case StationWorkType.PrevTrain when startIndex < 0 && work.SplitOrigin is { } origin:
                        startIndex = i;
                        current = ResolveSplitOriginConsist(origin, context);
                        break;

                    case StationWorkType.Decoupling when startIndex >= 0:
                        // train自身がこのDecouplingのstopTimeを持つ＝trainは常に継続側（origin）。
                        // IsRearBase直読みのみで確定し、他Trainへの参照は不要。
                        if (work.DecouplingDetail is { } dw)
                        {
                            current = (dw.IsRearBase ? dw.RearGroup : dw.FrontGroup)
                                .Select(e => e.CarCompositionId)
                                .ToList();
                        }
                        break;

                    case StationWorkType.Coupling when startIndex >= 0:
                        if (work.CouplingDetail is { } cw && current is not null)
                        {
                            var partnerConsist = ResolvePartnerConsistAt(cw.PartnerTrainId, cw.PartnerStopKey, context);
                            current = cw.AttachToFront
                                ? partnerConsist.Concat(current).ToList()
                                : current.Concat(partnerConsist).ToList();
                        }
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
        SplitOriginRef origin,
        ConsistResolutionContext context)
    {
        if (!context.AllTrainsById.TryGetValue(origin.OriginTrainId, out var originTrain)) return new List<CarCompositionId>();
        if (!originTrain.StopTimes.TryGetValue(origin.OriginStopKey, out var originStop)) return new List<CarCompositionId>();

        var decoupling = originStop.Works.FirstOrDefault(w => w.Type == StationWorkType.Decoupling);
        if (decoupling?.DecouplingDetail is not { } dw) return new List<CarCompositionId>();

        // 子Train（非継続側）は継続側の「逆」のグループを引き継ぐ。
        // IsRearBase=false（front継続）なら子はRearGroup。IsRearBase=true（rear継続）なら子はFrontGroup。
        var childGroup = dw.IsRearBase ? dw.FrontGroup : dw.RearGroup;
        return childGroup.Select(e => e.CarCompositionId).ToList();
    }

    private static List<CarCompositionId> ResolvePartnerConsistAt(
        TrainId partnerTrainId,
        StopKey partnerStopKey,
        ConsistResolutionContext context)
    {
        if (!context.AllTrainsById.TryGetValue(partnerTrainId, out var partnerTrain))
            return new List<CarCompositionId>();

        // 再帰的に相手Trainを解決する。相手がさらに別のCoupling/Decouplingを経ていても
        // ResolveConsistAt自身がそれを辿るため、ここでの特別扱いは不要。
        var resolved = ResolveConsistAt(partnerTrain, partnerStopKey, context);
        return resolved.ConsistBlocks.ToList();
    }
}
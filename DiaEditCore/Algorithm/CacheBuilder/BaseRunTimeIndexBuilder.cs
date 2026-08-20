namespace DiaEditCore.Algorithm.CacheBuilder;

using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.TimeTable.Trains;

/// <summary>
/// 5.6.1節：DiagramRevision.BaseTimeTableSetIdが指すTimeTableSet内のTrain実績から、
/// 区間ごとの基準所要時分インデックスを構築する（v12.27新設：StationConnectionSegment.BaseRunTimeSec
/// 廃止に伴う代替実装）。
///
/// 選定キー：(StationConnectionSegmentId, FromIsStop, ToIsStop, DefaultVehicleTypeId)。
/// 「停車/通過パターン4種」は当該ホップの出発駅側StopTime.IsStop・到着駅側StopTime.IsStopの組み合わせ。
/// 車両性能差はTrain.DefaultVehicleTypeIdで反映し、ServiceRoute／Directionは選定キーに含めない
/// （StationConnectionSegmentは複数ServiceRouteから共有されるという既存の設計前提と矛盾するため）。
///
/// 実測所要秒数の算出基準はStopVisitOccupancyResolverの基準時刻ロジックを踏襲する：
///   出発側基準 = DepartureSeconds（停車・通過を問わず「その地点を離れる時刻」）
///   到着側基準 = IsStop ? ArrivalSeconds : DepartureSeconds（停車なら到着時刻、通過なら通過時刻）
///
/// 一意性（同一選定キーに該当するTrainが2件以上存在してはならない）はここでは検証しない
/// （検証責務はBaseTimeTableSetTrainDuplicationCrossValidator、§9.1項目21・未実装。
/// 本Builderは「後勝ち」で単純に上書きする防御的実装とし、Calculate側の呼び出し前提として
/// 保存時に一意性が保証されていることを前提とする）。
///
/// ホップ→StationConnectionSegmentIdの解決：TrainRunSegmentはStationConnectionIdのみを持つため、
/// StationConnection.SegmentsをallSegmentsと突き合わせ、FromStationId/ToStationIdが一致する
/// SCSを1件特定する（ServiceRouteToRunSegmentsResolver.ResolveHopCandidatesと対称のロジック）。
/// 一致するSCSが0件・複数件の場合はそのホップを黙って読み飛ばす（データ不整合はServiceRoute側の
/// 保存時検証で別途検出される想定。本Builderはdiscard-and-regenerateの都度導出処理のため、
/// 例外を送出せず可能な範囲でインデックスを構築する防御的実装とする）。
/// </summary>
public static class BaseRunTimeIndexBuilder
{
    public readonly record struct SelectionKey(
        StationConnectionSegmentId SegmentId,
        bool FromIsStop,
        bool ToIsStop,
        VehicleTypeId? VehicleTypeId);

    public static Dictionary<SelectionKey, int> Build(
        IReadOnlyList<Train> baseTimeTableSetTrains,
        IReadOnlyList<StationConnection> allStationConnections,
        IReadOnlyList<StationConnectionSegment> allSegments)
    {
        var index = new Dictionary<SelectionKey, int>();
        var scById = allStationConnections.ToDictionary(sc => sc.Id);

        foreach (var train in baseTimeTableSetTrains)
        {
            if (train.RunSegments.Count == 0) continue;

            var visitedKeys = StopKeySequenceBuilder.BuildVisitedStopKeys(train);
            if (visitedKeys.Count != train.RunSegments.Count + 1) continue; // 不整合データは対象外

            for (var i = 0; i < train.RunSegments.Count; i++)
            {
                var hop = train.RunSegments[i];

                if (!scById.TryGetValue(hop.StationConnectionId, out var sc)) continue;

                var scsId = ResolveHopSegmentId(sc, hop.FromStationId, hop.ToStationId, allSegments);
                if (scsId is null) continue;

                if (!train.StopTimes.TryGetValue(visitedKeys[i], out var fromStopTime)) continue;
                if (!train.StopTimes.TryGetValue(visitedKeys[i + 1], out var toStopTime)) continue;

                if (fromStopTime.DepartureSeconds < 0) continue;

                var toBasis = toStopTime.IsStop ? toStopTime.ArrivalSeconds : toStopTime.DepartureSeconds;
                if (toBasis < 0) continue;

                var elapsed = toBasis - fromStopTime.DepartureSeconds;
                if (elapsed < 0) continue; // 時刻が逆転している不整合データは対象外（保存時検証の管轄）

                var key = new SelectionKey(
                    scsId.Value,
                    fromStopTime.IsStop,
                    toStopTime.IsStop,
                    train.DefaultVehicleTypeId);

                index[key] = elapsed; // 一意性違反時は後勝ち（検証はCross Validator側の責務）
            }
        }

        return index;
    }

    /// <summary>
    /// StationConnection.Segmentsのうち、fromStationId/toStationIdが厳密に一致するSCSを1件特定する。
    /// 一致が0件・複数件の場合はnull（呼び出し側でそのホップを読み飛ばす）。
    /// public化（v12.27）：BaseTimeTableSetTrainDuplicationCrossValidatorが同一の
    /// ホップ→SCS解決ロジックを共有するため（DRY原則）。
    /// </summary>
    public static StationConnectionSegmentId? ResolveHopSegmentId(
        StationConnection sc,
        StationId fromStationId,
        StationId toStationId,
        IReadOnlyList<StationConnectionSegment> allSegments)
    {
        StationConnectionSegmentId? found = null;

        foreach (var segId in sc.Segments)
        {
            var seg = allSegments.FirstOrDefault(s => s.Id == segId);
            if (seg is null) continue;
            if (seg.FromStationId != fromStationId || seg.ToStationId != toStationId) continue;

            if (found is not null) return null; // 複数一致は不整合として扱う
            found = seg.Id;
        }

        return found;
    }
}
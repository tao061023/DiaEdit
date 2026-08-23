namespace DiaEditCore.Algorithm.CacheBuilder;

using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.TimeTable.Trains;

/// <summary>
/// DiagramRevision.BaseTimeTableSetIdが指すTimeTableSet内のTrain実績から、
/// 区間ごとの基準所要時分インデックスを構築する。
///
/// v12.29 SCS direction-agnostic renameセッションでの変更点：
/// StationConnectionSegment.StationIdA/StationIdBは無向ペアのため、
/// hop.FromStationId/ToStationIdとの一致判定を無向マッチングへ変更した。旧実装のまま
/// （StationIdA==fromStationId固定）だと、双単線区間の上り方向Trainでは常に一致せず、
/// 基準所要時分インデックスが上り列車についてだけサイレントに空になる不具合があった
/// （RunTimeCalculatorのAuto/Manualモードでの実測アンカー調整が上り列車に一切効かなくなる）。
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
            if (visitedKeys.Count != train.RunSegments.Count + 1) continue;

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
                if (elapsed < 0) continue;

                var key = new SelectionKey(
                    scsId.Value,
                    fromStopTime.IsStop,
                    toStopTime.IsStop,
                    train.DefaultVehicleTypeId);

                index[key] = elapsed;
            }
        }

        return index;
    }

    /// <summary>
    /// StationConnection.Segmentsのうち、fromStationId/toStationIdが（無向で）一致するSCSを1件特定する。
    /// 一致が0件・複数件の場合はnull（呼び出し側でそのホップを読み飛ばす）。
    /// v12.29：StationIdA/StationIdBは無向ペアのため、fromStationId/toStationIdの
    /// どちらの順序で一致してもよい（ServiceRouteToRunSegmentsResolver.ResolveHopCandidatesと同じ精神）。
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

            var matches =
                (seg.StationIdA == fromStationId && seg.StationIdB == toStationId) ||
                (seg.StationIdA == toStationId && seg.StationIdB == fromStationId);
            if (!matches) continue;

            if (found is not null) return null;
            found = seg.Id;
        }

        return found;
    }
}
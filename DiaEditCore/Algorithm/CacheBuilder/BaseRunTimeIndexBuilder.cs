namespace DiaEditCore.Algorithm.CacheBuilder;

using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.TimeTable.Trains;

/// <summary>
/// DiagramRevision.BaseTimeTableSetIdが指すTimeTableSet内のTrain実績から、
/// 区間ごとの基準所要時分インデックスを構築する。
/// </summary>
public static class BaseRunTimeIndexBuilder
{
    /// <summary>
    /// 区間ごとの基準所要時分を選択するためのキー
    /// StationConnectionSegment（物理的な駅間区間）と、
    /// 区間両端の停車条件、および車両種別を組み合わせて一意に特定する。
    /// </summary>
    /// <param name="SegmentId"></param>
    /// <param name="FromIsStop"></param>
    /// <param name="ToIsStop"></param>
    /// <param name="VehicleTypeId"></param>
    /// <remarks>
    /// 同じ区間でも、停車・通過条件や車両種別によって所要時分が異なるため、
    /// これらをキーに含めて辞書化する。
    /// </remarks>
    public readonly record struct SelectionKey(
        StationConnectionSegmentId SegmentId,
        bool FromIsStop,
        bool ToIsStop,
        VehicleTypeId? VehicleTypeId);

    /// <summary>
    /// TimeTableSet 内の Train 実績から、区間ごとの基準所要時分インデックスを構築する。
    /// </summary>
    /// <param name="baseTimeTableSetTrains">
    /// 実績を持つ Train の一覧。RunSegments と StopTimes を使用して区間の所要時分を計算する。
    /// </param>
    /// <param name="allStationConnections">
    /// StationConnectionId から物理的な駅間接続を引くための一覧。
    /// </param>
    /// <param name="allSegments">
    /// StationConnection が保持する SegmentId を解決するための全 Segment 一覧。
    /// </param>
    /// <returns>
    /// SelectionKey（区間＋停車条件＋車両種別）をキーとし、
    /// その区間の実績所要時分（秒）を値とする辞書。
    /// </returns>
    /// <remarks>
    /// 処理内容：<br/>
    /// 1. Train の RunSegments を順に処理する。<br/>
    /// 2. StopKeySequenceBuilder により停車キー列を取得し、RunSegments と整合する列車のみ採用する。<br/>
    /// 3. StationConnectionId から StationConnection を取得する。<br/>
    /// 4. ResolveHopSegmentId により、今回の区間に対応する物理 Segment を1件だけ特定する。<br/>
    /// 5. from/to の StopTimes から出発・到着（または通過）時刻を取得する。<br/>
    /// 6. 経過秒数を計算し、SelectionKey を構築して辞書に登録する。<br/>
    /// </remarks>
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
    /// StationConnection.Segments の中から、from/to の StationId に無向一致する
    /// StationConnectionSegment を1件だけ特定する。
    /// </summary>
    /// <param name="sc">
    /// 対象の StationConnection。複数の Segment を持つ場合がある。
    /// </param>
    /// <param name="fromStationId">区間の始点駅 ID。</param>
    /// <param name="toStationId">区間の終点駅 ID。</param>
    /// <param name="allSegments">SegmentId を解決するための全 Segment 一覧。</param>
    /// <returns>
    /// 一致する Segment が 1 件の場合はその SegmentId。
    /// 一致が 0 件または複数件の場合は null（区間を特定できないため）。
    /// </returns>
    /// <remarks>
    /// 駅間は無向ペア（A-B と B-A は同じ物理区間）として扱うため、
    /// StationIdA/StationIdB の順序は問わない。
    /// </remarks>
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
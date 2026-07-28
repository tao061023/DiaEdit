using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.TimeTable;

namespace DiaEditCore.Algorithm;

/// <summary>
/// Track（番線）用途のConflictChecker（6.5節）：全Trainを走査し、番線ごとの占有区間を構築する。
///
/// 占有区間 = 「到着StationPathの占有開始」〜「出発StationPathの占有終了」（v11.22変更履歴6項目目）。
/// 各訪問の到着/出発StationPath占有はStopVisitOccupancyResolverを再利用する。
///
/// 始発・終着側（片側のStationPath占有が存在しない訪問）の境界：
///   - 到着側が存在しない訪問（visitSeq==0）：TrainConnectionResolver.ResolveUniquePrevTrainMapで
///     解決した一意なPrevTrainの到着占有終了を採用。PrevTrainが解決できない場合は
///     ProjectSettings.DiagramBasedTimeSec（ダイヤグラム描画の始端）で打ち切る。
///   - 出発側が存在しない訪問（visitSeq==segs.Count）：TrainConnectionResolver.ResolveUniqueNextTrainMapで
///     解決した一意なNextTrainの出発占有開始を採用。NextTrainが解決できない場合は、
///     この訪問自体の到着占有終了をそのままTrack占有終了として採用する（後続列車が存在しない以上、
///     それ以上先まで占有を仮定する根拠がないため）。
///
/// 一意マッチングの採用理由（v11.24）：ResolveNextTrain（単一列車視点の簡易API）を列車ごとに
/// 個別呼び出しすると、複数の到着列車が同じ出発列車を候補として選びうる（非単射）。この場合、
/// 本来PrevTrain/NextTrain関係にない到着列車同士が同じ出発列車の占有開始まで境界を延長し、
/// 誤ったTrack占有重複（誤検出）を引き起こす。ResolveUniqueNextTrainMap/ResolveUniquePrevTrainMapは
/// 「1出発列車=最大1到着列車」を保証するため、この誤検出を構造的に防止できる。
/// </summary>
public static class TrackOccupancyProvider
{
    public static Dictionary<RailId, List<ConflictChecker.Occupancy>> BuildOccupancy(
        IReadOnlyList<Train> trains,
        IReadOnlyList<StationConnection> stationConnections,
        IReadOnlyList<StationConnectionSegment> allSegments,
        IReadOnlyDictionary<StationPathId, StationPath> pathsById,
        IReadOnlyDictionary<(EntryPointId, RailId), StationPathId> arrivalIndex,
        IReadOnlyDictionary<(RailId, EntryPointId), StationPathId> departureIndex,
        IReadOnlyList<Rail> rails,
        ProjectSettings projectSettings)
    {
        var result = new Dictionary<RailId, List<ConflictChecker.Occupancy>>();

        void Add(RailId railId, ConflictChecker.Occupancy occ)
        {
            if (!result.TryGetValue(railId, out var list))
                result[railId] = list = new List<ConflictChecker.Occupancy>();
            list.Add(occ);
        }

        var resolveEp = EntryPointSequenceCache.Build(stationConnections, allSegments);
        var trainDepartureIndex = TrainConnectionResolver.BuildDepartureIndex(trains);
        var prevTrainMap = TrainConnectionResolver.ResolveUniquePrevTrainMap(trains, trainDepartureIndex, projectSettings);
        var nextTrainMap = TrainConnectionResolver.ResolveUniqueNextTrainMap(trains, trainDepartureIndex, projectSettings);

        // PrevTrain解決用：各Trainの「終着側訪問(visitSeq=segs.Count)」の到着占有終了を事前計算
        var terminalArrivalEnd = new Dictionary<TrainId, int>();
        // NextTrain解決用：各Trainの「始発側訪問(visitSeq=0)」の出発占有開始を事前計算
        var initialDepartureStart = new Dictionary<TrainId, int>();

        foreach (var train in trains)
        {
            var segs = train.RunSegments;
            if (segs.Count == 0) continue;

            var initial = StopVisitOccupancyResolver.Resolve(train, 0, resolveEp, pathsById, arrivalIndex, departureIndex);
            if (initial is { DepartureStart: { } ds }) initialDepartureStart[train.Id] = ds;

            var terminal = StopVisitOccupancyResolver.Resolve(train, segs.Count, resolveEp, pathsById, arrivalIndex, departureIndex);
            if (terminal is { ArrivalEnd: { } ae }) terminalArrivalEnd[train.Id] = ae;
        }

        foreach (var train in trains)
        {
            var segs = train.RunSegments;
            if (segs.Count == 0) continue;

            for (int visitSeq = 0; visitSeq <= segs.Count; visitSeq++)
            {
                var v = StopVisitOccupancyResolver.Resolve(train, visitSeq, resolveEp, pathsById, arrivalIndex, departureIndex);
                if (v is not { } visit) continue;

                int? start = visit.ArrivalStart;
                if (start is null)
                {
                    if (prevTrainMap.TryGetValue(train.Id, out var prevTrainId) &&
                        terminalArrivalEnd.TryGetValue(prevTrainId, out var prevEnd))
                    {
                        start = prevEnd;
                    }
                    else
                    {
                        start = projectSettings.DiagramBasedTimeSec;
                    }
                }

                int? end = visit.DepartureEnd;
                if (end is null)
                {
                    if (nextTrainMap.TryGetValue(train.Id, out var nextTrainId) &&
                        initialDepartureStart.TryGetValue(nextTrainId, out var nextStart))
                    {
                        end = nextStart;
                    }
                    else
                    {
                        // NextTrainが解決できない場合：これ以上先まで占有を仮定する根拠がないため、
                        // この訪問自体の到着占有終了をそのままTrack占有終了として採用する。
                        end = visit.ArrivalEnd ?? start;
                    }
                }

                if (start is { } s && end is { } e)
                    Add(visit.TrackRailId, new ConflictChecker.Occupancy(train.Id, s, e));
            }
        }

        AddShuntingOccupancy(trains, pathsById, rails, Add);

        return result;
    }

    /// <summary>
    /// StationWork.Type == Shunting について、参照先StationPathをRailSequenceResolverで
    /// Rail列に展開し、うちRailRoll.Trackのものを対象オブジェクトとしてTrack占有に加える
    /// （入換作業中に誤ってその番線へ進入する列車との支障を検知するため。8.2節候補から復帰・スコープ内化）。
    ///
    /// 前提：StationWork.StationPathId・StartOpSeconds・EndOpSeconds（Shuntingでは両方必須、5.11.5節）。
    /// </summary>
    private static void AddShuntingOccupancy(
        IReadOnlyList<Train> trains,
        IReadOnlyDictionary<StationPathId, StationPath> pathsById,
        IReadOnlyList<Rail> rails,
        Action<RailId, ConflictChecker.Occupancy> add)
    {
        var railSequenceResolver = new RailSequenceResolver(rails);
        var trackRailIds = rails.Where(r => r.Roll == RailRoll.Track).Select(r => r.Id).ToHashSet();

        // StationPathId単位でRail列をメモ化（同じStationPathが複数のShunting作業から参照されうるため）
        var railSequenceCache = new Dictionary<StationPathId, IReadOnlyList<RailId>>();
        IReadOnlyList<RailId> ResolveRails(StationPathId spId) =>
            railSequenceCache.TryGetValue(spId, out var cached)
                ? cached
                : railSequenceCache[spId] = railSequenceResolver.Resolve(pathsById[spId]);

        foreach (var train in trains)
        {
            foreach (var stopTime in train.StopTimes.Values)
            {
                foreach (var work in stopTime.Works)
                {
                    if (work.Type != StationWorkType.Shunting) continue;
                    if (work.StationPathId is not { } spId) continue;
                    if (work.StartOpSeconds < 0 || work.EndOpSeconds < 0) continue;

                    foreach (var railId in ResolveRails(spId))
                    {
                        if (!trackRailIds.Contains(railId)) continue;
                        add(railId, new ConflictChecker.Occupancy(train.Id, work.StartOpSeconds, work.EndOpSeconds));
                    }
                }
            }
        }
    }
}
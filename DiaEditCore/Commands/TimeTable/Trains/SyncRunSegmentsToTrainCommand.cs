namespace DiaEditCore.Commands.TimeTable.Trains;

using DiaEditCore.Algorithm;
using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.TimeTable;
using DiaEditCore.Model.TimeTable.Trains;

/// <summary>Sync前のTrain状態のスナップショット（Undo用）。</summary>
public sealed record SyncRunSegmentsSnapshot(
    List<TrainRunSegment> RunSegments,
    Dictionary<StopKey, StopTime> StopTimes);

/// <summary>
/// 6.2節：Train.RunSegmentsをServiceRoute（経由MainRoute.StationOrder）へ明示的に再同期する。 <br/>
/// v12.5確定：RunSegmentsはユーザーが直接編集せず、この派生データを再生成するコマンドのみが
/// 構造を変更する。ユーザーが直接編集できるのは「使用StationConnectionの選択」（汎用属性変更
/// パターン、本コマンドの対象外）のみ。 <br/>
///
/// 孤立StopTimeの扱い（セッション確定）：新StopKey列に含まれなくなったStopTimeは、Worksの
/// 有無に関わらず無条件破棄する。他Trainからの外部参照（StopKeyReferenceIndex経由）がある
/// 場合もexecute自体はブロックしない。参照元Trainは本コマンドのAffectedIdsに含め、実在性の
/// 検証はSaveValidationRunner側のCross Validator（§9.2項目10、未実装）に委ねる。 <br/>
///
/// 個別上書きの引き継ぎ：既存Train.RunSegmentsのホップ(From,To)が新経路にも残存し、かつ
/// IsOverriddenFromTemplate=trueの場合は、そのStationConnectionId選択をそのまま引き継ぐ。
/// </summary>
public sealed class SyncRunSegmentsToTrainCommand : UndoableCommand<Train, SyncRunSegmentsSnapshot>
{
    private readonly List<TrainRunSegment> _newRunSegments;
    private readonly Dictionary<StopKey, StopTime> _newStopTimes;

    private SyncRunSegmentsToTrainCommand(
        Train train,
        IReadOnlySet<ObjectId> affectedIds,
        List<TrainRunSegment> newRunSegments,
        Dictionary<StopKey, StopTime> newStopTimes)
        : base(train, affectedIds)
    {
        _newRunSegments = newRunSegments;
        _newStopTimes = newStopTimes;
    }

    /// <summary>
    /// 同期計画（新RunSegments・新StopTimes・AffectedIds）を確定させたうえでコマンドを生成する。
    /// ServiceRouteSegmentに対応するStationConnectionが0件／複数件で一意に決まらない場合は
    /// InvalidOperationExceptionを送出し、コマンド生成自体を失敗させる（Execute()には進ませない）。
    /// </summary>
    public static SyncRunSegmentsToTrainCommand Create(
        Train train,
        ServiceRoute serviceRoute,
        IReadOnlyList<MainRoute> allMainRoutes,
        IReadOnlyList<StationConnection> allStationConnections,
        IReadOnlyList<StationConnectionSegment> allSegments,
        TimeTableSetCache cache)
    {
        var newRunSegments = BuildNewRunSegments(train, serviceRoute, allMainRoutes, allStationConnections, allSegments);
        var (newStopTimes, orphanedKeys) = BuildNewStopTimes(train, newRunSegments);
        var affectedIds = BuildAffectedIds(train, orphanedKeys, cache);

        return new SyncRunSegmentsToTrainCommand(train, affectedIds, newRunSegments, newStopTimes);
    }

    protected override SyncRunSegmentsSnapshot CaptureSnapshot(Train target) =>
        new(
            new List<TrainRunSegment>(target.RunSegments),
            new Dictionary<StopKey, StopTime>(target.StopTimes));

    protected override void Apply(Train target)
    {
        target.RunSegments = _newRunSegments;
        target.StopTimes = _newStopTimes;
    }

    protected override void Restore(Train target, SyncRunSegmentsSnapshot snapshot)
    {
        target.RunSegments = snapshot.RunSegments;
        target.StopTimes = snapshot.StopTimes;
    }

    // -----------------------------
    // 静的ヘルパー（internal static：テストからの直接呼び出しを想定。CarConsistResolver等と同じ方針）
    // -----------------------------

    internal static List<TrainRunSegment> BuildNewRunSegments(
        Train train,
        ServiceRoute serviceRoute,
        IReadOnlyList<MainRoute> allMainRoutes,
        IReadOnlyList<StationConnection> allStationConnections,
        IReadOnlyList<StationConnectionSegment> allSegments)
    {
        var existingByHop = new Dictionary<(StationId From, StationId To), TrainRunSegment>();
        foreach (var seg in train.RunSegments)
        {
            existingByHop[(seg.FromStationId, seg.ToStationId)] = seg;
        }

        var result = new List<TrainRunSegment>();

        foreach (var srSegment in serviceRoute.Segments)
        {
            var mainRoute = allMainRoutes.FirstOrDefault(mr => mr.Id == srSegment.MainRouteId)
                ?? throw new InvalidOperationException(
                    $"SyncRunSegmentsToTrainCommand: MainRouteId {srSegment.MainRouteId} が見つかりません");

            var stationOrder = mainRoute.StationOrder;
            var fromIdx = srSegment.FromStationIndex;
            var toIdx = srSegment.ToStationIndex;
            var stepDown = fromIdx < toIdx;

            var stationConnectionId = ResolveSegmentStationConnectionId(
                srSegment, allMainRoutes, allStationConnections, allSegments);

            var i = fromIdx;
            while (i != toIdx)
            {
                var nextI = stepDown ? i + 1 : i - 1;
                var fromStationId = stationOrder[i];
                var toStationId = stationOrder[nextI];

                TrainRunSegment newSeg;
                if (existingByHop.TryGetValue((fromStationId, toStationId), out var existing)
                    && existing.IsOverriddenFromTemplate)
                {
                    newSeg = existing; // 個別上書きを引き継ぐ
                }
                else
                {
                    newSeg = new TrainRunSegment
                    {
                        FromStationId = fromStationId,
                        ToStationId = toStationId,
                        StationConnectionId = stationConnectionId,
                        IsOverriddenFromTemplate = false
                    };
                }

                result.Add(newSeg);
                i = nextI;
            }
        }

        return result;
    }

    internal static (Dictionary<StopKey, StopTime> NewStopTimes, List<StopKey> OrphanedKeys) BuildNewStopTimes(
        Train train,
        List<TrainRunSegment> newRunSegments)
    {
        // StopKeySequenceBuilderはTrain.RunSegmentsしか参照しないため、他フィールドは元のtrainから
        // そのままコピーした使い捨てインスタンスで代用する。
        var probe = new Train
        {
            Id = train.Id,
            TimeTableSetId = train.TimeTableSetId,
            TrainNumber = train.TrainNumber,
            ServiceRouteId = train.ServiceRouteId,
            TrainTypeId = train.TrainTypeId,
            TrainTypeName = train.TrainTypeName,
            Nickname = train.Nickname,
            RunSegments = newRunSegments,
        };

        var newKeys = StopKeySequenceBuilder.BuildVisitedStopKeys(probe);
        var newKeySet = new HashSet<StopKey>(newKeys);

        var newStopTimes = new Dictionary<StopKey, StopTime>();
        foreach (var key in newKeys)
        {
            newStopTimes[key] = train.StopTimes.TryGetValue(key, out var existing)
                ? existing            // 残存キーは引き継ぐ
                : new StopTime();     // 新規キーはデフォルト値
        }

        var orphanedKeys = train.StopTimes.Keys
            .Where(oldKey => !newKeySet.Contains(oldKey))
            .ToList();               // 無条件破棄対象（Works含む）

        return (newStopTimes, orphanedKeys);
    }

    internal static IReadOnlySet<ObjectId> BuildAffectedIds(
        Train train,
        List<StopKey> orphanedKeys,
        TimeTableSetCache cache)
    {
        var changedIds = new HashSet<ObjectId> { new TrainObjectId(train.Id) };
        var affected = new HashSet<ObjectId>(DependencyResolver.ResolveAffected(changedIds, cache));

        foreach (var orphanedKey in orphanedKeys)
        {
            if (cache.StopKeyReferenceIndex.TryGetValue((train.Id, orphanedKey), out var referrers))
            {
                foreach (var referrer in referrers)
                {
                    affected.Add(new TrainObjectId(referrer.ReferrerTrainId));
                }
            }
        }

        return affected;
    }

    private static StationConnectionId ResolveSegmentStationConnectionId(
        ServiceRouteSegment srSegment,
        IReadOnlyList<MainRoute> allMainRoutes,
        IReadOnlyList<StationConnection> allStationConnections,
        IReadOnlyList<StationConnectionSegment> allSegments)
    {
        if (srSegment.SelectedStationConnectionId is { } selected)
        {
            return selected;
        }

        var candidates = BoundaryEntryPointResolver.ResolveBoundaryStationConnection(
            srSegment.MainRouteId, srSegment.FromStationIndex, srSegment.ToStationIndex,
            allMainRoutes, allStationConnections, allSegments);

        return candidates.Count switch
        {
            1 => candidates[0],
            0 => throw new InvalidOperationException(
                $"SyncRunSegmentsToTrainCommand: MainRoute {srSegment.MainRouteId} の区間 " +
                $"{srSegment.FromStationIndex}→{srSegment.ToStationIndex} に対応する" +
                $"StationConnectionが存在しません"),
            _ => throw new InvalidOperationException(
                $"SyncRunSegmentsToTrainCommand: MainRoute {srSegment.MainRouteId} の区間 " +
                $"{srSegment.FromStationIndex}→{srSegment.ToStationIndex} は複数の" +
                $"StationConnection候補があるため、ServiceRouteSegment.SelectedStationConnectionId" +
                $"の明示設定が必要です")
        };
    }
}
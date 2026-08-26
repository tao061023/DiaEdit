namespace DiaEditCore.Algorithm.Conflicts;

using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.TimeTable.Trains;

/// <summary>
/// 1回の駅訪問(visitSeq)について、到着StationPath/出発StationPathそれぞれの占有区間 <br/>
/// (StationPathId・開始秒・終了秒)を導出する共通ロジック。 <br/>
/// StationPathOccupancyProvider・TrackOccupancyProviderの両方から同一の算出式を共有するために切り出した。 <br/>
///
/// 基準時刻の統一：停車時はarrivalSeconds/departureSeconds、通過時はdepartureSecondsを通過時刻として流用する。いずれも未設定(-1)なら対象外。
///
/// v12.29追加修正：arrivalEp/departureEpの取得を、resolveEpが返す配列の位置決め打ち
/// （[0]・[^1]）から、そのホップ自身の発着駅（TrainRunSegment.FromStationId/ToStationId）と
/// 一致する要素を検索する方式へ変更した。広域SC（複数ホップを1つのStationConnectionが
/// カバーする構成。ServiceRouteToRunSegmentsResolverが正式サポート）では、resolveEpが
/// 2件以上の要素を返すため、位置決め打ちだと中間駅で隣接ホップのEntryPointを誤って
/// 取得してしまうバグがあった（例：A→B→CをカバーするSCで、B駅到着時に本来必要な
/// A→B側の到着EPではなく、B→C側の到着EP（C駅側）を誤取得していた）。
/// FirstOrDefaultが一致要素を見つけられない場合（データ不整合等）はnullを返し、
/// 占有情報なしとして扱う（例外は投げない）。
/// </summary>
public static class StopVisitOccupancyResolver
{
    public readonly record struct VisitOccupancy(
        RailId TrackRailId,
        EntryPointId? ArrivalEp,
        StationPathId? ArrivalSpId,
        int? ArrivalStart,
        int? ArrivalEnd,
        EntryPointId? DepartureEp,
        StationPathId? DepartureSpId,
        int? DepartureStart,
        int? DepartureEnd);

    /// <summary>
    /// train.RunSegmentsに対するvisitSeq(0..segs.Count)を1つ受け取り、その訪問のStationPath占有情報を返す。 <br/>
    /// 対象のStopTimeが存在しない、またはTrackRailIdが未設定の場合はnullを返す。
    /// </summary>
    public static VisitOccupancy? Resolve(
        Train train,
        int visitSeq,
        Func<StationConnectionId, IReadOnlyList<EntryPointSequenceElement>> resolveEp,
        IReadOnlyDictionary<StationPathId, StationPath> pathsById,
        IReadOnlyDictionary<(EntryPointId, RailId), StationPathId> arrivalIndex,
        IReadOnlyDictionary<(RailId, EntryPointId), StationPathId> departureIndex)
    {
        var segs = train.RunSegments;
        var visitedKeys = StopKeySequenceBuilder.BuildVisitedStopKeys(train);
        if (visitSeq < 0 || visitSeq >= visitedKeys.Count) return null;
        var stopKey = visitedKeys[visitSeq];
        if (!train.StopTimes.TryGetValue(stopKey, out var st)) return null;
        if (st.TrackRailId is not { } trackRailId) return null;

        EntryPointId? arrivalEp = null;
        if (visitSeq > 0)
        {
            var hopPrev = segs[visitSeq - 1];
            var seq = resolveEp(hopPrev.StationConnectionId);
            var element = seq.FirstOrDefault(e =>
                e.FromStationId == hopPrev.FromStationId && e.ToStationId == hopPrev.ToStationId);
            arrivalEp = element?.ToEntryPointId;
            // 広域SC（複数ホップを1つのStationConnectionがカバーする構成）では、
            // seqが2件以上返りうるため、配列の末尾([^1])を機械的に取るのではなく、
            // このホップ自身のFrom/Toと一致する要素を明示的に検索する必要がある
            // （v12.29追加修正：末尾固定だと広域SCの中間駅で隣接ホップのEPを誤って
            // 取得してしまうバグがあった）。
        }

        EntryPointId? departureEp = null;
        if (visitSeq < segs.Count)
        {
            var hopNext = segs[visitSeq];
            var seq = resolveEp(hopNext.StationConnectionId);
            var element = seq.FirstOrDefault(e =>
                e.FromStationId == hopNext.FromStationId && e.ToStationId == hopNext.ToStationId);
            departureEp = element?.FromEntryPointId;
        }

        int? arrivalBasis = st.IsStop
            ? (st.ArrivalSeconds >= 0 ? st.ArrivalSeconds : (int?)null)
            : (st.DepartureSeconds >= 0 ? st.DepartureSeconds : (int?)null);
        int? departureBasis = st.DepartureSeconds >= 0 ? st.DepartureSeconds : (int?)null;

        StationPathId? arrSpId = null;
        int? arrStart = null, arrEnd = null;
        if (arrivalEp is { } aep && arrivalBasis is { } ab &&
            arrivalIndex.TryGetValue((aep, trackRailId), out var foundArrSpId))
        {
            arrSpId = foundArrSpId;
            var adj = pathsById[foundArrSpId].AdjustmentSec;
            arrStart = ab - adj;
            arrEnd = ab;
        }

        StationPathId? depSpId = null;
        int? depStart = null, depEnd = null;
        if (departureEp is { } dep && departureBasis is { } db &&
            departureIndex.TryGetValue((trackRailId, dep), out var foundDepSpId))
        {
            depSpId = foundDepSpId;
            var adj = pathsById[foundDepSpId].AdjustmentSec;
            depStart = db;
            depEnd = db + adj;
        }

        return new VisitOccupancy(
            trackRailId,
            arrivalEp, arrSpId, arrStart, arrEnd,
            departureEp, depSpId, depStart, depEnd);
    }
}
using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.TimeTable.Trains;

namespace DiaEditCore.Algorithm.Conflicts;

/// <summary>
/// 1回の駅訪問(visitSeq)について、到着StationPath/出発StationPathそれぞれの占有区間 <br/>
/// (StationPathId・開始秒・終了秒)を導出する共通ロジック。 <br/>
/// StationPathOccupancyProvider・TrackOccupancyProviderの両方から同一の算出式を共有するために切り出した。 <br/>
///
/// 基準時刻の統一：停車時はarrivalSeconds/departureSeconds、通過時はdepartureSecondsを通過時刻として流用する。いずれも未設定(-1)なら対象外。
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

        EntryPointId? arrivalEp = visitSeq > 0
            ? resolveEp(segs[visitSeq - 1].StationConnectionId)[^1].ToEntryPointId
            : null;
        EntryPointId? departureEp = visitSeq < segs.Count
            ? resolveEp(segs[visitSeq].StationConnectionId)[0].FromEntryPointId
            : null;

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
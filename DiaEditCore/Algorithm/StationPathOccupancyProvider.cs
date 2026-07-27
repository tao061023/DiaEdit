using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.TimeTable;

namespace DiaEditCore.Algorithm;

/// <summary>
/// 全Trainを走査し、StationPathごとの占有区間（ConflictChecker.Occupancy）を構築する。
/// 仮列車（IsProvisional=true）も対象に含める（6.5節・「仮列車も対象に含める」）。
///
/// 基準時刻の統一：
///   - 停車列車：arrivalSeconds（到着StationPath側）／departureSeconds（出発StationPath側）
///   - 通過列車：departureSeconds を通過時刻として扱う（5.11.4節）。未設定（推定アルゴリズム未実装）
///     の場合は対象外。
/// いずれも「基準時刻からStationPath.AdjustmentSec分オフセットした区間」という同一の式に
/// 正規化できるため、停車・通過を分岐なく扱う。
/// </summary>
public static class StationPathOccupancyProvider
{
    public static Dictionary<StationPathId, List<ConflictChecker.Occupancy>> BuildOccupancy(
        IReadOnlyList<Train> trains,
        IReadOnlyList<StationConnection> stationConnections,
        IReadOnlyList<StationConnectionSegment> allSegments,
        IReadOnlyDictionary<StationPathId, StationPath> pathsById,
        IReadOnlyDictionary<(EntryPointId, RailId), StationPathId> arrivalIndex,
        IReadOnlyDictionary<(RailId, EntryPointId), StationPathId> departureIndex)
    {
        var result = new Dictionary<StationPathId, List<ConflictChecker.Occupancy>>();

        void Add(StationPathId spId, ConflictChecker.Occupancy occ)
        {
            if (!result.TryGetValue(spId, out var list))
                result[spId] = list = new List<ConflictChecker.Occupancy>();
            list.Add(occ);
        }

        var epCache = new Dictionary<StationConnectionId, IReadOnlyList<EntryPointSequenceElement>>();
        IReadOnlyList<EntryPointSequenceElement> ResolveEp(StationConnectionId scId) =>
            epCache.TryGetValue(scId, out var cached) ? cached :
            epCache[scId] = EntryPointSequenceResolver.Resolve(
                stationConnections.First(s => s.Id == scId), allSegments);

        foreach (var train in trains)
        {
            var segs = train.RunSegments;
            if (segs.Count == 0) continue;

            for (int visitSeq = 0; visitSeq <= segs.Count; visitSeq++)
            {
                var stationId = visitSeq == 0 ? segs[0].FromStationId : segs[visitSeq - 1].ToStationId;
                if (!train.StopTimes.TryGetValue(new StopKey(stationId, visitSeq), out var st)) continue;
                if (st.TrackRailId is not { } trackRailId) continue;

                EntryPointId? arrivalEp = visitSeq > 0
                    ? ResolveEp(segs[visitSeq - 1].StationConnectionId)[^1].ToEntryPointId
                    : null;
                EntryPointId? departureEp = visitSeq < segs.Count
                    ? ResolveEp(segs[visitSeq].StationConnectionId)[0].FromEntryPointId
                    : null;

                // 基準時刻：停車時はarrivalSeconds/departureSeconds、通過時はdepartureSecondsを
                // 通過時刻として流用する（5.11.4節）。いずれも未設定（-1）なら対象外。
                int? arrivalBasis = st.IsStop
                    ? (st.ArrivalSeconds >= 0 ? st.ArrivalSeconds : (int?)null)
                    : (st.DepartureSeconds >= 0 ? st.DepartureSeconds : (int?)null);
                int? departureBasis = st.DepartureSeconds >= 0 ? st.DepartureSeconds : (int?)null;

                if (arrivalEp is { } aep && arrivalBasis is { } ab &&
                    arrivalIndex.TryGetValue((aep, trackRailId), out var arrSpId))
                {
                    int adj = pathsById[arrSpId].AdjustmentSec;
                    Add(arrSpId, new ConflictChecker.Occupancy(train.Id, ab - adj, ab));
                }

                if (departureEp is { } dep && departureBasis is { } db &&
                    departureIndex.TryGetValue((trackRailId, dep), out var depSpId))
                {
                    int adj = pathsById[depSpId].AdjustmentSec;
                    Add(depSpId, new ConflictChecker.Occupancy(train.Id, db, db + adj));
                }
            }
        }

        return result;
    }
}
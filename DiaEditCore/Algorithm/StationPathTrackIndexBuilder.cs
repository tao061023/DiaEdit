using DiaEditCore.Model;
using DiaEditCore.Model.Stations;

namespace DiaEditCore.Algorithm;

/// <summary>
/// 5.5.4節の具体化：StationPathは番線自体をwaypointsに含まないため、
/// StopTime.TrackRailIdから対応する到着/出発StationPathを一意に逆引きするための
/// インデックスを構築する。
///
/// 到着StationPathの終端waypoint（BoundaryPoint、または棒線駅ではEntryPoint自身）が、
/// 対象Track Rail（Roll=Track）のいずれかの端点と一致することで対応付けを行う。
/// これによりHalt駅（棒線駅）・通常駅を分岐なく統一的に扱える。
///
/// 構築タイミング：StationPathSuggesterによる自動生成時・手動登録時の両方
/// （呼び出し側＝TimeTableSetCache側の責務。DepartureByStationTrackIndex等の既存
/// インデックスと同じ分離方針）。
/// </summary>
public static class StationPathTrackIndexBuilder
{
    private static RailEndpointRef ToEndpointRef(StationPathWaypoint wp) => wp switch
    {
        BoundaryPointWaypoint x => new BoundaryPointEndpointRef(x.Id),
        EntryPointWaypoint x => new EntryPointEndpointRef(x.Id),
        BufferStopWaypoint x => new BufferStopEndpointRef(x.Id),
        _ => throw new InvalidOperationException(
            $"StationPathの終端に不正なwaypoint種別が指定されています: {wp.GetType().Name}"),
    };

    private static Rail? FindTrackRail(IReadOnlyList<Rail> rails, RailEndpointRef terminal) =>
        rails.FirstOrDefault(r => r.Roll == RailRoll.Track &&
            (r.EndpointA == terminal || r.EndpointB == terminal));

    public static (
        Dictionary<(EntryPointId ArrivalEp, RailId TrackRailId), StationPathId> ArrivalIndex,
        Dictionary<(RailId TrackRailId, EntryPointId DepartureEp), StationPathId> DepartureIndex
    ) Build(IReadOnlyList<StationPath> allPaths, IReadOnlyList<Rail> allRails)
    {
        var arrivalIndex = new Dictionary<(EntryPointId, RailId), StationPathId>();
        var departureIndex = new Dictionary<(RailId, EntryPointId), StationPathId>();

        foreach (var sp in allPaths)
        {
            if (sp.Direction == StationPathDirection.Arrival)
            {
                // 5.5.2節：到着用パターンは必ずEntryPoint始点
                if (sp.Waypoints.Count == 0 || sp.Waypoints[0] is not EntryPointWaypoint epw) continue;

                var terminal = ToEndpointRef(sp.Waypoints[^1]);
                var trackRail = FindTrackRail(allRails, terminal);
                if (trackRail is not null)
                    arrivalIndex[(epw.Id, trackRail.Id)] = sp.Id;
            }
            else if (sp.Direction == StationPathDirection.Departure)
            {
                // 5.5.2節：出発用パターンは必ずEntryPoint終点
                if (sp.Waypoints.Count == 0 || sp.Waypoints[^1] is not EntryPointWaypoint epw) continue;

                var terminal = ToEndpointRef(sp.Waypoints[0]);
                var trackRail = FindTrackRail(allRails, terminal);
                if (trackRail is not null)
                    departureIndex[(trackRail.Id, epw.Id)] = sp.Id;
            }
            // Shunting方向は対象外（引き上げ線用等、番線occupancy用途には無関係）
        }

        return (arrivalIndex, departureIndex);
    }
}
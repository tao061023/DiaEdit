using DiaEditCore.Model;
using DiaEditCore.Model.Stations;

namespace DiaEditCore.Algorithm;

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

    // ===== 既存シグネチャ・実装は完全に元のまま（変更なし） =====
    public static (
        Dictionary<(EntryPointId, RailId), StationPathId> ArrivalIndex,
        Dictionary<(RailId, EntryPointId), StationPathId> DepartureIndex
    ) Build(IReadOnlyList<StationPath> allPaths, IReadOnlyList<Rail> allRails)
    {
        var arrivalIndex = new Dictionary<(EntryPointId, RailId), StationPathId>();
        var departureIndex = new Dictionary<(RailId, EntryPointId), StationPathId>();

        foreach (var sp in allPaths)
        {
            if (sp.Direction == StationPathDirection.Arrival)
            {
                if (sp.Waypoints.Count == 0 || sp.Waypoints[0] is not EntryPointWaypoint epw) continue;
                var trackRail = FindTrackRail(allRails, ToEndpointRef(sp.Waypoints[^1]));
                if (trackRail is not null)
                    arrivalIndex[(epw.Id, trackRail.Id)] = sp.Id;
            }
            else if (sp.Direction == StationPathDirection.Departure)
            {
                if (sp.Waypoints.Count == 0 || sp.Waypoints[^1] is not EntryPointWaypoint epw) continue;
                var trackRail = FindTrackRail(allRails, ToEndpointRef(sp.Waypoints[0]));
                if (trackRail is not null)
                    departureIndex[(trackRail.Id, epw.Id)] = sp.Id;
            }
            // Shunting方向は対象外（既存仕様のまま）
        }

        return (arrivalIndex, departureIndex);
    }

    // ===== 新設：5.7/5.8節 MainRouteChecker専用。Shunting込み・汎用境界表現版 =====
    public readonly record struct BoundaryTerminal(string Kind, int Id)
    {
        public static BoundaryTerminal Of(StationPathWaypoint wp)
        {
            var (kind, id) = wp.Key();
            return new BoundaryTerminal(kind, id);
        }

        public static BoundaryTerminal FromEntryPoint(EntryPointId id) => new("EntryPoint", id.Value);

        public static implicit operator BoundaryTerminal(EntryPointId id) => FromEntryPoint(id);
    }

    public static (
        Dictionary<(BoundaryTerminal Terminal, RailId TrackRailId), StationPathId> ArrivalIndex,
        Dictionary<(RailId TrackRailId, BoundaryTerminal Terminal), StationPathId> DepartureIndex
    ) BuildWithBoundaryTerminals(IReadOnlyList<StationPath> allPaths, IReadOnlyList<Rail> allRails)
    {
        var arrivalIndex = new Dictionary<(BoundaryTerminal, RailId), StationPathId>();
        var departureIndex = new Dictionary<(RailId, BoundaryTerminal), StationPathId>();

        foreach (var sp in allPaths)
        {
            if (sp.Direction == StationPathDirection.Arrival)
            {
                if (sp.Waypoints.Count == 0 || sp.Waypoints[0] is not EntryPointWaypoint epw) continue;
                var trackRail = FindTrackRail(allRails, ToEndpointRef(sp.Waypoints[^1]));
                if (trackRail is not null)
                    arrivalIndex[(BoundaryTerminal.FromEntryPoint(epw.Id), trackRail.Id)] = sp.Id;
            }
            else if (sp.Direction == StationPathDirection.Departure)
            {
                if (sp.Waypoints.Count == 0 || sp.Waypoints[^1] is not EntryPointWaypoint epw) continue;
                var trackRail = FindTrackRail(allRails, ToEndpointRef(sp.Waypoints[0]));
                if (trackRail is not null)
                    departureIndex[(trackRail.Id, BoundaryTerminal.FromEntryPoint(epw.Id))] = sp.Id;
            }
            else if (sp.Direction == StationPathDirection.Shunting)
            {
                if (sp.Waypoints.Count < 2) continue;
                var head = sp.Waypoints[0];
                var tail = sp.Waypoints[^1];

                var headTrackRail = FindTrackRail(allRails, ToEndpointRef(head));
                if (headTrackRail is not null)
                    departureIndex[(headTrackRail.Id, BoundaryTerminal.Of(tail))] = sp.Id;

                var tailTrackRail = FindTrackRail(allRails, ToEndpointRef(tail));
                if (tailTrackRail is not null)
                    arrivalIndex[(BoundaryTerminal.Of(head), tailTrackRail.Id)] = sp.Id;
            }
        }

        return (arrivalIndex, departureIndex);
    }
}
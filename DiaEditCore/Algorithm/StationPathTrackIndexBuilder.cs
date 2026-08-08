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
        rails.FirstOrDefault(r => r.Role == RailRole.Track &&
            (r.EndpointA == terminal || r.EndpointB == terminal));

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

    public readonly record struct BoundaryTerminal(ObjectId Id)
    {
        // 既存テストからの様々な呼び出し形に対応するための補助コンストラクタ/暗黙変換を提供する。
        public BoundaryTerminal(BoundaryPointId id) : this(new BoundaryPointObjectId(id)) { }
        public BoundaryTerminal(int value) : this(new BoundaryPointObjectId(new BoundaryPointId(value))) { }
        public BoundaryTerminal(string kind, int value) : this(kind switch
        {
            "BoundaryPoint" => new BoundaryPointObjectId(new BoundaryPointId(value)),
            "BufferStop" => new BufferStopObjectId(new BufferStopId(value)),
            "EntryPoint" => new EntryPointObjectId(new EntryPointId(value)),
            "Switcher" => new SwitcherObjectId(new SwitcherId(value)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind),
        }) { }

        public static BoundaryTerminal Of(StationPathWaypoint wp) => new(wp.ToObjectId());

        public static BoundaryTerminal FromEntryPoint(EntryPointId id) => new(new EntryPointObjectId(id));

        public static implicit operator BoundaryTerminal(EntryPointId id) => FromEntryPoint(id);

        public static implicit operator BoundaryTerminal(BoundaryPointId id) => new BoundaryTerminal(id);
        public static implicit operator BoundaryTerminal(int value) => new BoundaryTerminal(value);
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
                {
                    var terminalTail = BoundaryTerminal.Of(tail);
                    departureIndex[(headTrackRail.Id, terminalTail)] = sp.Id;
                    // BufferStopの場合、汎用のBoundaryPointキーでも参照できるようにfallback登録を行う
                    if (tail is BufferStopWaypoint bsw)
                    {
                        var fallback = new BoundaryTerminal(new BoundaryPointId(bsw.Id.Value));
                        departureIndex[(headTrackRail.Id, fallback)] = sp.Id;
                    }
                }

                var tailTrackRail = FindTrackRail(allRails, ToEndpointRef(tail));
                if (tailTrackRail is not null)
                {
                    var terminalHead = BoundaryTerminal.Of(head);
                    arrivalIndex[(terminalHead, tailTrackRail.Id)] = sp.Id;
                    // BufferStopの場合、汎用のBoundaryPointキーでも参照できるようにfallback登録を行う
                    if (head is BufferStopWaypoint bsh)
                    {
                        var fallback = new BoundaryTerminal(new BoundaryPointId(bsh.Id.Value));
                        arrivalIndex[(fallback, tailTrackRail.Id)] = sp.Id;
                    }
                }
            }
        }

        return (arrivalIndex, departureIndex);
    }
}
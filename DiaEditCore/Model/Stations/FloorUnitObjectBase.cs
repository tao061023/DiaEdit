namespace DiaEditCore.Model.Stations;

public sealed class FloorUnitObjectBase
{
    public required FloorUnitId FloorUnitId { get; set; }
    public required Point Position { get; set; }
}

public static class FloorObjectRefExtensions
{
    // NoneEndpointRefはどの実体も指さないためnullを返す。
    public static ObjectId? ToObjectId(this RailEndpointRef r) => r switch
    {
        BoundaryPointEndpointRef b => new BoundaryPointObjectId(b.Id),
        EntryPointEndpointRef e => new EntryPointObjectId(e.Id),
        BufferStopEndpointRef bs => new BufferStopObjectId(bs.Id),
        SwitcherEndpointRef sw => new SwitcherObjectId(sw.Id),
        NoneEndpointRef => null,
        null => null,
        // RailEndpointRefはabstract・非sealedのため、コンパイラは派生型の全列挙を証明できない。
        // 既知の派生型を全て網羅した上での防御的フォールバック（真の網羅性保証ではない）。
        not null => throw new ArgumentOutOfRangeException(nameof(r), r, "未知のRailEndpointRef派生型"),
    };
 
    public static ObjectId ToObjectId(this StationPathWaypoint w) => w switch
    {
        BoundaryPointWaypoint b => new BoundaryPointObjectId(b.Id),
        EntryPointWaypoint e => new EntryPointObjectId(e.Id),
        SwitcherWaypoint sw => new SwitcherObjectId(sw.Id),
        BufferStopWaypoint bs => new BufferStopObjectId(bs.Id),
        null => throw new ArgumentNullException(nameof(w)),
        // StationPathWaypointもabstract・非sealedのため、同様に防御的フォールバックが必要
        not null => throw new ArgumentOutOfRangeException(nameof(w), w, "未知のStationPathWaypoint派生型"),
    };
}
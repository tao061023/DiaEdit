namespace DiaEditCore.Model.Stations;

public sealed class FloorUnitObjectBase
{
    public required FloorUnitId FloorUnitId { get; set; }
    public required Point Position { get; set; }
}

public static class FloorObjectRefExtensions
{
    // NoneEndpointRefはどの実体も指さないためnullを返す。
    // 「None同士は常に一致する」という旧Key()実装の挙動（("None",-1)==("None",-1)）は
    // 意図的に踏襲しない。None同士の一致を「同一地点を共有している」と解釈するのは誤りであるため。
    public static ObjectId? ToObjectId(this RailEndpointRef r) => r switch
    {
        BoundaryPointEndpointRef b => new BoundaryPointObjectId(b.Id),
        EntryPointEndpointRef e => new EntryPointObjectId(e.Id),
        BufferStopEndpointRef bs => new BufferStopObjectId(bs.Id),
        SwitcherEndpointRef sw => new SwitcherObjectId(sw.Id),
        // PortIndexはここでは捨てる。RailSequenceResolver/ReversalResolverの用途は
        // 「同一の物理的収束点を共有しているか」の判定であり、どのポート経由かは関心事ではない
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
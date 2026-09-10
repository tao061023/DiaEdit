namespace DiaEditCore.Model.Stations.FloorUnitObjects;

/// <summary>
/// 駅階層を識別する FloorUnitId と、平面上の座標を保持する基本オブジェクト。
/// </summary>
/// <remarks>
/// 本クラスは駅構内オブジェクトの基底情報として利用される。
/// FloorUnitId と Position は必須であり、null を許容しない。
/// </remarks>
public sealed class FloorUnitObjectBase
{
    /// <summary>
    /// 駅階層を識別する ID。
    /// </summary>
    public required FloorUnitId FloorUnitId { get; set; }

    /// <summary>
    /// 駅階層内での座標 (X, Y)。
    /// </summary>
    public required Point Position { get; set; }
}

public static class FloorObjectRefExtensions
{
    /// <summary>
    /// RailEndpointRef を対応する ObjectId に変換する。
    /// </summary>
    /// <remarks>
    /// RailEndpointRef は abstract かつ非 sealed のため、既知の派生型のみを変換対象とする。
    /// 未知の派生型が渡された場合は例外を送出する。
    /// null は変換不能として null を返す。
    /// </remarks>
    /// <param name="r">変換対象となる RailEndpointRef。</param>
    /// <returns>対応する ObjectId。未知型の場合は例外、null の場合は null。</returns>
    public static ObjectId? ToObjectId(this RailEndpointRef r) => r switch
    {
        BoundaryPointEndpointRef b => new BoundaryPointObjectId(b.Id),
        EntryPointEndpointRef e => new EntryPointObjectId(e.Id),
        BufferStopEndpointRef bs => new BufferStopObjectId(bs.Id),
        SwitcherEndpointRef sw => new SwitcherObjectId(sw.Id),
        NoneEndpointRef n => new NoneEndpointObjectId(n.Id),
        null => null,
        not null => throw new ArgumentOutOfRangeException(nameof(r), r, "未知のRailEndpointRef派生型"),
    };

    /// <summary>
    /// StationPathWaypoint を対応する ObjectId に変換する。
    /// </summary>
    /// <remarks>
    /// StationPathWaypoint も abstract・非 sealed のため、既知の派生型のみを変換対象とする。
    /// null は許容されず、必ず例外を送出する。
    /// 未知の派生型が渡された場合も例外を送出する。
    /// </remarks>
    /// <param name="w">変換対象となる StationPathWaypoint。</param>
    /// <returns>対応する ObjectId。null の場合は例外。</returns>
    public static ObjectId ToObjectId(this StationPathWaypoint w) => w switch
    {
        BoundaryPointWaypoint b => new BoundaryPointObjectId(b.Id),
        EntryPointWaypoint e => new EntryPointObjectId(e.Id),
        SwitcherWaypoint sw => new SwitcherObjectId(sw.Id),
        BufferStopWaypoint bs => new BufferStopObjectId(bs.Id),
        null => throw new ArgumentNullException(nameof(w)),
        not null => throw new ArgumentOutOfRangeException(nameof(w), w, "未知のStationPathWaypoint派生型"),
    };
}

namespace DiaEditCore.Model.Stations.FloorUnitObjects;

/// <summary>
/// 構内進路の方向種別
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description><c>Arrival</c>：到着用</description></item>
/// <item><description><c>Departure</c>：出発用</description></item>
/// <item><description><c>Shunting</c>：入替作業用</description></item>
/// </list>
/// 単線等で進行方向が固定されている場合があり、EntryPointの種別から解決することが不可能なため、有向としている。<br/>
/// Shuntingの向きは移動元、移動先から判定する。詳しくは<see cref="TimeTable.Trains.StationWork"/>を参照。
/// </remarks>
public enum StationPathDirection { Arrival, Departure, Shunting }

/// <summary>
/// 進路オブジェクトでRail端点を参照するための抽象基底型
/// </summary>
/// <remarks>
/// <see cref="RailEndpointRef"/>とは異なり、StationPathとして参照する、
/// もしくはStationPathの自動検出の判定に利用する端点種別のみに派生型を限定している。
/// 自動検出については<see cref="Algorithm.Stations.FloorUnitObjects.StationPathSuggester"/>を参照。
/// </remarks>
public abstract record StationPathWaypoint;

/// <summary>
/// レール端点の「閉塞境界点」を参照する型
/// </summary>
/// <param name="Id">閉塞境界点識別子</param>
public sealed record BoundaryPointWaypoint(BoundaryPointId Id) : StationPathWaypoint;
/// <summary>
/// レール端点の「駅境界点」を参照する型
/// </summary>
/// <param name="Id">駅境界点識別子</param>
public sealed record EntryPointWaypoint(EntryPointId Id) : StationPathWaypoint;
/// <summary>
/// レール端点の「分岐器」を参照する型
/// </summary>
/// <remarks>
/// PortIndexはRailのためのフィールドであるから持たない。
/// </remarks>
/// <param name="Id">分岐器識別子</param>
public sealed record SwitcherWaypoint(SwitcherId Id) : StationPathWaypoint;
/// <summary>
/// レール端点の「車止め」を参照する型
/// </summary>
/// <param name="Id">車止め識別子</param>
public sealed record BufferStopWaypoint(BufferStopId Id) : StationPathWaypoint;

/// <summary>
/// <see cref="Algorithm.TimeTable.Trains.Conflicts.ConflictChecker"/>用の仮想グループオブジェクト 
/// </summary>
/// <remarks>
/// 信号システム、建築限界など、グラフ構造から導出できない支障をグループ化する。
/// </remarks>
public sealed class VirtualConflictObject
{
    /// <summary>
    /// 仮想支障グループ識別子
    /// </summary>
    public required VirtualConflictObjectId Id { get; set; }
    /// <summary>
    /// 所属する駅階層の識別子
    /// </summary>
    public required FloorUnitId FloorUnitId { get; set; }
    /// <summary>
    /// 仮想支障グループ名
    /// </summary>
    public string Name { get; set; } = "";
}

/// <summary>
/// 構内進路を表す
/// </summary>
public sealed class StationPath
{
    /// <summary>
    /// 構内進路識別子
    /// </summary>
    public required StationPathId Id { get; set; }
    /// <summary>
    /// 所属する駅階層の識別子
    /// </summary>
    public required FloorUnitId FloorUnitId { get; set; }
    /// <summary>
    /// 構内進路の名称
    /// </summary>
    /// <remarks>
    /// 同一FloorUnitId内で一意
    /// </remarks>
    public required string Name { get; set; }
    /// <summary>
    /// 構内進路の方向種別
    /// </summary>
    public required StationPathDirection Direction { get; set; }
    /// <summary>
    /// 構内進路を構成するRail端点配列
    /// </summary>
    /// <remarks>
    /// 制約：
    /// <list type="bullet">
    /// <item>WayPointsは最低1件（Halt駅単一EPパターンのみ1件、他は通常2件以上）</item>
    /// <item>Waypoints[0]はEntryPoint/BoundaryPointのいずれか</item>
    /// <item>Waypoints[last]も同様</item>
    /// <item>中間要素はSwitcher/BoundaryPointのいずれか</item>
    /// <item>隣接Waypoint間を直接結ぶRailが存在すること</item>
    /// <item>同一参照先が2回以上出現してはならない（ループ排除）</item>
    /// <item>Track各端部（BoundaryPoint以外）は、到達可能なArrivalEP/DepartureEPが1つ以上StationPathとして存在すること</item>
    /// <item>Waypoints各要素の参照先オブジェクトのFloorUnitIdが、StationPath.FloorUnitIdと一致する</item>
    /// </list>
    /// </remarks>
    public required List<StationPathWaypoint> Waypoints { get; set; }
    /// <summary>
    /// 停車時の構内進路の通過に必要な時間。通過時には用いない。
    /// </summary>
    public int AdjustmentSec { get; set; } = 0;
    /// <summary>
    /// 所属する仮想支障グループ識別子
    /// </summary>
    public List<VirtualConflictObjectId> ManualConflictObjectIds { get; set; } = new();
}

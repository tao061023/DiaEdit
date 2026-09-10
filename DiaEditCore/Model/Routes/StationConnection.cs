namespace DiaEditCore.Model.Routes;

/// <summary>
/// 走行経路の向き
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description><c>Up</c>上り方向</description></item>
/// <item><description><c>Down</c>下り方向</description></item>
/// </list>
/// </remarks>
public enum StationConnectionDirection { Up, Down }

/// <summary>
/// 走行経路を表す
/// </summary>
public sealed class StationConnection
{
    /// <summary>
    /// 走行経路識別子
    /// </summary>
    public required StationConnectionId Id { get; set; }
    /// <summary>
    /// 走行経路名称
    /// </summary>
    /// <remarks>
    /// 同一MainRouteで一意
    /// </remarks>
    public required string Name { get; set; } = "";
    /// <summary>
    /// 所属する路線の識別子
    /// </summary>
    public required MainRouteId MainRouteId { get; set; }
    /// <summary>
    /// 走行経路の向き
    /// </summary>
    public required StationConnectionDirection Direction { get; set; }
    /// <summary>
    /// 経路を構成する駅間物理区間のリスト
    /// </summary>
    /// <remarks>
    /// 実体参照。同一SCSIdを複数SCが共有しうる
    /// </remarks>
    public required List<StationConnectionSegmentId> Segments { get; set; }
}
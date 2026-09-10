namespace DiaEditCore.Model.Routes;

/// <summary>
/// 駅と駅を接続する最小単位
/// </summary>
/// <remarks>
/// 線路単位で管理する
/// </remarks>
public sealed class StationConnectionSegment
{
    /// <summary>
    /// 駅間物理区間識別子
    /// </summary>
    public required StationConnectionSegmentId Id { get; set; }
    /// <summary>
    /// 区間を形成する駅A
    /// </summary>
    public required StationId StationIdA { get; set; }
    /// <summary>
    /// 区間を形成する駅B
    /// </summary>
    public required StationId StationIdB { get; set; }
    /// <summary>
    /// 駅Aの駅境界点識別子
    /// </summary>
    public required EntryPointId EntryPointIdA { get; set; }
    /// <summary>
    /// 駅Bの駅境界点識別子
    /// </summary>
    public required EntryPointId EntryPointIdB { get; set; }
    /// <summary>
    /// 所属する路線の識別子
    /// </summary>
    public required MainRouteId MainRouteId { get; set; }
    /// <summary>
    /// 区間長
    /// </summary>
    /// <remarks>
    /// 単位は [ km ]
    /// </remarks>
    public double LengthM { get; set; }
    /// <summary>
    /// 区間の営業最高速度
    /// </summary>
    public double SpeedLimitKph { get; set; }
}
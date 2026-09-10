namespace DiaEditCore.Model.Cars;

/// <summary>
/// 車両形式を表現する
/// </summary>
public sealed class VehicleType
{
    /// <summary>
    /// 車両形式識別子
    /// </summary>
    public required VehicleTypeId Id { get; set; }
    /// <summary>
    /// 車両形式名
    /// </summary>
    public required string Name { get; set; }
    /// <summary>
    /// 設計最高速度
    /// </summary>
    public double MaxSpeedKph { get; set; }
}
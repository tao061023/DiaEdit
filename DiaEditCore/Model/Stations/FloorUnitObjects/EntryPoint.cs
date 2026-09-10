namespace DiaEditCore.Model.Stations.FloorUnitObjects;

/// <summary>
/// 駅境界点種別
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description><c>Arrival</c>：進入専用</description></item>
/// <item><description><c>Departure</c>：進出専用</description></item>
/// <item><description><c>Both</c>：双方向対応。単線や双単線、三複線などに用いる。</description></item>
/// </list>
/// </remarks>
public enum EntryPointType { Arrival, Departure, Both }

/// <summary>
/// 駅境界点を表す
/// </summary>
public sealed class EntryPoint
{
    /// <summary>
    /// 駅境界点識別子
    /// </summary>
    public required EntryPointId Id { get; set; }
    /// <summary>
    /// 駅階層識別子と座標情報を保持する複合フィールド
    /// </summary>
    public required FloorUnitObjectBase Base { get; set; }
    /// <summary>
    /// 駅境界点の名称
    /// </summary>
    public string Name { get; set; } = "";
    /// <summary>
    /// 駅境界点種別
    /// </summary>
    public required EntryPointType Type { get; set; }
}
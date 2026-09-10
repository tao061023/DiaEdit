namespace DiaEditCore.Model.Stations.FloorUnitObjects;

/// <summary>
/// 閉塞境界点を表す
/// </summary>
public sealed class BoundaryPoint
{
    /// <summary>
    /// 閉塞境界点識別子
    /// </summary>
    public required BoundaryPointId Id { get; set; }
    /// <summary>
    /// 駅階層識別子と座標情報を保持する複合フィールド
    /// </summary>
    public required FloorUnitObjectBase Base { get; set; }
    /// <summary>
    /// 閉塞境界点の名称
    /// </summary>
    public string Name { get; set; } = "";
}
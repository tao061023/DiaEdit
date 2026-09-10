namespace DiaEditCore.Model.Stations.FloorUnitObjects;

/// <summary>
/// 車止めを表す
/// </summary>
public sealed class BufferStop
{
    /// <summary>
    /// 車止め識別子
    /// </summary>
    public required BufferStopId Id { get; set; }
    /// <summary>
    /// 駅階層識別子と座標情報を保持する複合フィールド
    /// </summary>
    public required FloorUnitObjectBase Base { get; set; }
    /// <summary>
    /// 車止めの名称
    /// </summary>
    public string Name { get; set; } = "";
}
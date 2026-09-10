namespace DiaEditCore.Model.Stations.FloorUnitObjects;

/// <summary>
/// Rail作成時における端点オブジェクトの種別決定前の仮種別
/// </summary>
public sealed class NoneEndpoint
{
    /// <summary>
    /// 仮種別識別子
    /// </summary>
    public required NoneEndpointId Id { get; set; }
    /// <summary>
    /// 駅階層識別子と座標情報を保持する複合フィールド
    /// </summary>
    public required FloorUnitObjectBase Base { get; set; }
    /// <summary>
    /// 仮種別の名称
    /// </summary>
    public string Name { get; set; } = "";
}
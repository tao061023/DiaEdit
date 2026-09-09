namespace DiaEditCore.Model.Stations;

/// <summary>
/// 駅階層を表現する。
/// 
/// </summary>
public sealed class FloorUnit
{
    /// <summary>
    /// 駅階層識別子
    /// </summary>
    public required FloorUnitId Id { get; set; }
    /// <summary>
    /// 駅階層の所属する駅の識別子
    /// </summary>
    public required StationId StationId { get; set; }
    /// <summary>
    /// 駅階層名称
    /// </summary>
    /// <remarks>
    /// 空文字列許容。自動採番は行わない。
    /// </remarks>
    public string Name { get; set; } = "";
    /// <summary>
    /// 駅詳細画面における表示順
    /// </summary>
    /// <remarks>
    /// 同一StationId内で一意（保存時検証）
    /// </remarks>
    public required int DisplayOrder { get; set; }
}
namespace DiaEditCore.Model.TimeTable.Trains;

/// <summary>
/// 駅間ごとのStationConnection使用実績
/// </summary>
public sealed class TrainRunSegment
{
    public required StationId FromStationId { get; set; }
    public required StationId ToStationId { get; set; }
    public required StationConnectionId StationConnectionId { get; set; }
    /// <summary>
    /// 基準列車の値からの変更有無（UI表示用）
    /// </summary>
    public bool IsOverriddenFromTemplate { get; set; } = false;
}
namespace DiaEditCore.Model;

public sealed class TrainRunSegment
{
    public required StationId FromStationId { get; set; }
    public required StationId ToStationId { get; set; }
    public required StationConnectionId StationConnectionId { get; set; }
    public bool IsOverriddenFromTemplate { get; set; } = false; // 基準列車の値からの変更有無（UI表示用）
}
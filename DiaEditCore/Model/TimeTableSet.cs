namespace DiaEditCore.Model;

public sealed class TimeTableSet
{
    public required TimeTableSetId Id { get; set; }
    public required string Name { get; set; }
    public List<TrainId> TrainIds { get; set; } = new(); // 時刻表種別作成のみを許可。
}
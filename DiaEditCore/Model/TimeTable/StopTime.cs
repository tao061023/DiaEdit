namespace DiaEditCore.Model.TimeTable;

public sealed class StopTime
{
    public int ArrivalSeconds { get; set; } = -1;
    public int DepartureSeconds { get; set; } = -1;
    public bool IsStop { get; set; } = false;
    public RailId? TrackRailId { get; set; } // 客扱いする番線（Rail, roll=Track）。旧RailRef
    public List<StationWork> Works { get; set; } = new(); // このStopで発生する駅作業
}
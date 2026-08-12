namespace DiaEditCore.Model.Stations;

public enum StationType { Standard, Halt, SignalStation, Depot }

public sealed class Station
{
    public required StationId Id { get; set; }
    public required DisplayName DisplayName { get; set; }
    public required StationType Type { get; set; }
    public string OperatingCode { get; set; } = "";
    public string TelegraphCode { get; set; } = "";
    public bool? ShowsInStationTimetableOverride { get; set; }

    public bool ResolveShowsInStationTimetable()
    {
        if (ShowsInStationTimetableOverride.HasValue)
            return ShowsInStationTimetableOverride.Value;
        return Type != StationType.SignalStation && Type != StationType.Depot;
    }
}
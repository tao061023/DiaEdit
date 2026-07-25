namespace DiaEditCore.Model;

public record struct DateRange(DateTime Start, DateTime End);

public abstract record RestrictionTarget
{
    private RestrictionTarget() { }

    public record Segment(StationConnectionSegmentId StationConnectionSegmentId) : RestrictionTarget;

    public record Rail(RailId RailId) : RestrictionTarget;
}

public record TemporaryRestriction(
    TemporaryRestrictionId Id,
    RestrictionTarget Target,               
    int? ExtraRunTimeSec,
    int? SpeedLimitKph,
    DateRange DateRange,
    string Note
);
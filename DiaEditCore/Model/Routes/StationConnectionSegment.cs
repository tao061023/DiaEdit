namespace DiaEditCore.Model.Routes;

public sealed class StationConnectionSegment
{
    public required StationConnectionSegmentId Id { get; set; }
    public required StationId StationIdA { get; set; }
    public required StationId StationIdB { get; set; }
    public required EntryPointId EntryPointIdA { get; set; } // StationIdA側で使うEP（正データ）
    public required EntryPointId EntryPointIdB { get; set; }   // StationIdB側で使うEP（正データ）
    public required MainRouteId MainRouteId { get; set; }
    public double LengthM { get; set; }
    public double SpeedLimitKph { get; set; }
}
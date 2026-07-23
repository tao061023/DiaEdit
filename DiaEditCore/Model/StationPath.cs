namespace DiaEditCore.Model;

public enum StationPathDirection { Arrival, Departure, Shunting }

public sealed class StationPath
{
    public required StationPathId Id { get; set; }
    public required FloorUnitId FloorUnitId { get; set; }
    public required string Name { get; set; }
    public required StationPathDirection Direction { get; set; }
    public required List<StationPathWaypoint> Waypoints { get; set; }
    public int AdjustmentSec { get; set; } = 0;
    public List<VirtualConflictObjectId> ManualConflictObjectIds { get; set; } = new();
}

public sealed class VirtualConflictObject
{
    public required VirtualConflictObjectId Id { get; set; }
    public required StationId StationId { get; set; }
    public string Name { get; set; } = "";
}
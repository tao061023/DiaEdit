namespace DiaEditCore.Model.Stations;

public enum StationPathDirection { Arrival, Departure, Shunting }

public abstract record StationPathWaypoint;

public sealed record BoundaryPointWaypoint(BoundaryPointId Id) : StationPathWaypoint;
public sealed record EntryPointWaypoint(EntryPointId Id) : StationPathWaypoint;
public sealed record SwitcherWaypoint(SwitcherId Id) : StationPathWaypoint;
public sealed record BufferStopWaypoint(BufferStopId Id) : StationPathWaypoint;

public sealed class VirtualConflictObject
{
    public required VirtualConflictObjectId Id { get; set; }
    public required FloorUnitId FloorUnitId { get; set; }
    public string Name { get; set; } = "";
}

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

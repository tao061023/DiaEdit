namespace DiaEditCore.Model.Stations;

public abstract record StationPathWaypoint;

public sealed record BoundaryPointWaypoint(BoundaryPointId Id) : StationPathWaypoint;
public sealed record EntryPointWaypoint(EntryPointId Id) : StationPathWaypoint;
public sealed record SwitcherWaypoint(SwitcherId Id) : StationPathWaypoint;
public sealed record BufferStopWaypoint(BufferStopId Id) : StationPathWaypoint;
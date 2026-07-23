namespace DiaEditCore.Model;

public abstract record RailEndpointRef;

public sealed record NoneEndpointRef : RailEndpointRef;
public sealed record BoundaryPointEndpointRef(BoundaryPointId Id) : RailEndpointRef;
public sealed record EntryPointEndpointRef(EntryPointId Id) : RailEndpointRef;
public sealed record BufferStopEndpointRef(BufferStopId Id) : RailEndpointRef;
public sealed record SwitcherEndpointRef(SwitcherId Id, int PortIndex) : RailEndpointRef;
// PortIndexはSwitcher接続時のみ意味を持つため、Switcher用派生型にのみ持たせる（構造的防止）
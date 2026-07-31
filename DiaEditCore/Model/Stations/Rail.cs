namespace DiaEditCore.Model.Stations;

public enum RailRole { Normal, Track, Shunting };

public abstract record RailEndpointRef;

public sealed record NoneEndpointRef : RailEndpointRef;
public sealed record BoundaryPointEndpointRef(BoundaryPointId Id) : RailEndpointRef;
public sealed record EntryPointEndpointRef(EntryPointId Id) : RailEndpointRef;
public sealed record BufferStopEndpointRef(BufferStopId Id) : RailEndpointRef;
public sealed record SwitcherEndpointRef(SwitcherId Id, int PortIndex) : RailEndpointRef;
// PortIndexはSwitcher接続時のみ意味を持つため、Switcher用派生型にのみ持たせる（構造的防止）

public sealed class RailControlPoint
{
    public required Point Point { get; set; }
}

public sealed class Rail
{
    public required RailId Id { get; set; }
    public string Name { get; set; } = "";
    public required double LengthM { get; set; }
    public required double SpeedLimitKph { get; set; }
    public required RailRole Role { get; set; }

    public required RailEndpointRef EndpointA { get; set; }
    public required RailEndpointRef EndpointB { get; set; }

    public List<RailControlPoint> ControlPoints { get; set; } = new();
}
namespace DiaEditCore.Model.Stations;

public static class FloorObjectRefKeyExtensions
{
    public static (string Kind, int Id) Key(this RailEndpointRef r) => r switch
    {
        BoundaryPointEndpointRef b => ("BoundaryPoint", b.Id.Value),
        EntryPointEndpointRef e => ("EntryPoint", e.Id.Value),
        BufferStopEndpointRef bs => ("BufferStop", bs.Id.Value),
        SwitcherEndpointRef sw => ("Switcher", sw.Id.Value),
        NoneEndpointRef => ("None", -1),
        _ => throw new ArgumentOutOfRangeException(nameof(r)),
    };

    public static (string Kind, int Id) Key(this StationPathWaypoint w) => w switch
    {
        BoundaryPointWaypoint b => ("BoundaryPoint", b.Id.Value),
        EntryPointWaypoint e => ("EntryPoint", e.Id.Value),
        SwitcherWaypoint sw => ("Switcher", sw.Id.Value),
        BufferStopWaypoint bs => ("BufferStop", bs.Id.Value),
        _ => throw new ArgumentOutOfRangeException(nameof(w)),
    };
}
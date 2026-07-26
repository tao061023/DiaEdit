namespace DiaEditCore.Model.Stations;

public enum RailRoll { Normal, Track, Shunting }

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
    public required RailRoll Roll { get; set; }

    public required RailEndpointRef EndpointA { get; set; }
    public required RailEndpointRef EndpointB { get; set; }

    public List<RailControlPoint> ControlPoints { get; set; } = new();
}
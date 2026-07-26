namespace DiaEditCore.Model.Stations;
public enum EntryPointType { Arrival, Departure, Both }

public sealed class EntryPoint
{
    public required EntryPointId Id { get; set; }
    public required FloorUnitObjectBase Base { get; set; }
    public string Name { get; set; } = "";
    public required EntryPointType Type { get; set; }
}
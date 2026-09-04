namespace DiaEditCore.Model.Stations.FloorUnitObjects;

public sealed class NoneEndpoint
{
    public required NoneEndpointId Id { get; set; }
    public required FloorUnitObjectBase Base { get; set; }
    public string Name { get; set; } = "";
}
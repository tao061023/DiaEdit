namespace DiaEditCore.Model.Stations.FloorUnitObjects;

public sealed class BoundaryPoint
{
    public required BoundaryPointId Id { get; set; }
    public required FloorUnitObjectBase Base { get; set; }
    public string Name { get; set; } = "";
}
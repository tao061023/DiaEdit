namespace DiaEditCore.Model.Stations;
public sealed class BoundaryPoint
{
    public required BoundaryPointId Id { get; set; }
    public required FloorUnitObjectBase Base { get; set; }
    public string Name { get; set; } = "";
}
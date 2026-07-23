namespace DiaEditCore.Model;

public sealed class BufferStop
{
    public required BufferStopId Id { get; set; }
    public required FloorUnitObjectBase Base { get; set; }
    public string Name { get; set; } = "";
}
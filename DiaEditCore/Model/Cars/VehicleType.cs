namespace DiaEditCore.Model.Cars;

public sealed class VehicleType
{
    public required VehicleTypeId Id { get; set; }
    public required string Name { get; set; } // 例: "E235系"
    public double MaxSpeedKph { get; set; }
}
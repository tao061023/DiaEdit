namespace DiaEditCore.Model.Cars;

public sealed class Car
{
    public required CarId Id { get; set; }
    public required VehicleTypeId VehicleTypeId { get; set; }
    public required string Number { get; set; }
}
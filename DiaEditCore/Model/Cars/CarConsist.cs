namespace DiaEditCore.Model.Cars;

public enum CarConsistType { Basic, Attached }

public sealed class CarRef
{
    public required CarId CarId { get; set; }
    public required int Position { get; set; }
}

public sealed class CarConsist
{
    public required CarConsistId Id { get; set; }
    public required VehicleTypeId VehicleTypeId { get; set; }
    public required CarConsistType Type { get; set; }
    public required List<CarRef> Cars { get; set; }
}
namespace DiaEditCore.Model.Cars;

public abstract record CarConsistSourceTemplate;
public sealed record BaseTemplateSource : CarConsistSourceTemplate;
public sealed record AttachedTemplateSource(AttachedCarTemplateId TemplateId) : CarConsistSourceTemplate;

public sealed class CarRef
{
    public required CarId CarId { get; set; }
    public required int Position { get; set; }
}

public sealed class CarConsist
{
    public required CarConsistId Id { get; set; }
    public required string Name { get; set; } // 例: "トウ01"
    public required VehicleTypeId VehicleTypeId { get; set; }
    public required CarConsistSourceTemplate SourceTemplate { get; set; }
    public required string Identifier { get; set; }
    public required List<CarRef> Cars { get; set; }
}
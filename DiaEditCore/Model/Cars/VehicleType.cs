namespace DiaEditCore.Model.Cars;

public sealed class CarRoleSlot
{
    public required string CarTypeCode { get; set; } // "クハE234" など
    public string Placeholder { get; set; } = "0";
}

public sealed class AttachedCarTemplate
{
    public required AttachedCarTemplateId Id { get; set; }
    public required string Name { get; set; } // 例: "5両付属編成"
    public required List<CarRoleSlot> Slots { get; set; }
}

public sealed class VehicleType
{
    public required VehicleTypeId Id { get; set; }
    public required string Name { get; set; } // 例: "E235系"
    public double MaxSpeedKph { get; set; }
    public required double LengthM { get; set; }
    public required List<CarRoleSlot> BaseCarTemplate { get; set; }             // 基本編成のひな型
    public List<AttachedCarTemplate> AttachedCarTemplates { get; set; } = new(); // 付属編成のひな型群
}
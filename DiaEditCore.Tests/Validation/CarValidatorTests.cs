using DiaEditCore.Model;
using DiaEditCore.Serialization.Validation;
using Xunit;

namespace DiaEditCore.Tests.Validation;

public class CarValidatorTests
{
    private readonly CarValidator _validator = new();

    private static VehicleType MakeVehicleType(int id) => new()
    {
        Id = new VehicleTypeId(id),
        Name = "E235系",
        LengthM = 20.0,
        BaseCarTemplate = new List<CarRoleSlot> { new() { CarTypeCode = "クハE234" } },
    };

    [Fact]
    public void Validate_正常なCarはissueなし()
    {
        var vehicleType = MakeVehicleType(1);
        var context = new ValidationContext { VehicleTypes = new[] { vehicleType } };
        var car = new Car { Id = new CarId(1), VehicleTypeId = vehicleType.Id, Number = "1001" };

        var issues = _validator.Validate(car, context);
        Assert.Empty(issues);
    }

    [Fact]
    public void Validate_Numberが空ならissue()
    {
        var vehicleType = MakeVehicleType(1);
        var context = new ValidationContext { VehicleTypes = new[] { vehicleType } };
        var car = new Car { Id = new CarId(1), VehicleTypeId = vehicleType.Id, Number = "" };

        var issues = _validator.Validate(car, context);
        Assert.Contains(issues, i => i.Message.Contains("Number"));
    }

    [Fact]
    public void Validate_VehicleTypeIdが存在しなければissue()
    {
        var context = new ValidationContext { VehicleTypes = Array.Empty<VehicleType>() };
        var car = new Car { Id = new CarId(1), VehicleTypeId = new VehicleTypeId(999), Number = "1001" };

        var issues = _validator.Validate(car, context);
        Assert.Contains(issues, i => i.Message.Contains("VehicleTypeId"));
    }
}

namespace DiaEditCore.Tests.Serialization.Validation.Cars;

using DiaEditCore.Model;
using DiaEditCore.Model.Cars;
using DiaEditCore.Serialization.Validation;
using DiaEditCore.Serialization.Validation.Cars;

using Xunit;

public class VehicleTypeValidatorTests
{
    private readonly VehicleTypeValidator _validator = new();
    private static readonly ValidationContext EmptyContext = new();

    private static VehicleType ValidVehicleType() => new()
    {
        Id = new VehicleTypeId(1),
        Name = "E235系",
    };

    [Fact]
    public void Validate_正常なVehicleTypeはissueなし()
    {
        var issues = _validator.Validate(ValidVehicleType(), EmptyContext);
        Assert.Empty(issues);
    }

    [Fact]
    public void Validate_Nameが空ならissue()
    {
        var target = ValidVehicleType();
        target.Name = " ";
        var issues = _validator.Validate(target, EmptyContext);
        Assert.Contains(issues, i => i.Message.Contains("Name"));
    }
}
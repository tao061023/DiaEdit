using DiaEditCore.Model;
using DiaEditCore.Model.Cars;

using DiaEditCore.Serialization.Validation;
using DiaEditCore.Serialization.Validation.Cars;
using Xunit;

namespace DiaEditCore.Tests.Serialization.Validation.Cars;

public class CarValidatorTests
{
    private readonly CarValidator _validator = new();
    private static readonly ValidationContext EmptyContext = new();

    private static Car ValidCar() => new()
    {
        Id = new CarId(1),
        CarType = "クハE234",
        IsPower = false,
        LengthM = 20.0,
    };

    [Fact]
    public void Validate_正常なCarはissueなし()
    {
        var issues = _validator.Validate(ValidCar(), EmptyContext);
        Assert.Empty(issues);
    }

    [Fact]
    public void Validate_CarTypeが空ならissue()
    {
        var car = ValidCar();
        car.CarType = "";
        var issues = _validator.Validate(car, EmptyContext);
        Assert.Contains(issues, i => i.Message.Contains("CarType"));
    }

    [Fact]
    public void Validate_LengthMが0以下ならissue()
    {
        var car = ValidCar();
        car.LengthM = 0;
        var issues = _validator.Validate(car, EmptyContext);
        Assert.Contains(issues, i => i.Message.Contains("LengthM"));
    }
}
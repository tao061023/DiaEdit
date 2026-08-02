using DiaEditCore.Model;
using DiaEditCore.Model.Cars;

using DiaEditCore.Serialization.Validation;
using DiaEditCore.Serialization.Validation.Cars;

using Xunit;

namespace DiaEditCore.Tests.Validation.Cars;

public class CarConsistValidatorTests
{
    private readonly CarConsistValidator _validator = new();

    private static readonly VehicleTypeId VehicleTypeId1 = new(1);

    // firstIsPower: 先頭車両（Position 0相当）だけをIsPower=trueにする。動力車ゼロ両編成を作りたい場合はfalseを指定。
    private static List<Car> MakeCars(int count, bool firstIsPower = true, int startId = 1)
    {
        var cars = new List<Car>();
        for (var i = 0; i < count; i++)
        {
            cars.Add(new Car
            {
                Id = new CarId(startId + i),
                CarType = "テスト車両",
                IsPower = firstIsPower && i == 0,
                LengthM = 20.0,
            });
        }
        return cars;
    }

    private static ValidationContext MakeContext(IReadOnlyList<Car> cars) => new()
    {
        Cars = cars,
    };

    [Fact]
    public void Validate_基本編成で正常なCarConsistはissueなし()
    {
        var cars = MakeCars(2);
        var context = MakeContext(cars);

        var consist = new CarConsist
        {
            Id = new CarConsistId(1),
            VehicleTypeId = VehicleTypeId1,
            Type = CarConsistType.Basic,
            Cars = new List<CarRef>
            {
                new() { CarId = cars[0].Id, Position = 0 },
                new() { CarId = cars[1].Id, Position = 1 },
            },
        };

        var issues = _validator.Validate(consist, context);
        Assert.Empty(issues);
    }

    [Fact]
    public void Validate_付属編成で正常なCarConsistはissueなし()
    {
        var cars = MakeCars(3);
        var context = MakeContext(cars);

        var consist = new CarConsist
        {
            Id = new CarConsistId(2),
            VehicleTypeId = VehicleTypeId1,
            Type = CarConsistType.Attached,
            Cars = cars.Select((c, i) => new CarRef { CarId = c.Id, Position = i }).ToList(),
        };

        var issues = _validator.Validate(consist, context);
        Assert.Empty(issues);
    }

    [Fact]
    public void Validate_Positionが0始まりの連番でなければissue()
    {
        var cars = MakeCars(2);
        var context = MakeContext(cars);

        var consist = new CarConsist
        {
            Id = new CarConsistId(1),
            VehicleTypeId = VehicleTypeId1,
            Type = CarConsistType.Basic,
            Cars = new List<CarRef>
            {
                new() { CarId = cars[0].Id, Position = 0 },
                new() { CarId = cars[1].Id, Position = 2 }, // 1が抜けている
            },
        };

        var issues = _validator.Validate(consist, context);
        Assert.Contains(issues, i => i.Message.Contains("連番"));
    }

    [Fact]
    public void Validate_参照Carが存在しなければissue()
    {
        var context = MakeContext(Array.Empty<Car>());

        var consist = new CarConsist
        {
            Id = new CarConsistId(1),
            VehicleTypeId = VehicleTypeId1,
            Type = CarConsistType.Basic,
            Cars = new List<CarRef> { new() { CarId = new CarId(999), Position = 0 } },
        };

        var issues = _validator.Validate(consist, context);
        Assert.Contains(issues, i => i.Message.Contains("存在しない"));
    }

    [Fact]
    public void Validate_動力車が1両も含まれなければissue()
    {
        var cars = MakeCars(2, firstIsPower: false);
        var context = MakeContext(cars);

        var consist = new CarConsist
        {
            Id = new CarConsistId(1),
            VehicleTypeId = VehicleTypeId1,
            Type = CarConsistType.Basic,
            Cars = cars.Select((c, i) => new CarRef { CarId = c.Id, Position = i }).ToList(),
        };

        var issues = _validator.Validate(consist, context);
        Assert.Contains(issues, i => i.Message.Contains("動力車"));
    }

    [Fact]
    public void Validate_動力車が含まれていればType種別を問わずissueなし()
    {
        var cars = MakeCars(2, firstIsPower: true);
        var context = MakeContext(cars);

        var consist = new CarConsist
        {
            Id = new CarConsistId(2),
            VehicleTypeId = VehicleTypeId1,
            Type = CarConsistType.Attached,
            Cars = cars.Select((c, i) => new CarRef { CarId = c.Id, Position = i }).ToList(),
        };

        var issues = _validator.Validate(consist, context);
        Assert.DoesNotContain(issues, i => i.Message.Contains("動力車"));
    }
}
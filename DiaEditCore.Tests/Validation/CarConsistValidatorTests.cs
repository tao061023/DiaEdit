using DiaEditCore.Model;
using DiaEditCore.Serialization.Validation;
using Xunit;

namespace DiaEditCore.Tests.Validation;

public class CarConsistValidatorTests
{
    private readonly CarConsistValidator _validator = new();

    private static readonly VehicleTypeId VehicleTypeId1 = new(1);
    private static readonly AttachedCarTemplateId AttachedTemplateId1 = new(1);

    private static VehicleType MakeVehicleType() => new()
    {
        Id = VehicleTypeId1,
        Name = "E235系",
        LengthM = 20.0,
        BaseCarTemplate = new List<CarRoleSlot>
        {
            new() { CarTypeCode = "クハE234" },
            new() { CarTypeCode = "モハE235" },
        },
        AttachedCarTemplates = new List<AttachedCarTemplate>
        {
            new()
            {
                Id = AttachedTemplateId1,
                Name = "5両付属編成",
                Slots = new List<CarRoleSlot>
                {
                    new() { CarTypeCode = "クモハE234" },
                    new() { CarTypeCode = "モハE235" },
                    new() { CarTypeCode = "クハE235" },
                },
            },
        },
    };

    private static List<Car> MakeCars(int count, VehicleTypeId vehicleTypeId, int startId = 1) =>
        Enumerable.Range(startId, count)
            .Select(i => new Car { Id = new CarId(i), VehicleTypeId = vehicleTypeId, Number = $"10{i:00}" })
            .ToList();

    private static ValidationContext MakeContext(VehicleType vehicleType, IReadOnlyList<Car> cars) => new()
    {
        VehicleTypes = new[] { vehicleType },
        Cars = cars,
    };

    [Fact]
    public void Validate_基本編成で正常なCarConsistはissueなし()
    {
        var vehicleType = MakeVehicleType();
        var cars = MakeCars(2, VehicleTypeId1);
        var context = MakeContext(vehicleType, cars);

        var consist = new CarConsist
        {
            Id = new CarConsistId(1),
            Name = "トウ01",
            VehicleTypeId = VehicleTypeId1,
            SourceTemplate = new BaseTemplateSource(),
            Identifier = "1",
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
        var vehicleType = MakeVehicleType();
        var cars = MakeCars(3, VehicleTypeId1);
        var context = MakeContext(vehicleType, cars);

        var consist = new CarConsist
        {
            Id = new CarConsistId(2),
            Name = "トウ81",
            VehicleTypeId = VehicleTypeId1,
            SourceTemplate = new AttachedTemplateSource(AttachedTemplateId1),
            Identifier = "81",
            Cars = new List<CarRef>
            {
                new() { CarId = cars[0].Id, Position = 0 },
                new() { CarId = cars[1].Id, Position = 1 },
                new() { CarId = cars[2].Id, Position = 2 },
            },
        };

        var issues = _validator.Validate(consist, context);
        Assert.Empty(issues);
    }

    [Fact]
    public void Validate_Positionが0始まりの連番でなければissue()
    {
        var vehicleType = MakeVehicleType();
        var cars = MakeCars(2, VehicleTypeId1);
        var context = MakeContext(vehicleType, cars);

        var consist = new CarConsist
        {
            Id = new CarConsistId(1),
            Name = "トウ01",
            VehicleTypeId = VehicleTypeId1,
            SourceTemplate = new BaseTemplateSource(),
            Identifier = "1",
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
        var vehicleType = MakeVehicleType();
        var context = MakeContext(vehicleType, Array.Empty<Car>());

        var consist = new CarConsist
        {
            Id = new CarConsistId(1),
            Name = "トウ01",
            VehicleTypeId = VehicleTypeId1,
            SourceTemplate = new BaseTemplateSource(),
            Identifier = "1",
            Cars = new List<CarRef> { new() { CarId = new CarId(999), Position = 0 } },
        };

        var issues = _validator.Validate(consist, context);
        Assert.Contains(issues, i => i.Message.Contains("存在しない"));
    }

    [Fact]
    public void Validate_CarのVehicleTypeIdが不一致ならissue()
    {
        var vehicleType = MakeVehicleType();
        var mismatchedCar = new Car { Id = new CarId(1), VehicleTypeId = new VehicleTypeId(999), Number = "1001" };
        var context = MakeContext(vehicleType, new[] { mismatchedCar });

        var consist = new CarConsist
        {
            Id = new CarConsistId(1),
            Name = "トウ01",
            VehicleTypeId = VehicleTypeId1,
            SourceTemplate = new BaseTemplateSource(),
            Identifier = "1",
            Cars = new List<CarRef> { new() { CarId = mismatchedCar.Id, Position = 0 } },
        };

        var issues = _validator.Validate(consist, context);
        Assert.Contains(issues, i => i.Message.Contains("VehicleTypeId"));
    }

    [Fact]
    public void Validate_AttachedTemplateSourceの参照先が存在しなければissue()
    {
        var vehicleType = MakeVehicleType();
        var cars = MakeCars(1, VehicleTypeId1);
        var context = MakeContext(vehicleType, cars);

        var consist = new CarConsist
        {
            Id = new CarConsistId(2),
            Name = "トウ81",
            VehicleTypeId = VehicleTypeId1,
            SourceTemplate = new AttachedTemplateSource(new AttachedCarTemplateId(999)),
            Identifier = "81",
            Cars = new List<CarRef> { new() { CarId = cars[0].Id, Position = 0 } },
        };

        var issues = _validator.Validate(consist, context);
        Assert.Contains(issues, i => i.Message.Contains("AttachedCarTemplateId"));
    }

    [Fact]
    public void Validate_Cars数がひな型スロット数と不一致ならissue()
    {
        var vehicleType = MakeVehicleType(); // BaseCarTemplateは2両編成
        var cars = MakeCars(3, VehicleTypeId1); // 3両分用意
        var context = MakeContext(vehicleType, cars);

        var consist = new CarConsist
        {
            Id = new CarConsistId(1),
            Name = "トウ01",
            VehicleTypeId = VehicleTypeId1,
            SourceTemplate = new BaseTemplateSource(),
            Identifier = "1",
            Cars = new List<CarRef>
            {
                new() { CarId = cars[0].Id, Position = 0 },
                new() { CarId = cars[1].Id, Position = 1 },
                new() { CarId = cars[2].Id, Position = 2 }, // ひな型は2両分しかない
            },
        };

        var issues = _validator.Validate(consist, context);
        Assert.Contains(issues, i => i.Message.Contains("スロット数"));
    }
}

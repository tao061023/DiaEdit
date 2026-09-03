namespace DiaEditCore.Tests.Serialization.Validation.Cars;

using DiaEditCore.Model;
using DiaEditCore.Model.Cars;
using DiaEditCore.Serialization.Validation;
using DiaEditCore.Serialization.Validation.Cars;

using Xunit;

public class CarCompositionValidatorTests
{
    private readonly CarCompositionValidator _validator = new();

    private static readonly CarConsistId ConsistId1 = new(1);

    private static CarConsist MakeConsist(CarConsistId id) => new()
    {
        Id = id,
        VehicleTypeId = new VehicleTypeId(1),
        Type = CarConsistType.Basic,
        Cars = new List<CarRef>(),
    };

    private static ValidationContext MakeContext(
        IReadOnlyList<CarConsist>? consists = null,
        IReadOnlyList<CarComposition>? compositions = null) => new()
    {
        CarConsists = consists ?? Array.Empty<CarConsist>(),
        CarCompositions = compositions ?? Array.Empty<CarComposition>(),
    };

    [Fact]
    public void Validate_正常なCarCompositionはissueなし()
    {
        var consist = MakeConsist(ConsistId1);
        var composition = new CarComposition
        {
            Id = new CarCompositionId(1),
            Name = "トウ01",
            Identifier = 1,
            CarConsistId = ConsistId1,
        };
        var context = MakeContext(
            consists: new[] { consist },
            compositions: new[] { composition });

        var issues = _validator.Validate(composition, context);
        Assert.Empty(issues);
    }

    [Fact]
    public void Validate_Nameが空ならissue()
    {
        var consist = MakeConsist(ConsistId1);
        var composition = new CarComposition
        {
            Id = new CarCompositionId(1),
            Name = "",
            Identifier = 1,
            CarConsistId = ConsistId1,
        };
        var context = MakeContext(
            consists: new[] { consist },
            compositions: new[] { composition });

        var issues = _validator.Validate(composition, context);
        Assert.Contains(issues, i => i.Message.Contains("Nameが空"));
    }

    [Fact]
    public void Validate_参照CarConsistが存在しなければissue()
    {
        var composition = new CarComposition
        {
            Id = new CarCompositionId(1),
            Name = "トウ01",
            Identifier = 1,
            CarConsistId = ConsistId1,
        };
        var context = MakeContext(
            consists: Array.Empty<CarConsist>(),
            compositions: new[] { composition });

        var issues = _validator.Validate(composition, context);
        Assert.Contains(issues, i => i.Message.Contains("存在しない"));
    }

    [Fact]
    public void Validate_Nameが他のCarCompositionと重複していればissue()
    {
        var consist = MakeConsist(ConsistId1);
        var target = new CarComposition
        {
            Id = new CarCompositionId(1),
            Name = "トウ01",
            Identifier = 1,
            CarConsistId = ConsistId1,
        };
        var other = new CarComposition
        {
            Id = new CarCompositionId(2),
            Name = "トウ01", // 重複
            Identifier = 2,
            CarConsistId = ConsistId1,
        };
        var context = MakeContext(
            consists: new[] { consist },
            compositions: new[] { target, other });

        var issues = _validator.Validate(target, context);
        Assert.Contains(issues, i => i.Message.Contains("Name") && i.Message.Contains("重複"));
    }

    [Fact]
    public void Validate_Identifierが他のCarCompositionと重複していればissue_CarConsistIdが異なっていても検知する()
    {
        var consist1 = MakeConsist(ConsistId1);
        var consist2 = MakeConsist(new CarConsistId(2));
        var target = new CarComposition
        {
            Id = new CarCompositionId(1),
            Name = "トウ01",
            Identifier = 1,
            CarConsistId = ConsistId1,
        };
        var other = new CarComposition
        {
            Id = new CarCompositionId(2),
            Name = "トウ02",
            Identifier = 1, // 重複（CarConsistIdは異なる）
            CarConsistId = consist2.Id,
        };
        var context = MakeContext(
            consists: new[] { consist1, consist2 },
            compositions: new[] { target, other });

        var issues = _validator.Validate(target, context);
        Assert.Contains(issues, i => i.Message.Contains("Identifier") && i.Message.Contains("重複"));
    }

    [Fact]
    public void Validate_自分自身とのIdentifier比較では重複と判定しない()
    {
        var consist = MakeConsist(ConsistId1);
        var target = new CarComposition
        {
            Id = new CarCompositionId(1),
            Name = "トウ01",
            Identifier = 1,
            CarConsistId = ConsistId1,
        };
        var context = MakeContext(
            consists: new[] { consist },
            compositions: new[] { target });

        var issues = _validator.Validate(target, context);
        Assert.DoesNotContain(issues, i => i.Message.Contains("重複"));
    }
}

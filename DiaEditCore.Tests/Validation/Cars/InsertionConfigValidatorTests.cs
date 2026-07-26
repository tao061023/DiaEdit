using DiaEditCore.Model;
using DiaEditCore.Model.Cars;

using DiaEditCore.Serialization.Validation;
using DiaEditCore.Serialization.Validation.Cars;

using Xunit;

namespace DiaEditCore.Tests.Validation.Cars;

public class InsertionConfigValidatorTests
{
    private readonly InsertionConfigValidator _validator = new();
    private static readonly VehicleTypeId VehicleTypeId1 = new(1);

    private static CarConsist MakeConsist(int id, CarConsistSourceTemplate sourceTemplate, int carCount = 2) => new()
    {
        Id = new CarConsistId(id),
        Name = $"編成{id}",
        VehicleTypeId = VehicleTypeId1,
        SourceTemplate = sourceTemplate,
        Identifier = id.ToString(),
        Cars = Enumerable.Range(0, carCount)
            .Select(p => new CarRef { CarId = new CarId(id * 100 + p), Position = p })
            .ToList(),
    };

    [Fact]
    public void Validate_正常なInsertionConfigはissueなし()
    {
        var baseConsist = MakeConsist(1, new BaseTemplateSource());
        var insertedConsist = MakeConsist(2, new AttachedTemplateSource(new AttachedCarTemplateId(1)));
        var context = new ValidationContext { CarConsists = new[] { baseConsist, insertedConsist } };

        var config = new InsertionConfig
        {
            Id = new InsertionConfigId(1),
            BaseCarConsistId = baseConsist.Id,
            AfterPosition = 1,
            InsertedCarConsistId = insertedConsist.Id,
        };

        var issues = _validator.Validate(config, context);
        Assert.Empty(issues);
    }

    [Fact]
    public void Validate_BaseとInsertedが同一ならissue()
    {
        var consist = MakeConsist(1, new BaseTemplateSource());
        var context = new ValidationContext { CarConsists = new[] { consist } };

        var config = new InsertionConfig
        {
            Id = new InsertionConfigId(1),
            BaseCarConsistId = consist.Id,
            AfterPosition = 0,
            InsertedCarConsistId = consist.Id,
        };

        var issues = _validator.Validate(config, context);
        Assert.Contains(issues, i => i.Message.Contains("同一"));
    }

    [Fact]
    public void Validate_BaseCarConsistIdが存在しなければissue()
    {
        var insertedConsist = MakeConsist(2, new AttachedTemplateSource(new AttachedCarTemplateId(1)));
        var context = new ValidationContext { CarConsists = new[] { insertedConsist } };

        var config = new InsertionConfig
        {
            Id = new InsertionConfigId(1),
            BaseCarConsistId = new CarConsistId(999),
            AfterPosition = 0,
            InsertedCarConsistId = insertedConsist.Id,
        };

        var issues = _validator.Validate(config, context);
        Assert.Contains(issues, i => i.Message.Contains("BaseCarConsistId"));
    }

    [Fact]
    public void Validate_BaseCarConsistが基本編成でなければissue()
    {
        var notBase = MakeConsist(1, new AttachedTemplateSource(new AttachedCarTemplateId(1)));
        var inserted = MakeConsist(2, new AttachedTemplateSource(new AttachedCarTemplateId(2)));
        var context = new ValidationContext { CarConsists = new[] { notBase, inserted } };

        var config = new InsertionConfig
        {
            Id = new InsertionConfigId(1),
            BaseCarConsistId = notBase.Id,
            AfterPosition = 0,
            InsertedCarConsistId = inserted.Id,
        };

        var issues = _validator.Validate(config, context);
        Assert.Contains(issues, i => i.Message.Contains("基本編成"));
    }

    [Fact]
    public void Validate_AfterPositionが範囲外ならissue()
    {
        var baseConsist = MakeConsist(1, new BaseTemplateSource(), carCount: 2); // Cars.Count = 2
        var inserted = MakeConsist(2, new AttachedTemplateSource(new AttachedCarTemplateId(1)));
        var context = new ValidationContext { CarConsists = new[] { baseConsist, inserted } };

        var config = new InsertionConfig
        {
            Id = new InsertionConfigId(1),
            BaseCarConsistId = baseConsist.Id,
            AfterPosition = 5, // Cars.Countを超えている
            InsertedCarConsistId = inserted.Id,
        };

        var issues = _validator.Validate(config, context);
        Assert.Contains(issues, i => i.Message.Contains("範囲外"));
    }

    [Fact]
    public void Validate_InsertedCarConsistIdが存在しなければissue()
    {
        var baseConsist = MakeConsist(1, new BaseTemplateSource());
        var context = new ValidationContext { CarConsists = new[] { baseConsist } };

        var config = new InsertionConfig
        {
            Id = new InsertionConfigId(1),
            BaseCarConsistId = baseConsist.Id,
            AfterPosition = 0,
            InsertedCarConsistId = new CarConsistId(999),
        };

        var issues = _validator.Validate(config, context);
        Assert.Contains(issues, i => i.Message.Contains("InsertedCarConsistId"));
    }

    [Fact]
    public void Validate_InsertedCarConsistが付属編成でなければissue()
    {
        var baseConsist = MakeConsist(1, new BaseTemplateSource());
        var notAttached = MakeConsist(2, new BaseTemplateSource());
        var context = new ValidationContext { CarConsists = new[] { baseConsist, notAttached } };

        var config = new InsertionConfig
        {
            Id = new InsertionConfigId(1),
            BaseCarConsistId = baseConsist.Id,
            AfterPosition = 0,
            InsertedCarConsistId = notAttached.Id,
        };

        var issues = _validator.Validate(config, context);
        Assert.Contains(issues, i => i.Message.Contains("付属編成"));
    }
}

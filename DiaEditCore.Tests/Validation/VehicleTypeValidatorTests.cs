using DiaEditCore.Model;
using DiaEditCore.Serialization.Validation;
using Xunit;

namespace DiaEditCore.Tests.Validation;

public class VehicleTypeValidatorTests
{
    private readonly VehicleTypeValidator _validator = new();
    private static readonly ValidationContext EmptyContext = new();

    private static VehicleType ValidVehicleType() => new()
    {
        Id = new VehicleTypeId(1),
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
                Id = new AttachedCarTemplateId(1),
                Name = "5両付属編成",
                Slots = new List<CarRoleSlot> { new() { CarTypeCode = "クハE234" } },
            },
        },
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

    [Fact]
    public void Validate_LengthMが0以下ならissue()
    {
        var target = ValidVehicleType();
        target.LengthM = 0;
        var issues = _validator.Validate(target, EmptyContext);
        Assert.Contains(issues, i => i.Message.Contains("LengthM"));
    }

    [Fact]
    public void Validate_BaseCarTemplateが空ならissue()
    {
        var target = ValidVehicleType();
        target.BaseCarTemplate = new List<CarRoleSlot>();
        var issues = _validator.Validate(target, EmptyContext);
        Assert.Contains(issues, i => i.Message.Contains("BaseCarTemplate"));
    }

    [Fact]
    public void Validate_BaseCarTemplate内のCarTypeCodeが空ならissue()
    {
        var target = ValidVehicleType();
        target.BaseCarTemplate[0].CarTypeCode = "";
        var issues = _validator.Validate(target, EmptyContext);
        Assert.Contains(issues, i => i.Message.Contains("CarTypeCode"));
    }

    [Fact]
    public void Validate_AttachedCarTemplateIdが重複していればissue()
    {
        var target = ValidVehicleType();
        target.AttachedCarTemplates.Add(new AttachedCarTemplate
        {
            Id = new AttachedCarTemplateId(1), // 既存と重複
            Name = "別の付属編成",
            Slots = new List<CarRoleSlot> { new() { CarTypeCode = "モハE235" } },
        });
        var issues = _validator.Validate(target, EmptyContext);
        Assert.Contains(issues, i => i.Message.Contains("重複"));
    }

    [Fact]
    public void Validate_AttachedCarTemplateのSlotsが空ならissue()
    {
        var target = ValidVehicleType();
        target.AttachedCarTemplates[0].Slots = new List<CarRoleSlot>();
        var issues = _validator.Validate(target, EmptyContext);
        Assert.Contains(issues, i => i.Message.Contains("Slots"));
    }
}

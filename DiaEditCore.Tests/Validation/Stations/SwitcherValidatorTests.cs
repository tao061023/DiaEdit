using DiaEditCore.Model;
using DiaEditCore.Model.Stations;

using DiaEditCore.Serialization.Validation;
using DiaEditCore.Serialization.Validation.Stations;

using Xunit;

namespace DiaEditCore.Tests.Validation.Stations;

public class SwitcherValidatorTests
{
    private static FloorUnitObjectBase MakeBase() => new()
    {
        FloorUnitId = new FloorUnitId(1),
        Position = new Point(0, 0),
    };

    [Fact]
    public void PortCountが5以上は不合格()
    {
        var s = new Switcher { Id = new SwitcherId(1), Base = MakeBase(), PortCount = 5 };
        var context = new ValidationContext();

        var issues = new SwitcherValidator().Validate(s, context);

        Assert.Contains(issues, i => i.Message.Contains("5以上"));
    }

    [Fact]
    public void PortCount3でMechanism未設定は不合格()
    {
        var s = new Switcher { Id = new SwitcherId(1), Base = MakeBase(), PortCount = 3, Mechanism = null };
        var context = new ValidationContext();

        var issues = new SwitcherValidator().Validate(s, context);

        Assert.Contains(issues, i => i.Message.Contains("Mechanism必須"));
    }

    [Fact]
    public void PortCount3でRootとNormalが同一だと不合格()
    {
        var s = new Switcher
        {
            Id = new SwitcherId(1),
            Base = MakeBase(),
            PortCount = 3,
            Mechanism = new SwitchMechanism { RootPortIndex = 0, NormalPortIndex = 0, ReversePortIndex = 1 },
        };
        var context = new ValidationContext();

        var issues = new SwitcherValidator().Validate(s, context);

        Assert.Contains(issues, i => i.Message.Contains("同一"));
    }

    [Fact]
    public void PortCount3で正しいMechanismなら合格()
    {
        var s = new Switcher
        {
            Id = new SwitcherId(1),
            Base = MakeBase(),
            PortCount = 3,
            Mechanism = new SwitchMechanism { RootPortIndex = 0, NormalPortIndex = 1, ReversePortIndex = 2 },
        };
        var context = new ValidationContext();

        var issues = new SwitcherValidator().Validate(s, context);

        Assert.Empty(issues);
    }

    [Fact]
    public void PortCount4でValidRoutesが0件は不合格()
    {
        var s = new Switcher { Id = new SwitcherId(1), Base = MakeBase(), PortCount = 4, ValidRoutes = [] };
        var context = new ValidationContext();

        var issues = new SwitcherValidator().Validate(s, context);

        Assert.Contains(issues, i => i.Message.Contains("1件以上必須"));
    }

    [Fact]
    public void PortCount4でPortAeqPortBは不合格()
    {
        var s = new Switcher { Id = new SwitcherId(1), Base = MakeBase(), PortCount = 4, ValidRoutes = [new PortPair(1, 1)] };
        var context = new ValidationContext();

        var issues = new SwitcherValidator().Validate(s, context);

        Assert.Contains(issues, i => i.Message.Contains("PortA≠PortB"));
    }

    [Fact]
    public void Halt駅上のSwitcherは不合格()
    {
        var station = new Station { Id = new StationId(1), DisplayName = new DisplayName { Name = "棒線駅" }, Type = StationType.Halt };
        var floorUnit = new FloorUnit { Id = new FloorUnitId(1), StationId = station.Id, DisplayOrder = 0 };
        var s = new Switcher
        {
            Id = new SwitcherId(1),
            Base = new FloorUnitObjectBase { FloorUnitId = floorUnit.Id, Position = new Point(0, 0) },
            PortCount = 3,
            Mechanism = new SwitchMechanism { RootPortIndex = 0, NormalPortIndex = 1, ReversePortIndex = 2 },
        };
        var context = new ValidationContext { Stations = [station], FloorUnits = [floorUnit] };

        var issues = new SwitcherValidator().Validate(s, context);

        Assert.Contains(issues, i => i.Message.Contains("Halt駅"));
    }
}
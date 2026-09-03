namespace DiaEditCore.Tests.Serialization.Validation.Stations.FloorUnitObjects;

using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.Stations.FloorUnitObjects;
using DiaEditCore.Serialization.Validation;
using DiaEditCore.Serialization.Validation.Stations.FloorUnitObjects;

using Xunit;

public class SwitcherValidatorTests
{
    private static FloorUnitObjectBase MakeBase() => new()
    {
        FloorUnitId = new FloorUnitId(1),
        Position = new Point(0, 0),
    };

    private static Rail MakeRailToSwitcher(int railId, SwitcherId switcherId, int portIndex) => new()
    {
        Id = new RailId(railId),
        LengthM = 10,
        SpeedLimitKph = 25,
        Role = RailRole.Normal,
        EndpointA = new SwitcherEndpointRef(switcherId, portIndex),
        EndpointB = new NoneEndpointRef(),
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

        Assert.Contains(issues, i => i.Message.Contains("重複している"));
    }

    [Fact]
    public void PortCount3でRootとReverseが同一だと不合格()
    {
        var s = new Switcher
        {
            Id = new SwitcherId(1),
            Base = MakeBase(),
            PortCount = 3,
            Mechanism = new SwitchMechanism { RootPortIndex = 0, NormalPortIndex = 1, ReversePortIndex = 0 },
        };
        var context = new ValidationContext();

        var issues = new SwitcherValidator().Validate(s, context);

        Assert.Contains(issues, i => i.Message.Contains("重複している"));
    }

    [Fact]
    public void PortCount3でNormalとReverseが同一だと不合格()
    {
        var s = new Switcher
        {
            Id = new SwitcherId(1),
            Base = MakeBase(),
            PortCount = 3,
            Mechanism = new SwitchMechanism { RootPortIndex = 0, NormalPortIndex = 1, ReversePortIndex = 1 },
        };
        var context = new ValidationContext();

        var issues = new SwitcherValidator().Validate(s, context);

        Assert.Contains(issues, i => i.Message.Contains("重複している"));
    }

    [Fact]
    public void PortCount3で正しいMechanismかつ接続Rail数が一致すれば合格()
    {
        var s = new Switcher
        {
            Id = new SwitcherId(1),
            Base = MakeBase(),
            PortCount = 3,
            Mechanism = new SwitchMechanism { RootPortIndex = 0, NormalPortIndex = 1, ReversePortIndex = 2 },
        };
        var rails = new[]
        {
            MakeRailToSwitcher(1, s.Id, 0),
            MakeRailToSwitcher(2, s.Id, 1),
            MakeRailToSwitcher(3, s.Id, 2),
        };
        var context = new ValidationContext { Rails = rails };

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
    public void PortCount4でValidRoutesに完全同一のPortPairが重複していると不合格()
    {
        var s = new Switcher { Id = new SwitcherId(1), Base = MakeBase(), PortCount = 4, ValidRoutes = [new PortPair(0, 1), new PortPair(0, 1)] };
        var context = new ValidationContext();

        var issues = new SwitcherValidator().Validate(s, context);

        Assert.Contains(issues, i => i.Message.Contains("重複登録"));
    }

    [Fact]
    public void PortCount4でValidRoutesに順序違いのPortPairが重複していると不合格()
    {
        var s = new Switcher { Id = new SwitcherId(1), Base = MakeBase(), PortCount = 4, ValidRoutes = [new PortPair(0, 1), new PortPair(1, 0)] };
        var context = new ValidationContext();

        var issues = new SwitcherValidator().Validate(s, context);

        Assert.Contains(issues, i => i.Message.Contains("重複登録"));
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

    // ==== §8.2項目10：PortCountと実際に接続しているRail端点数の一致検証 ====

    [Fact]
    public void 接続Rail端点数がPortCountより少なければ不合格()
    {
        var s = new Switcher
        {
            Id = new SwitcherId(1),
            Base = MakeBase(),
            PortCount = 3,
            Mechanism = new SwitchMechanism { RootPortIndex = 0, NormalPortIndex = 1, ReversePortIndex = 2 },
        };
        var rails = new[]
        {
            MakeRailToSwitcher(1, s.Id, 0),
            MakeRailToSwitcher(2, s.Id, 1),
            // Port2に接続するRailが無い
        };
        var context = new ValidationContext { Rails = rails };

        var issues = new SwitcherValidator().Validate(s, context);

        Assert.Contains(issues, i => i.Message.Contains("実際に接続しているRail端点数は2"));
    }

    [Fact]
    public void 接続Rail端点数がPortCountより多ければ不合格()
    {
        var s = new Switcher
        {
            Id = new SwitcherId(1),
            Base = MakeBase(),
            PortCount = 3,
            Mechanism = new SwitchMechanism { RootPortIndex = 0, NormalPortIndex = 1, ReversePortIndex = 2 },
        };
        var rails = new[]
        {
            MakeRailToSwitcher(1, s.Id, 0),
            MakeRailToSwitcher(2, s.Id, 1),
            MakeRailToSwitcher(3, s.Id, 2),
            MakeRailToSwitcher(4, s.Id, 2), // 余分な接続（Port2と重複）
        };
        var context = new ValidationContext { Rails = rails };

        var issues = new SwitcherValidator().Validate(s, context);

        Assert.Contains(issues, i => i.Message.Contains("実際に接続しているRail端点数は4"));
    }

    [Fact]
    public void 他のSwitcherを指すRailはカウント対象外()
    {
        var s = new Switcher
        {
            Id = new SwitcherId(1),
            Base = MakeBase(),
            PortCount = 3,
            Mechanism = new SwitchMechanism { RootPortIndex = 0, NormalPortIndex = 1, ReversePortIndex = 2 },
        };
        var otherSwitcherId = new SwitcherId(2);
        var rails = new[]
        {
            MakeRailToSwitcher(1, s.Id, 0),
            MakeRailToSwitcher(2, s.Id, 1),
            MakeRailToSwitcher(3, s.Id, 2),
            MakeRailToSwitcher(4, otherSwitcherId, 0), // 別Switcher宛。target(1)のカウントには影響しない
        };
        var context = new ValidationContext { Rails = rails };

        var issues = new SwitcherValidator().Validate(s, context);

        Assert.Empty(issues);
    }

    [Fact]
    public void EndpointBがtargetを指すRailもカウント対象()
    {
        var s = new Switcher
        {
            Id = new SwitcherId(1),
            Base = MakeBase(),
            PortCount = 3,
            Mechanism = new SwitchMechanism { RootPortIndex = 0, NormalPortIndex = 1, ReversePortIndex = 2 },
        };
        var rail = new Rail
        {
            Id = new RailId(1),
            LengthM = 10,
            SpeedLimitKph = 25,
            Role = RailRole.Normal,
            EndpointA = new NoneEndpointRef(),
            EndpointB = new SwitcherEndpointRef(s.Id, 0), // EndpointB側
        };
        var rails = new[]
        {
            rail,
            MakeRailToSwitcher(2, s.Id, 1),
            MakeRailToSwitcher(3, s.Id, 2),
        };
        var context = new ValidationContext { Rails = rails };

        var issues = new SwitcherValidator().Validate(s, context);

        Assert.Empty(issues);
    }

    [Fact]
    public void 同一PortIndexを指すRail端点が複数あれば不合格()
    {
        var s = new Switcher
        {
            Id = new SwitcherId(1),
            Base = MakeBase(),
            PortCount = 3,
            Mechanism = new SwitchMechanism { RootPortIndex = 0, NormalPortIndex = 1, ReversePortIndex = 2 },
        };
        var rails = new[]
        {
            MakeRailToSwitcher(1, s.Id, 0),
            MakeRailToSwitcher(2, s.Id, 0), // Port0の重複
            MakeRailToSwitcher(3, s.Id, 1),
        };
        var context = new ValidationContext { Rails = rails };

        var issues = new SwitcherValidator().Validate(s, context);

        Assert.Contains(issues, i => i.Message.Contains("PortIndex=0") && i.Message.Contains("配線異常"));
    }

    [Fact]
    public void 接続Rail端点のPortIndexが範囲外なら不合格()
    {
        var s = new Switcher
        {
            Id = new SwitcherId(1),
            Base = MakeBase(),
            PortCount = 3,
            Mechanism = new SwitchMechanism { RootPortIndex = 0, NormalPortIndex = 1, ReversePortIndex = 2 },
        };
        var rails = new[]
        {
            MakeRailToSwitcher(1, s.Id, 0),
            MakeRailToSwitcher(2, s.Id, 1),
            MakeRailToSwitcher(3, s.Id, 5), // PortCount=3の範囲外
        };
        var context = new ValidationContext { Rails = rails };

        var issues = new SwitcherValidator().Validate(s, context);

        Assert.Contains(issues, i => i.Message.Contains("PortIndex=5") && i.Message.Contains("範囲外"));
    }
}
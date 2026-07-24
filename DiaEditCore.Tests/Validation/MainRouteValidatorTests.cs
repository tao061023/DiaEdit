using DiaEditCore.Model;
using DiaEditCore.Serialization.Validation;
using Xunit;

namespace DiaEditCore.Tests.Validation;

public class MainRouteValidatorTests
{
    [Fact]
    public void StationDisplayNameOverridesのキーがStationOrderに無ければ不合格()
    {
        var route = new MainRoute
        {
            Id = new MainRouteId(1),
            Name = new DisplayName { Name = "テスト線" },
            StationOrder = [new StationId(1), new StationId(2)],
            StationDisplayNameOverrides = new() { [new StationId(99)] = new DisplayName { Name = "不正" } },
        };
        var context = new ValidationContext();

        var issues = new MainRouteValidator().Validate(route, context);

        Assert.Contains(issues, i => i.Message.Contains("StationDisplayNameOverrides"));
    }

    [Fact]
    public void 先頭駅がHalt駅だと不合格()
    {
        var haltStation = new Station { Id = new StationId(1), DisplayName = new DisplayName { Name = "棒線駅" }, Type = StationType.Halt };
        var route = new MainRoute
        {
            Id = new MainRouteId(1),
            Name = new DisplayName { Name = "テスト線" },
            StationOrder = [haltStation.Id, new StationId(2)],
        };
        var context = new ValidationContext { Stations = [haltStation] };

        var issues = new MainRouteValidator().Validate(route, context);

        Assert.Contains(issues, i => i.Message.Contains("Halt駅"));
    }

    [Fact]
    public void 正常なMainRouteは合格()
    {
        var s1 = new Station { Id = new StationId(1), DisplayName = new DisplayName { Name = "A駅" }, Type = StationType.Standard };
        var s2 = new Station { Id = new StationId(2), DisplayName = new DisplayName { Name = "B駅" }, Type = StationType.Standard };
        var route = new MainRoute
        {
            Id = new MainRouteId(1),
            Name = new DisplayName { Name = "テスト線" },
            StationOrder = [s1.Id, s2.Id],
        };
        var context = new ValidationContext { Stations = [s1, s2] };

        var issues = new MainRouteValidator().Validate(route, context);

        Assert.Empty(issues);
    }
}
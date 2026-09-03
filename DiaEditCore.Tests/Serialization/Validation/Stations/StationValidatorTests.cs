namespace DiaEditCore.Tests.Serialization.Validation.Stations;

using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Serialization.Validation;
using DiaEditCore.Serialization.Validation.Stations;

using Xunit;

public class StationValidatorTests
{
    private static Station MakeStation(int id) => new()
    {
        Id = new StationId(id),
        DisplayName = new DisplayName { Name = "テスト駅" },
        Type = StationType.Standard,
    };

    [Fact]
    public void FloorUnitが1件以上あれば合格()
    {
        var station = MakeStation(1);
        var floorUnit = new FloorUnit { Id = new FloorUnitId(1), StationId = station.Id, DisplayOrder = 0 };
        var context = new ValidationContext { FloorUnits = [floorUnit] };

        var issues = new StationValidator().Validate(station, context);

        Assert.Empty(issues);
    }

    [Fact]
    public void FloorUnitが0件だと不合格()
    {
        var station = MakeStation(1);
        var context = new ValidationContext(); // FloorUnits空

        var issues = new StationValidator().Validate(station, context);

        Assert.Single(issues);
    }

    [Fact]
    public void DisplayNameのnameが空文字列だと不合格()
    {
        var station = MakeStation(1);
        station.DisplayName.Name = "";
        var floorUnit = new FloorUnit { Id = new FloorUnitId(1), StationId = station.Id, DisplayOrder = 0 };
        var context = new ValidationContext { FloorUnits = [floorUnit] };

        var issues = new StationValidator().Validate(station, context);

        Assert.Contains(issues, i => i.Message.Contains("Name"));
    }
}
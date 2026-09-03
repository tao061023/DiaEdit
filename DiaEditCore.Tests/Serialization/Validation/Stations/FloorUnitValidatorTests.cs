namespace DiaEditCore.Tests.Serialization.Validation.Stations;

using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Serialization.Validation;
using DiaEditCore.Serialization.Validation.Stations;

using Xunit;

public class FloorUnitValidatorTests
{
    [Fact]
    public void 同一Station内でDisplayOrderが重複していなければ合格()
    {
        var stationId = new StationId(1);
        var a = new FloorUnit { Id = new FloorUnitId(1), StationId = stationId, DisplayOrder = 0 };
        var b = new FloorUnit { Id = new FloorUnitId(2), StationId = stationId, DisplayOrder = 1 };
        var context = new ValidationContext { FloorUnits = [a, b] };

        var issues = new FloorUnitValidator().Validate(a, context);

        Assert.Empty(issues);
    }

    [Fact]
    public void 同一Station内でDisplayOrderが重複すると不合格()
    {
        var stationId = new StationId(1);
        var a = new FloorUnit { Id = new FloorUnitId(1), StationId = stationId, DisplayOrder = 0 };
        var b = new FloorUnit { Id = new FloorUnitId(2), StationId = stationId, DisplayOrder = 0 };
        var context = new ValidationContext { FloorUnits = [a, b] };

        var issues = new FloorUnitValidator().Validate(a, context);

        Assert.Single(issues);
    }

    [Fact]
    public void 異なるStation間ではDisplayOrder重複が許容される()
    {
        var a = new FloorUnit { Id = new FloorUnitId(1), StationId = new StationId(1), DisplayOrder = 0 };
        var b = new FloorUnit { Id = new FloorUnitId(2), StationId = new StationId(2), DisplayOrder = 0 };
        var context = new ValidationContext { FloorUnits = [a, b] };

        var issues = new FloorUnitValidator().Validate(a, context);

        Assert.Empty(issues);
    }
}
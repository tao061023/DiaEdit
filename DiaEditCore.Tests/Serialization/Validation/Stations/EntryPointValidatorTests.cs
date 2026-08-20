using DiaEditCore.Model;
using DiaEditCore.Model.Stations;

using DiaEditCore.Serialization.Validation;
using DiaEditCore.Serialization.Validation.Stations;
using Xunit;

namespace DiaEditCore.Tests.Serialization.Validation.Stations;

public class EntryPointValidatorTests
{
    [Fact]
    public void FloorUnitが存在すれば合格()
    {
        var floorUnit = new FloorUnit { Id = new FloorUnitId(1), StationId = new StationId(1), DisplayOrder = 0 };
        var ep = new EntryPoint
        {
            Id = new EntryPointId(1),
            Base = new FloorUnitObjectBase { FloorUnitId = new FloorUnitId(1), Position = default },
            Type = EntryPointType.Both,
        };
        var context = new ValidationContext { FloorUnits = [floorUnit] };

        var issues = new EntryPointValidator().Validate(ep, context);

        Assert.Empty(issues);
    }

    [Fact]
    public void FloorUnitが存在しなければ不合格()
    {
        var ep = new EntryPoint
        {
            Id = new EntryPointId(1),
            Base = new FloorUnitObjectBase { FloorUnitId = new FloorUnitId(999), Position = default },
            Type = EntryPointType.Arrival,
        };
        var context = new ValidationContext();

        var issues = new EntryPointValidator().Validate(ep, context);

        Assert.Single(issues);
    }
}

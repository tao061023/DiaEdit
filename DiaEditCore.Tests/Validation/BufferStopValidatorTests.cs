using DiaEditCore.Model;
using DiaEditCore.Serialization.Validation;
using Xunit;

namespace DiaEditCore.Tests.Validation;

public class BufferStopValidatorTests
{
    [Fact]
    public void FloorUnitが存在すれば合格()
    {
        var floorUnit = new FloorUnit { Id = new FloorUnitId(1), StationId = new StationId(1), DisplayOrder = 0 };
        var bs = new BufferStop
        {
            Id = new BufferStopId(1),
            Base = new FloorUnitObjectBase { FloorUnitId = new FloorUnitId(1), Position = default },
        };
        var context = new ValidationContext { FloorUnits = [floorUnit] };

        var issues = new BufferStopValidator().Validate(bs, context);

        Assert.Empty(issues);
    }

    [Fact]
    public void FloorUnitが存在しなければ不合格()
    {
        var bs = new BufferStop
        {
            Id = new BufferStopId(1),
            Base = new FloorUnitObjectBase { FloorUnitId = new FloorUnitId(999), Position = default },
        };
        var context = new ValidationContext();

        var issues = new BufferStopValidator().Validate(bs, context);

        Assert.Single(issues);
    }
}

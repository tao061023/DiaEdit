namespace DiaEditCore.Tests.Serialization.Validation.Stations.FloorUnitObjects;

using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.Stations.FloorUnitObjects;
using DiaEditCore.Serialization.Validation;
using DiaEditCore.Serialization.Validation.Stations.FloorUnitObjects;

using Xunit;

public class PlatformValidatorTests
{
    [Fact]
    public void FloorUnitが存在しなければ不合格()
    {
        var platform = new Platform
        {
            Id = new PlatformId(1),
            Base = new FloorUnitObjectBase { FloorUnitId = new FloorUnitId(999), Position = default },
            FacingRailIds = [new RailId(1)],
        };
        var context = new ValidationContext();

        var issues = new PlatformValidator().Validate(platform, context);

        Assert.Contains(issues, i => i.Message.Contains("FloorUnitId"));
    }

    [Fact]
    public void FacingRailIdsが空なら不合格()
    {
        var floorUnit = new FloorUnit { Id = new FloorUnitId(1), StationId = new StationId(1), DisplayOrder = 0 };
        var platform = new Platform
        {
            Id = new PlatformId(1),
            Base = new FloorUnitObjectBase { FloorUnitId = new FloorUnitId(1), Position = default },
            FacingRailIds = [],
        };
        var context = new ValidationContext { FloorUnits = [floorUnit] };

        var issues = new PlatformValidator().Validate(platform, context);

        Assert.Contains(issues, i => i.Message.Contains("FacingRailIdsが空"));
    }

    [Fact]
    public void FacingRailIdsに存在しないRailIdがあれば不合格()
    {
        var floorUnit = new FloorUnit { Id = new FloorUnitId(1), StationId = new StationId(1), DisplayOrder = 0 };
        var platform = new Platform
        {
            Id = new PlatformId(1),
            Base = new FloorUnitObjectBase { FloorUnitId = new FloorUnitId(1), Position = default },
            FacingRailIds = [new RailId(999)],
        };
        var context = new ValidationContext { FloorUnits = [floorUnit] };

        var issues = new PlatformValidator().Validate(platform, context);

        Assert.Contains(issues, i => i.Message.Contains("RailId"));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-5.0)]
    public void EffectiveLengthが0以下なら不合格(double length)
    {
        var floorUnit = new FloorUnit { Id = new FloorUnitId(1), StationId = new StationId(1), DisplayOrder = 0 };
        var platform = new Platform
        {
            Id = new PlatformId(1),
            Base = new FloorUnitObjectBase { FloorUnitId = new FloorUnitId(1), Position = default },
            FacingRailIds = [new RailId(1)],
            EffectiveLength = length,
        };
        var context = new ValidationContext { FloorUnits = [floorUnit] };

        var issues = new PlatformValidator().Validate(platform, context);

        Assert.Contains(issues, i => i.Message.Contains("EffectiveLength"));
    }

    [Fact]
    public void EffectiveLength未設定なら該当エラーは出ない()
    {
        var floorUnit = new FloorUnit { Id = new FloorUnitId(1), StationId = new StationId(1), DisplayOrder = 0 };
        var platform = new Platform
        {
            Id = new PlatformId(1),
            Base = new FloorUnitObjectBase { FloorUnitId = new FloorUnitId(1), Position = default },
            FacingRailIds = [new RailId(1)],
            EffectiveLength = null,
        };
        var context = new ValidationContext { FloorUnits = [floorUnit] };

        var issues = new PlatformValidator().Validate(platform, context);

        Assert.DoesNotContain(issues, i => i.Message.Contains("EffectiveLength"));
    }
}

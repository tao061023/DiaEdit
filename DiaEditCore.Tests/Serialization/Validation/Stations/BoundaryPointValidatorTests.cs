using DiaEditCore.Model;
using DiaEditCore.Model.Stations;

using DiaEditCore.Serialization.Validation;
using DiaEditCore.Serialization.Validation.Stations;
using Xunit;

namespace DiaEditCore.Tests.Serialization.Validation.Stations;

public class BoundaryPointValidatorTests
{
    private static readonly FloorUnitId FuId = new(1);
    private static readonly StationId StId = new(1);

    private static BoundaryPoint MakeTarget(int id = 1) => new()
    {
        Id = new BoundaryPointId(id),
        Base = new FloorUnitObjectBase { FloorUnitId = FuId, Position = new Point(0, 0) },
    };

    private static FloorUnit MakeFloorUnit() => new()
    {
        Id = FuId,
        StationId = StId,
        DisplayOrder = 0,
    };

    private static Station MakeStation(StationType type) => new()
    {
        Id = StId,
        DisplayName = new DisplayName { Name = "テスト駅" },
        Type = type,
    };

    [Fact]
    public void Standard駅に配置されていれば合格()
    {
        var target = MakeTarget();
        var context = new ValidationContext
        {
            FloorUnits = [MakeFloorUnit()],
            Stations = [MakeStation(StationType.Standard)],
        };

        var issues = new BoundaryPointValidator().Validate(target, context);

        Assert.Empty(issues);
    }

    [Fact]
    public void Halt駅に配置されていると不合格()
    {
        var target = MakeTarget();
        var context = new ValidationContext
        {
            FloorUnits = [MakeFloorUnit()],
            Stations = [MakeStation(StationType.Halt)],
        };

        var issues = new BoundaryPointValidator().Validate(target, context);

        Assert.Contains(issues, i => i.Message.Contains("Halt"));
    }

    [Fact]
    public void SignalStation駅に配置されていれば合格()
    {
        var target = MakeTarget();
        var context = new ValidationContext
        {
            FloorUnits = [MakeFloorUnit()],
            Stations = [MakeStation(StationType.SignalStation)],
        };

        var issues = new BoundaryPointValidator().Validate(target, context);

        Assert.Empty(issues);
    }

    [Fact]
    public void Depot駅に配置されていれば合格()
    {
        var target = MakeTarget();
        var context = new ValidationContext
        {
            FloorUnits = [MakeFloorUnit()],
            Stations = [MakeStation(StationType.Depot)],
        };

        var issues = new BoundaryPointValidator().Validate(target, context);

        Assert.Empty(issues);
    }

    [Fact]
    public void 参照先FloorUnitが存在しない場合は判定不能として合格扱いになる()
    {
        // FloorUnitsが空＝floorUnitが見つからずstationも解決できない。
        // 現状の実装ではこのケースを「Halt駅ではない」として通過させる（横断検証未整備の暗黙の許容）。
        var target = MakeTarget();
        var context = new ValidationContext
        {
            FloorUnits = [],
            Stations = [MakeStation(StationType.Halt)],
        };

        var issues = new BoundaryPointValidator().Validate(target, context);

        Assert.Empty(issues);
    }

    [Fact]
    public void FloorUnitは存在するがStationIdが実在しない場合も合格扱いになる()
    {
        var target = MakeTarget();
        var context = new ValidationContext
        {
            FloorUnits = [MakeFloorUnit()],
            Stations = [], // StId自体が存在しない
        };

        var issues = new BoundaryPointValidator().Validate(target, context);

        Assert.Empty(issues);
    }
}

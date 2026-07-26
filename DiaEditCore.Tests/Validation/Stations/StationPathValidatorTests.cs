using DiaEditCore.Model;
using DiaEditCore.Model.Stations;

using DiaEditCore.Serialization.Validation;
using DiaEditCore.Serialization.Validation.Stations;

using Xunit;

namespace DiaEditCore.Tests.Validation.Stations;

public class StationPathValidatorTests
{
    [Fact]
    public void 隣接waypointを結ぶRailが無ければ不合格()
    {
        var sp = new StationPath
        {
            Id = new StationPathId(1),
            FloorUnitId = new FloorUnitId(1),
            Name = "EP1→BP1",
            Direction = StationPathDirection.Arrival,
            Waypoints =
            [
                new EntryPointWaypoint(new EntryPointId(1)),
                new BoundaryPointWaypoint(new BoundaryPointId(2)),
            ],
        };
        var context = new ValidationContext { Rails = [] };

        var issues = new StationPathValidator().Validate(sp, context);

        Assert.Contains(issues, i => i.Message.Contains("直接結ぶRail"));
    }

    [Fact]
    public void 隣接waypointを結ぶRailがあれば当該ルールは合格()
    {
        var rail = new Rail
        {
            Id = new RailId(1),
            LengthM = 100,
            SpeedLimitKph = 60,
            Roll = RailRoll.Normal,
            EndpointA = new EntryPointEndpointRef(new EntryPointId(1)),
            EndpointB = new BoundaryPointEndpointRef(new BoundaryPointId(2)),
        };
        var sp = new StationPath
        {
            Id = new StationPathId(1),
            FloorUnitId = new FloorUnitId(1),
            Name = "EP1→BP1",
            Direction = StationPathDirection.Arrival,
            Waypoints =
            [
                new EntryPointWaypoint(new EntryPointId(1)),
                new BoundaryPointWaypoint(new BoundaryPointId(2)),
            ],
        };
        var context = new ValidationContext { Rails = [rail] };

        var issues = new StationPathValidator().Validate(sp, context);

        Assert.DoesNotContain(issues, i => i.Message.Contains("直接結ぶRail"));
    }

    [Fact]
    public void waypointsがループしていると不合格()
    {
        var sp = new StationPath
        {
            Id = new StationPathId(1),
            FloorUnitId = new FloorUnitId(1),
            Name = "test",
            Direction = StationPathDirection.Shunting,
            Waypoints =
            [
                new BoundaryPointWaypoint(new BoundaryPointId(1)),
                new SwitcherWaypoint(new SwitcherId(2)),
                new BoundaryPointWaypoint(new BoundaryPointId(1)), // 重複
            ],
        };
        var context = new ValidationContext();

        var issues = new StationPathValidator().Validate(sp, context);

        Assert.Contains(issues, i => i.Message.Contains("ループ"));
    }

    [Fact]
    public void AdjustmentSecが負だと不合格()
    {
        var sp = new StationPath
        {
            Id = new StationPathId(1),
            FloorUnitId = new FloorUnitId(1),
            Name = "test",
            Direction = StationPathDirection.Shunting,
            Waypoints =
            [
                new BoundaryPointWaypoint(new BoundaryPointId(1)),
                new BoundaryPointWaypoint(new BoundaryPointId(2)),
            ],
            AdjustmentSec = -1,
        };
        var context = new ValidationContext();

        var issues = new StationPathValidator().Validate(sp, context);

        Assert.Contains(issues, i => i.Message.Contains("AdjustmentSec"));
    }

    [Fact]
    public void Halt駅の単一EPパスは合格()
    {
        var sp = new StationPath
        {
            Id = new StationPathId(1),
            FloorUnitId = new FloorUnitId(1),
            Name = "Halt駅EP",
            Direction = StationPathDirection.Arrival,
            Waypoints = [new EntryPointWaypoint(new EntryPointId(1))],
            AdjustmentSec = 0,
        };
        var context = new ValidationContext();

        var issues = new StationPathValidator().Validate(sp, context);

        Assert.Empty(issues);
    }
}
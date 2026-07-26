using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.TimeTable;

using DiaEditCore.Serialization.Validation;
using DiaEditCore.Serialization.Validation.Timetable;

using Xunit;

namespace DiaEditCore.Tests.Validation.TimeTable;

public class TemporaryRestrictionValidatorTests
{
    private static (Station s1, Station s2, MainRoute route, EntryPoint depEp, EntryPoint arrEp, StationConnectionSegment seg) MakeSegmentSetup()
    {
        var s1 = new Station { Id = new StationId(1), DisplayName = new DisplayName { Name = "A駅" }, Type = StationType.Standard };
        var s2 = new Station { Id = new StationId(2), DisplayName = new DisplayName { Name = "B駅" }, Type = StationType.Standard };
        var route = new MainRoute { Id = new MainRouteId(1), Name = new DisplayName { Name = "テスト線" }, StationOrder = [s1.Id, s2.Id] };

        var depEp = new EntryPoint
        {
            Id = new EntryPointId(1),
            Base = new FloorUnitObjectBase { FloorUnitId = new FloorUnitId(1), Position = new Point(0, 0) },
            Type = EntryPointType.Departure,
        };
        var arrEp = new EntryPoint
        {
            Id = new EntryPointId(2),
            Base = new FloorUnitObjectBase { FloorUnitId = new FloorUnitId(2), Position = new Point(0, 0) },
            Type = EntryPointType.Arrival,
        };

        var seg = new StationConnectionSegment
        {
            Id = new StationConnectionSegmentId(1),
            FromStationId = s1.Id,
            ToStationId = s2.Id,
            FromEntryPointId = depEp.Id,
            ToEntryPointId = arrEp.Id,
            MainRouteId = route.Id,
            BaseRunTimeSec = 120,
        };

        return (s1, s2, route, depEp, arrEp, seg);
    }

    private static DateRange MakeValidDateRange() =>
        new(new DateTime(2026, 1, 1), new DateTime(2026, 3, 31));

    [Fact]
    public void Segment参照が実在しExtraRunTimeSecとSpeedLimitKphが正常値なら合格()
    {
        var (_, _, _, _, _, seg) = MakeSegmentSetup();
        var restriction = new TemporaryRestriction(
            new TemporaryRestrictionId(1),
            new RestrictionTarget.Segment(seg.Id),
            ExtraRunTimeSec: 30,
            SpeedLimitKph: 25,
            DateRange: MakeValidDateRange(),
            Note: "工事支障");
        var context = new ValidationContext { StationConnectionSegments = [seg] };

        var issues = new TemporaryRestrictionValidator().Validate(restriction, context);

        Assert.Empty(issues);
    }

    [Fact]
    public void ExtraRunTimeSecとSpeedLimitKphがnullでも合格()
    {
        var (_, _, _, _, _, seg) = MakeSegmentSetup();
        var restriction = new TemporaryRestriction(
            new TemporaryRestrictionId(1),
            new RestrictionTarget.Segment(seg.Id),
            ExtraRunTimeSec: null, // 通行不可
            SpeedLimitKph: null,
            DateRange: MakeValidDateRange(),
            Note: "");
        var context = new ValidationContext { StationConnectionSegments = [seg] };

        var issues = new TemporaryRestrictionValidator().Validate(restriction, context);

        Assert.Empty(issues);
    }

    [Fact]
    public void 存在しないStationConnectionSegmentIdを参照すると不合格()
    {
        var restriction = new TemporaryRestriction(
            new TemporaryRestrictionId(1),
            new RestrictionTarget.Segment(new StationConnectionSegmentId(999)),
            ExtraRunTimeSec: null,
            SpeedLimitKph: null,
            DateRange: MakeValidDateRange(),
            Note: "");
        var context = new ValidationContext { StationConnectionSegments = [] };

        var issues = new TemporaryRestrictionValidator().Validate(restriction, context);

        Assert.Contains(issues, i => i.Message.Contains("999"));
    }

    [Fact]
    public void Rail参照が実在すれば合格()
    {
        var rail = new Rail
        {
            Id = new RailId(1),
            LengthM = 200,
            SpeedLimitKph = 95,
            Roll = RailRoll.Normal,
            EndpointA = new NoneEndpointRef(),
            EndpointB = new NoneEndpointRef(),
        };
        var restriction = new TemporaryRestriction(
            new TemporaryRestrictionId(1),
            new RestrictionTarget.Rail(rail.Id),
            ExtraRunTimeSec: null,
            SpeedLimitKph: null,
            DateRange: MakeValidDateRange(),
            Note: "");
        var context = new ValidationContext { Rails = [rail] };

        var issues = new TemporaryRestrictionValidator().Validate(restriction, context);

        Assert.Empty(issues);
    }

    [Fact]
    public void 存在しないRailIdを参照すると不合格()
    {
        var restriction = new TemporaryRestriction(
            new TemporaryRestrictionId(1),
            new RestrictionTarget.Rail(new RailId(999)),
            ExtraRunTimeSec: null,
            SpeedLimitKph: null,
            DateRange: MakeValidDateRange(),
            Note: "");
        var context = new ValidationContext { Rails = [] };

        var issues = new TemporaryRestrictionValidator().Validate(restriction, context);

        Assert.Contains(issues, i => i.Message.Contains("Rail"));
    }

    [Fact]
    public void DateRangeのStartがEndより後だと不合格()
    {
        var (_, _, _, _, _, seg) = MakeSegmentSetup();
        var restriction = new TemporaryRestriction(
            new TemporaryRestrictionId(1),
            new RestrictionTarget.Segment(seg.Id),
            ExtraRunTimeSec: null,
            SpeedLimitKph: null,
            DateRange: new DateRange(new DateTime(2026, 3, 31), new DateTime(2026, 1, 1)),
            Note: "");
        var context = new ValidationContext { StationConnectionSegments = [seg] };

        var issues = new TemporaryRestrictionValidator().Validate(restriction, context);

        Assert.Contains(issues, i => i.Message.Contains("DateRange"));
    }

    [Fact]
    public void ExtraRunTimeSecが負値だと不合格()
    {
        var (_, _, _, _, _, seg) = MakeSegmentSetup();
        var restriction = new TemporaryRestriction(
            new TemporaryRestrictionId(1),
            new RestrictionTarget.Segment(seg.Id),
            ExtraRunTimeSec: -1,
            SpeedLimitKph: null,
            DateRange: MakeValidDateRange(),
            Note: "");
        var context = new ValidationContext { StationConnectionSegments = [seg] };

        var issues = new TemporaryRestrictionValidator().Validate(restriction, context);

        Assert.Contains(issues, i => i.Message.Contains("ExtraRunTimeSec"));
    }

    [Fact]
    public void SpeedLimitKphが0以下だと不合格()
    {
        var (_, _, _, _, _, seg) = MakeSegmentSetup();
        var restriction = new TemporaryRestriction(
            new TemporaryRestrictionId(1),
            new RestrictionTarget.Segment(seg.Id),
            ExtraRunTimeSec: null,
            SpeedLimitKph: 0,
            DateRange: MakeValidDateRange(),
            Note: "");
        var context = new ValidationContext { StationConnectionSegments = [seg] };

        var issues = new TemporaryRestrictionValidator().Validate(restriction, context);

        Assert.Contains(issues, i => i.Message.Contains("SpeedLimitKph"));
    }
}
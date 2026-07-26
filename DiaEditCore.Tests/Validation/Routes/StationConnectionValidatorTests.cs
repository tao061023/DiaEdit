using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.Routes;

using DiaEditCore.Serialization.Validation;
using DiaEditCore.Serialization.Validation.Routes;

using Xunit;

namespace DiaEditCore.Tests.Validation.Routes;
public class StationConnectionValidatorTests
{
    private static (Station s1, Station s2, MainRoute route, EntryPoint depEp, EntryPoint arrEp, StationConnectionSegment seg) MakeSimpleTwoStationSetup()
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

    [Fact]
    public void 正常なDown方向StationConnectionは合格()
    {
        var (s1, s2, route, depEp, arrEp, seg) = MakeSimpleTwoStationSetup();
        var sc = new StationConnection { Id = new StationConnectionId(1), Name = "下り本線", MainRouteId = route.Id, Direction = StationConnectionDirection.Down, Segments = [seg.Id] };
        var context = new ValidationContext
        {
            Stations = [s1, s2],
            MainRoutes = [route],
            EntryPoints = [depEp, arrEp],
            StationConnectionSegments = [seg],
        };

        var issues = new StationConnectionValidator().Validate(sc, context);

        Assert.Empty(issues);
    }

    [Fact]
    public void 出発側EPのtypeがArrivalだと不合格()
    {
        var (s1, s2, route, depEp, arrEp, seg) = MakeSimpleTwoStationSetup();
        depEp.Type = EntryPointType.Arrival; // 出発側なのにArrival専用に変更 → 不整合

        var sc = new StationConnection { Id = new StationConnectionId(1), Name = "下り本線", MainRouteId = route.Id, Direction = StationConnectionDirection.Down, Segments = [seg.Id] };
        var context = new ValidationContext
        {
            Stations = [s1, s2],
            MainRoutes = [route],
            EntryPoints = [depEp, arrEp],
            StationConnectionSegments = [seg],
        };

        var issues = new StationConnectionValidator().Validate(sc, context);

        Assert.Contains(issues, i => i.Message.Contains("出発側EP"));
    }

    [Fact]
    public void SegmentsとStationOrderの順序が矛盾すると不合格()
    {
        var (s1, s2, route, depEp, arrEp, seg) = MakeSimpleTwoStationSetup();
        // Directionを逆にする（Up方向として登録）が、segはs1→s2のまま（Downの並び）→矛盾
        var sc = new StationConnection { Id = new StationConnectionId(1), Name = "上り本線", MainRouteId = route.Id, Direction = StationConnectionDirection.Up, Segments = [seg.Id] };
        var context = new ValidationContext
        {
            Stations = [s1, s2],
            MainRoutes = [route],
            EntryPoints = [depEp, arrEp],
            StationConnectionSegments = [seg],
        };

        var issues = new StationConnectionValidator().Validate(sc, context);

        Assert.Contains(issues, i => i.Message.Contains("期待値"));
    }
}
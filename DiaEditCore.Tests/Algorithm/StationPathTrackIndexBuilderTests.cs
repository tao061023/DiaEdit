using DiaEditCore.Algorithm;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using Xunit;

namespace DiaEditCore.Tests.Algorithm;

public class StationPathTrackIndexBuilderTests
{
    private static Rail TrackRail(int id, RailEndpointRef a, RailEndpointRef b) => new()
    {
        Id = new RailId(id),
        LengthM = 100,
        SpeedLimitKph = 60,
        Roll = RailRoll.Track,
        EndpointA = a,
        EndpointB = b,
    };

    private static StationPath Path(int id, StationPathDirection dir, params StationPathWaypoint[] wps) => new()
    {
        Id = new StationPathId(id),
        FloorUnitId = new FloorUnitId(1),
        Name = $"path{id}",
        Direction = dir,
        Waypoints = wps.ToList(),
    };

    [Fact]
    public void Build_通常駅パターン_到着BPと出発BPそれぞれ正しく引ける()
    {
        var bp1 = new BoundaryPointId(1);
        var bp2 = new BoundaryPointId(2);
        var epArrival = new EntryPointId(1);
        var epDeparture = new EntryPointId(2);

        var track = TrackRail(10, new BoundaryPointEndpointRef(bp1), new BoundaryPointEndpointRef(bp2));

        var arrivalPath = Path(1, StationPathDirection.Arrival,
            new EntryPointWaypoint(epArrival), new BoundaryPointWaypoint(bp1));
        var departurePath = Path(2, StationPathDirection.Departure,
            new BoundaryPointWaypoint(bp2), new EntryPointWaypoint(epDeparture));

        var (arrivalIndex, departureIndex) = StationPathTrackIndexBuilder.Build(
            new[] { arrivalPath, departurePath }, new[] { track });

        Assert.Equal(new StationPathId(1), arrivalIndex[(epArrival, track.Id)]);
        Assert.Equal(new StationPathId(2), departureIndex[(track.Id, epDeparture)]);
    }

    [Fact]
    public void Build_Halt駅単線パターン_EP自身がTrackRail端点として引ける()
    {
        var epA = new EntryPointId(1); // 上り方向側
        var epB = new EntryPointId(2); // 下り方向側

        var track = TrackRail(10, new EntryPointEndpointRef(epA), new EntryPointEndpointRef(epB));

        var arrivalPath = Path(1, StationPathDirection.Arrival, new EntryPointWaypoint(epA));
        var departurePath = Path(2, StationPathDirection.Departure, new EntryPointWaypoint(epB));

        var (arrivalIndex, departureIndex) = StationPathTrackIndexBuilder.Build(
            new[] { arrivalPath, departurePath }, new[] { track });

        Assert.Equal(new StationPathId(1), arrivalIndex[(epA, track.Id)]);
        Assert.Equal(new StationPathId(2), departureIndex[(track.Id, epB)]);
    }

    [Fact]
    public void Build_Halt駅複線パターン_上下線それぞれ独立したTrackRailで引ける()
    {
        var epUpArrival = new EntryPointId(1);
        var epUpDeparture = new EntryPointId(2);
        var epDownArrival = new EntryPointId(3);
        var epDownDeparture = new EntryPointId(4);

        var upTrack = TrackRail(10, new EntryPointEndpointRef(epUpArrival), new EntryPointEndpointRef(epUpDeparture));
        var downTrack = TrackRail(11, new EntryPointEndpointRef(epDownArrival), new EntryPointEndpointRef(epDownDeparture));

        var paths = new[]
        {
            Path(1, StationPathDirection.Arrival, new EntryPointWaypoint(epUpArrival)),
            Path(2, StationPathDirection.Departure, new EntryPointWaypoint(epUpDeparture)),
            Path(3, StationPathDirection.Arrival, new EntryPointWaypoint(epDownArrival)),
            Path(4, StationPathDirection.Departure, new EntryPointWaypoint(epDownDeparture)),
        };

        var (arrivalIndex, departureIndex) = StationPathTrackIndexBuilder.Build(paths, new[] { upTrack, downTrack });

        Assert.Equal(new StationPathId(1), arrivalIndex[(epUpArrival, upTrack.Id)]);
        Assert.Equal(new StationPathId(2), departureIndex[(upTrack.Id, epUpDeparture)]);
        Assert.Equal(new StationPathId(3), arrivalIndex[(epDownArrival, downTrack.Id)]);
        Assert.Equal(new StationPathId(4), departureIndex[(downTrack.Id, epDownDeparture)]);
    }

    [Fact]
    public void Build_対応するTrackRailが存在しない場合_インデックスに登録されない()
    {
        var ep = new EntryPointId(1);
        var bp = new BoundaryPointId(1);
        // TrackRailは全く別のBPを両端に持つ＝このStationPathの終端とは一致しない
        var unrelatedTrack = TrackRail(10, new BoundaryPointEndpointRef(new BoundaryPointId(99)),
            new BoundaryPointEndpointRef(new BoundaryPointId(98)));

        var arrivalPath = Path(1, StationPathDirection.Arrival,
            new EntryPointWaypoint(ep), new BoundaryPointWaypoint(bp));

        var (arrivalIndex, _) = StationPathTrackIndexBuilder.Build(
            new[] { arrivalPath }, new[] { unrelatedTrack });

        Assert.Empty(arrivalIndex);
    }

    [Fact]
    public void Build_京急蒲田型_素通り番線を経由しても終端waypointの照合だけで正しく引ける()
    {
        var epArrival = new EntryPointId(1);
        var bpFinal = new BoundaryPointId(2); // 実際に停車する番線2手前のBP
        var sw = new SwitcherId(1);

        var track2 = TrackRail(20, new BoundaryPointEndpointRef(bpFinal), new BoundaryPointEndpointRef(new BoundaryPointId(3)));

        // 到着StationPath: EP1 → (番線1を素通り) → Switcher → BP（番線2手前）
        var arrivalPath = Path(1, StationPathDirection.Arrival,
            new EntryPointWaypoint(epArrival), new SwitcherWaypoint(sw), new BoundaryPointWaypoint(bpFinal));

        var (arrivalIndex, _) = StationPathTrackIndexBuilder.Build(
            new[] { arrivalPath }, new[] { track2 });

        Assert.Equal(new StationPathId(1), arrivalIndex[(epArrival, track2.Id)]);
    }

    [Fact]
    public void Build_Shunting方向のStationPathは対象外()
    {
        var bp1 = new BoundaryPointId(1);
        var bp2 = new BoundaryPointId(2);
        var track = TrackRail(10, new BoundaryPointEndpointRef(bp1), new BoundaryPointEndpointRef(bp2));

        var shuntingPath = Path(1, StationPathDirection.Shunting,
            new BoundaryPointWaypoint(bp1), new BoundaryPointWaypoint(bp2));

        var (arrivalIndex, departureIndex) = StationPathTrackIndexBuilder.Build(
            new[] { shuntingPath }, new[] { track });

        Assert.Empty(arrivalIndex);
        Assert.Empty(departureIndex);
    }
}

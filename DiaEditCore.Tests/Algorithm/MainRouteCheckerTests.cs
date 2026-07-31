using DiaEditCore.Algorithm;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using Xunit;

namespace DiaEditCore.Tests.Algorithm;

public class MainRouteCheckerTests
{
    private static Rail TrackRail(int id, RailEndpointRef a, RailEndpointRef b) => new()
    {
        Id = new RailId(id),
        LengthM = 100,
        SpeedLimitKph = 60,
        Role = RailRole.Track,
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

    /// <summary>
    /// A-B-Cの3駅、B駅にTrack1本(trackB)を持つ簡易路線。SCS[0]=A→B、SCS[1]=B→C。
    /// B駅到着側EP(epBArr)・出発側EP(epBDep)は別個体（中間駅で異なるEPを使うケースを模す）。
    /// </summary>
    private static (
        EntryPointId epA, EntryPointId epBArr, EntryPointId epBDep, EntryPointId epC,
        IReadOnlyDictionary<(StationPathTrackIndexBuilder.BoundaryTerminal, RailId), StationPathId> arrivalIndex,
        IReadOnlyDictionary<(RailId, StationPathTrackIndexBuilder.BoundaryTerminal), StationPathId> departureIndex
    ) BuildConnectedTopology()
    {
        var epA = new EntryPointId(1);
        var epBArr = new EntryPointId(2);
        var epBDep = new EntryPointId(3);
        var epC = new EntryPointId(4);
        var bp = new BoundaryPointId(10);

        var trackB = TrackRail(1, new EntryPointEndpointRef(epBArr), new BoundaryPointEndpointRef(bp));
        // 到着側EPと出発側EPが別個体でも、同一Trackに両方接続していれば連結とみなされる
        var arrivalPath = Path(1, StationPathDirection.Arrival, new EntryPointWaypoint(epBArr), new BoundaryPointWaypoint(bp));
        var departurePath = Path(2, StationPathDirection.Departure, new BoundaryPointWaypoint(bp), new EntryPointWaypoint(epBDep));

        // epBDep自身もtrackBに直結させるため、経路のTrack判定はEP終端のRail一致で行われる。
        // ここでは出発StationPathの先頭waypoint(bp)がtrackBの端点なので、trackBがそのまま採用される。
        var (arrivalIndex, departureIndex) = StationPathTrackIndexBuilder.BuildWithBoundaryTerminals(
            new[] { arrivalPath, departurePath }, new[] { trackB });

        return (epA, epBArr, epBDep, epC, arrivalIndex, departureIndex);
    }

    [Fact]
    public void 到着側出発側のTrack集合が重複する境界はIsSatisfiedがtrueになる()
    {
        var (epA, epBArr, epBDep, epC, arrivalIndex, departureIndex) = BuildConnectedTopology();
        var sequence = new List<EntryPointId> { epA, epBArr, epBDep, epC };

        var results = MainRouteChecker.CheckBoundaryConnectivity(sequence, isLoop: false, arrivalIndex, departureIndex);

        var single = Assert.Single(results);
        Assert.Equal(0, single.BoundaryIndex);
        Assert.True(single.IsSatisfied);
    }

    [Fact]
    public void Track集合が重複しない境界はIsSatisfiedがfalseになる()
    {
        var epA = new EntryPointId(1);
        var epBArr = new EntryPointId(2);
        var epBDep = new EntryPointId(3); // 到着側とは全く別のTrackに接続
        var epC = new EntryPointId(4);

        var arrivalTrack = TrackRail(1, new EntryPointEndpointRef(epBArr), new BoundaryPointEndpointRef(new BoundaryPointId(10)));
        var departureTrack = TrackRail(2, new EntryPointEndpointRef(epBDep), new BoundaryPointEndpointRef(new BoundaryPointId(11)));

        var arrivalPath = Path(1, StationPathDirection.Arrival, new EntryPointWaypoint(epBArr), new BoundaryPointWaypoint(new BoundaryPointId(10)));
        var departurePath = Path(2, StationPathDirection.Departure, new BoundaryPointWaypoint(new BoundaryPointId(11)), new EntryPointWaypoint(epBDep));

        var (arrivalIndex, departureIndex) = StationPathTrackIndexBuilder.BuildWithBoundaryTerminals(
            new[] { arrivalPath, departurePath }, new[] { arrivalTrack, departureTrack });

        var sequence = new List<EntryPointId> { epA, epBArr, epBDep, epC };
        var results = MainRouteChecker.CheckBoundaryConnectivity(sequence, isLoop: false, arrivalIndex, departureIndex);

        var single = Assert.Single(results);
        Assert.False(single.IsSatisfied);
    }

    [Fact]
    public void isLoopがfalseなら先頭末尾境界は検証対象に含まれない()
    {
        var (epA, epBArr, epBDep, epC, arrivalIndex, departureIndex) = BuildConnectedTopology();
        var sequence = new List<EntryPointId> { epA, epBArr, epBDep, epC };

        var results = MainRouteChecker.CheckBoundaryConnectivity(sequence, isLoop: false, arrivalIndex, departureIndex);

        Assert.Single(results); // 内部境界(0)のみ。先頭/末尾境界は含まれない
    }

    [Fact]
    public void isLoopがtrueなら先頭末尾境界が追加で検証される()
    {
        var (epA, epBArr, epBDep, epC, arrivalIndex, departureIndex) = BuildConnectedTopology();
        var sequence = new List<EntryPointId> { epA, epBArr, epBDep, epC };

        var results = MainRouteChecker.CheckBoundaryConnectivity(sequence, isLoop: true, arrivalIndex, departureIndex);

        Assert.Equal(2, results.Count);
        Assert.Equal(1, results[1].BoundaryIndex); // n-1 = 2-1 = 1
        // epA(出発側候補なし)・epC(到着側候補なし)の組み合わせなので通常は不成立
        Assert.False(results[1].IsSatisfied);
    }

    [Fact]
    public void SCSが1件のみ_内部境界が存在しないため非ループなら結果は空になる()
    {
        var epA = new EntryPointId(1);
        var epB = new EntryPointId(2);
        var sequence = new List<EntryPointId> { epA, epB }; // N=1

        var emptyArrival = new Dictionary<(StationPathTrackIndexBuilder.BoundaryTerminal, RailId), StationPathId>();
        var emptyDeparture = new Dictionary<(RailId, StationPathTrackIndexBuilder.BoundaryTerminal), StationPathId>();

        var results = MainRouteChecker.CheckBoundaryConnectivity(sequence, isLoop: false, emptyArrival, emptyDeparture);

        Assert.Empty(results);
    }

    [Fact]
    public void SCSが1件のみでisLoopがtrueなら先頭末尾境界のみ検証される()
    {
        var (epA, epBArr, _, _, arrivalIndex, departureIndex) = BuildConnectedTopology();
        // N=1相当：epA→epBArrの1SCSのみのシーケンス
        var sequence = new List<EntryPointId> { epA, epBArr };

        var results = MainRouteChecker.CheckBoundaryConnectivity(sequence, isLoop: true, arrivalIndex, departureIndex);

        var single = Assert.Single(results);
        Assert.Equal(0, single.BoundaryIndex); // n-1 = 1-1 = 0
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void 長さが2の倍数でない場合は例外を投げる(int length)
    {
        var sequence = Enumerable.Range(1, length).Select(i => new EntryPointId(i)).ToList();
        var emptyArrival = new Dictionary<(StationPathTrackIndexBuilder.BoundaryTerminal, RailId), StationPathId>();
        var emptyDeparture = new Dictionary<(RailId, StationPathTrackIndexBuilder.BoundaryTerminal), StationPathId>();

        Assert.Throws<ArgumentException>(() =>
            MainRouteChecker.CheckBoundaryConnectivity(sequence, isLoop: false, emptyArrival, emptyDeparture));
    }
}
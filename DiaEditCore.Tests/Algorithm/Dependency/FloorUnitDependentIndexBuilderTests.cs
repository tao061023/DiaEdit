namespace DiaEditCore.Tests.Algorithm.Dependency;

using DiaEditCore.Algorithm.Dependency;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations;

using Xunit;

public sealed class FloorUnitDependentIndexBuilderTests
{
    private static FloorUnitObjectBase MakeBase(int floorUnitId) => new()
    {
        FloorUnitId = new FloorUnitId(floorUnitId),
        Position = new Point(0, 0)
    };

    [Fact]
    public void Build_IndexesAllSixSourceTypes()
    {
        var cache = new TimeTableSetCache();

        var boundaryPoints = new List<BoundaryPoint>
        {
            new() { Id = new BoundaryPointId(1), Base = MakeBase(10) }
        };
        var entryPoints = new List<EntryPoint>
        {
            new() { Id = new EntryPointId(1), Base = MakeBase(10), Type = EntryPointType.Both }
        };
        var bufferStops = new List<BufferStop>
        {
            new() { Id = new BufferStopId(1), Base = MakeBase(10) }
        };
        var switchers = new List<Switcher>
        {
            new() { Id = new SwitcherId(1), Base = MakeBase(10), PortCount = 3 }
        };
        var platforms = new List<Platform>
        {
            new() { Id = new PlatformId(1), Base = MakeBase(10), FacingRailIds = new List<RailId>() }
        };
        var stationPaths = new List<StationPath>
        {
            new()
            {
                Id = new StationPathId(1),
                FloorUnitId = new FloorUnitId(10),
                Name = "test",
                Direction = StationPathDirection.Arrival,
                Waypoints = new List<StationPathWaypoint>()
            }
        };

        FloorUnitDependentIndexBuilder.Build(cache, boundaryPoints, entryPoints, bufferStops, switchers, platforms, stationPaths);

        Assert.True(cache.FloorUnitDependentIndex.TryGetValue(new FloorUnitId(10), out var deps));
        Assert.Equal(6, deps!.Count);
        Assert.Contains(deps, d => d is BoundaryPointObjectId);
        Assert.Contains(deps, d => d is EntryPointObjectId);
        Assert.Contains(deps, d => d is BufferStopObjectId);
        Assert.Contains(deps, d => d is SwitcherObjectId);
        Assert.Contains(deps, d => d is PlatformObjectId);
        Assert.Contains(deps, d => d is StationPathObjectId);
    }

    [Fact]
    public void Build_ClearsPreviousContents()
    {
        var cache = new TimeTableSetCache();
        cache.FloorUnitDependentIndex[new FloorUnitId(99)] = new List<ObjectId>
        {
            new BoundaryPointObjectId(new BoundaryPointId(1))
        };

        FloorUnitDependentIndexBuilder.Build(
            cache,
            Array.Empty<BoundaryPoint>(),
            Array.Empty<EntryPoint>(),
            Array.Empty<BufferStop>(),
            Array.Empty<Switcher>(),
            Array.Empty<Platform>(),
            Array.Empty<StationPath>());

        Assert.False(cache.FloorUnitDependentIndex.ContainsKey(new FloorUnitId(99)));
    }

    [Fact]
    public void Build_SeparatesEntriesByFloorUnitId()
    {
        var cache = new TimeTableSetCache();
        var boundaryPoints = new List<BoundaryPoint>
        {
            new() { Id = new BoundaryPointId(1), Base = MakeBase(10) },
            new() { Id = new BoundaryPointId(2), Base = MakeBase(20) }
        };

        FloorUnitDependentIndexBuilder.Build(
            cache, boundaryPoints,
            Array.Empty<EntryPoint>(), Array.Empty<BufferStop>(),
            Array.Empty<Switcher>(), Array.Empty<Platform>(), Array.Empty<StationPath>());

        Assert.Single(cache.FloorUnitDependentIndex[new FloorUnitId(10)]);
        Assert.Single(cache.FloorUnitDependentIndex[new FloorUnitId(20)]);
    }
}
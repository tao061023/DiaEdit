namespace DiaEditCore.Tests.Algorithm.CacheBuilder;

using DiaEditCore.Algorithm.CacheBuilder;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;

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

        // Build()は純粋関数（Dictionaryを返すのみ、キャッシュへの書き込みは一切行わない）。
        // 戻り値そのものを検証対象とする（旧版はcache.FloorUnitDependentIndexを見ておりBuildの
        // 挙動を一切検証できていなかった）。
        var result = FloorUnitDependentIndexBuilder.Build(
            boundaryPoints, entryPoints, bufferStops, switchers, platforms, stationPaths);

        Assert.True(result.TryGetValue(new FloorUnitId(10), out var deps));
        Assert.Equal(6, deps!.Count);
        Assert.Contains(deps, d => d is BoundaryPointObjectId);
        Assert.Contains(deps, d => d is EntryPointObjectId);
        Assert.Contains(deps, d => d is BufferStopObjectId);
        Assert.Contains(deps, d => d is SwitcherObjectId);
        Assert.Contains(deps, d => d is PlatformObjectId);
        Assert.Contains(deps, d => d is StationPathObjectId);
    }

    [Fact]
    public void Build_AllInputsEmpty_ReturnsEmptyDictionary()
    {
        // Build()は純粋関数のため「以前の内容をクリアする」という概念自体が存在しない
        // （そのような責務はProjectSession.RebuildCacheIfDirty側にある）。
        // ここでは単純に「入力が全て空なら空のDictionaryを返す」ことのみを検証する。
        var result = FloorUnitDependentIndexBuilder.Build(
            Array.Empty<BoundaryPoint>(),
            Array.Empty<EntryPoint>(),
            Array.Empty<BufferStop>(),
            Array.Empty<Switcher>(),
            Array.Empty<Platform>(),
            Array.Empty<StationPath>());

        Assert.Empty(result);
    }

    [Fact]
    public void Build_SeparatesEntriesByFloorUnitId()
    {
        var boundaryPoints = new List<BoundaryPoint>
        {
            new() { Id = new BoundaryPointId(1), Base = MakeBase(10) },
            new() { Id = new BoundaryPointId(2), Base = MakeBase(20) }
        };

        var result = FloorUnitDependentIndexBuilder.Build(
            boundaryPoints,
            Array.Empty<EntryPoint>(), Array.Empty<BufferStop>(),
            Array.Empty<Switcher>(), Array.Empty<Platform>(), Array.Empty<StationPath>());

        Assert.Single(result[new FloorUnitId(10)]);
        Assert.Single(result[new FloorUnitId(20)]);
    }

    [Fact]
    public void Build_同一FloorUnitに複数種別が混在する場合すべて集約される()
    {
        // 実運用に近いケース：同一FloorUnit配下にEntryPoint・Platform・StationPathが混在
        var entryPoints = new List<EntryPoint>
        {
            new() { Id = new EntryPointId(1), Base = MakeBase(10), Type = EntryPointType.Both }
        };
        var platforms = new List<Platform>
        {
            new() { Id = new PlatformId(1), Base = MakeBase(10), FacingRailIds = new List<RailId>() }
        };

        var result = FloorUnitDependentIndexBuilder.Build(
            Array.Empty<BoundaryPoint>(), entryPoints, Array.Empty<BufferStop>(),
            Array.Empty<Switcher>(), platforms, Array.Empty<StationPath>());

        Assert.Equal(2, result[new FloorUnitId(10)].Count);
        Assert.Contains(result[new FloorUnitId(10)], d => d is EntryPointObjectId);
        Assert.Contains(result[new FloorUnitId(10)], d => d is PlatformObjectId);
    }
}
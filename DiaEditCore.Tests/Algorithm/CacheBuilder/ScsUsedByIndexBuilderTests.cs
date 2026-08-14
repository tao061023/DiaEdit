namespace DiaEditCore.Tests.Algorithm.CacheBuilder;

using DiaEditCore.Algorithm.CacheBuilder;
using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using Xunit;

public class ScsUsedByIndexBuilderTests
{
    private static StationConnection MakeSc(int id, params int[] segIds) => new()
    {
        Id = new StationConnectionId(id),
        Name = $"SC{id}",
        MainRouteId = new MainRouteId(1),
        Direction = StationConnectionDirection.Down,
        Segments = segIds.Select(s => new StationConnectionSegmentId(s)).ToList(),
    };

    [Fact]
    public void Build_単一SCS単一StationConnectionの基本ケース()
    {
        var scs = new[] { MakeSc(1, 10, 11) };

        var result = ScsUsedByIndexBuilder.Build(scs);

        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { new StationConnectionId(1) }, result[new StationConnectionSegmentId(10)]);
        Assert.Equal(new[] { new StationConnectionId(1) }, result[new StationConnectionSegmentId(11)]);
    }

    [Fact]
    public void Build_複々線等で同一SCSが複数StationConnectionに共有される場合_両方が登録される()
    {
        // 同一SCSId=10を SC1・SC2 の双方が参照するケース（複々線の共有区間を想定）
        var scs = new[] { MakeSc(1, 10), MakeSc(2, 10) };

        var result = ScsUsedByIndexBuilder.Build(scs);

        Assert.Single(result);
        Assert.Equal(
            new[] { new StationConnectionId(1), new StationConnectionId(2) },
            result[new StationConnectionSegmentId(10)]);
    }

    [Fact]
    public void Build_Segmentsが空のStationConnectionは何も登録しない()
    {
        var scs = new[] { MakeSc(1) };

        var result = ScsUsedByIndexBuilder.Build(scs);

        Assert.Empty(result);
    }
}
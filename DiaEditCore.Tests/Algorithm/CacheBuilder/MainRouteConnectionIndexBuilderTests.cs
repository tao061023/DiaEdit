namespace DiaEditCore.Tests.Algorithm.CacheBuilder;

using DiaEditCore.Algorithm.CacheBuilder;
using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using Xunit;

public class MainRouteConnectionIndexBuilderTests
{
    private static StationConnection MakeSc(int id, int mainRouteId) => new()
    {
        Id = new StationConnectionId(id),
        Name = $"SC{id}",
        MainRouteId = new MainRouteId(mainRouteId),
        Direction = StationConnectionDirection.Down,
        Segments = new List<StationConnectionSegmentId>(),
    };

    [Fact]
    public void Build_単一MainRouteに複数StationConnectionが紐づく場合_全て集約される()
    {
        var scs = new[] { MakeSc(1, 100), MakeSc(2, 100), MakeSc(3, 200) };

        var result = MainRouteConnectionIndexBuilder.Build(scs);

        Assert.Equal(2, result.Count);
        Assert.Equal(
            new[] { new StationConnectionId(1), new StationConnectionId(2) },
            result[new MainRouteId(100)]);
        Assert.Equal(
            new[] { new StationConnectionId(3) },
            result[new MainRouteId(200)]);
    }

    [Fact]
    public void Build_空リストなら空辞書を返す()
    {
        var result = MainRouteConnectionIndexBuilder.Build(Array.Empty<StationConnection>());

        Assert.Empty(result);
    }

    [Fact]
    public void Build_同一MainRouteIdに対応するリストは呼び出しごとに独立している()
    {
        var scs = new[] { MakeSc(1, 100) };

        var result1 = MainRouteConnectionIndexBuilder.Build(scs);
        result1[new MainRouteId(100)].Add(new StationConnectionId(999));

        var result2 = MainRouteConnectionIndexBuilder.Build(scs);

        Assert.Single(result2[new MainRouteId(100)]);
    }
}
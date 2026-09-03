namespace DiaEditCore.Tests.Algorithm.CacheBuilder;

using DiaEditCore.Algorithm.CacheBuilder;
using DiaEditCore.Model;
using DiaEditCore.Model.TimeTable.Trains;

using Xunit;

public class DerivedTrainsBySourceIdIndexBuilderTests
{
    private static Train MakeTrain(int id, int? sourceTrainId = null) => new()
    {
        Id = new TrainId(id),
        TimeTableSetId = new TimeTableSetId(1),
        TrainNumber = $"{id}M",
        ServiceRouteId = new ServiceRouteId(1),
        TrainTypeId = new TrainTypeId(1),
        TrainTypeName = new DisplayName { Name = "普通" },
        Nickname = new DisplayName { Name = "" },
        SourceTrainId = sourceTrainId is { } sid ? new TrainId(sid) : null,
    };

    [Fact]
    public void Build_SourceTrainIdを持つTrainが元Trainの下に集約される()
    {
        var trains = new[]
        {
            MakeTrain(1), // 元Train（複製元）
            MakeTrain(2, sourceTrainId: 1), // 1から複製
            MakeTrain(3, sourceTrainId: 1), // 1から複製（2回目のコピー）
        };

        var result = DerivedTrainsBySourceIdIndexBuilder.Build(trains);

        Assert.Single(result);
        Assert.Equal(
            new[] { new TrainId(2), new TrainId(3) },
            result[new TrainId(1)]);
    }

    [Fact]
    public void Build_SourceTrainIdがnullのTrainは登録されない()
    {
        var trains = new[] { MakeTrain(1), MakeTrain(2) };

        var result = DerivedTrainsBySourceIdIndexBuilder.Build(trains);

        Assert.Empty(result);
    }

    [Fact]
    public void Build_異なる元Trainからの複製がそれぞれ独立して集約される()
    {
        var trains = new[]
        {
            MakeTrain(1),
            MakeTrain(2),
            MakeTrain(10, sourceTrainId: 1),
            MakeTrain(20, sourceTrainId: 2),
        };

        var result = DerivedTrainsBySourceIdIndexBuilder.Build(trains);

        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { new TrainId(10) }, result[new TrainId(1)]);
        Assert.Equal(new[] { new TrainId(20) }, result[new TrainId(2)]);
    }

    [Fact]
    public void Build_空リストなら空辞書を返す()
    {
        var result = DerivedTrainsBySourceIdIndexBuilder.Build(Array.Empty<Train>());

        Assert.Empty(result);
    }
}
namespace DiaEditCore.Tests.Algorithm.CacheBuilder;

using DiaEditCore.Algorithm.CacheBuilder;
using DiaEditCore.Model;
using DiaEditCore.Model.TimeTable.Trains;

using Xunit;

public class DepartureByStationTrackIndexBuilderTests
{
    private static Train NewTrain(int id, string trainNumber) => new()
    {
        Id = new TrainId(id),
        TimeTableSetId = new TimeTableSetId(1),
        TrainNumber = trainNumber,
        ServiceRouteId = new ServiceRouteId(1),
        TrainTypeId = new TrainTypeId(1),
        TrainTypeName = new DisplayName { Name = "普通" },
        Nickname = new DisplayName { Name = "" },
        DefaultVehicleTypeId = new VehicleTypeId(1),
    };

    private static Train MakeDepartingTrain(int id, StationId stationId, int departureSeconds, RailId? trackRailId, string trainNumber = "")
    {
        var dummyTo = new StationId(9000 + id);
        var train = NewTrain(id, trainNumber);
        train.RunSegments.Add(new TrainRunSegment
        {
            FromStationId = stationId,
            ToStationId = dummyTo,
            StationConnectionId = new StationConnectionId(1),
        });
        train.StopTimesInternal[new StopKey(stationId, 0)] = new StopTime
        {
            ArrivalSeconds = -1,
            DepartureSeconds = departureSeconds,
            TrackRailId = trackRailId,
        };
        return train;
    }

    [Fact]
    public void 始発StopTimeのDepartureSecondsが未設定のTrainを除外する()
    {
        var station = new StationId(1);
        var rail = new RailId(1);
        var departing = MakeDepartingTrain(1, station, departureSeconds: -1, rail);

        var index = DepartureByStationTrackIndexBuilder.Build([departing]);

        Assert.False(index.ContainsKey((station, rail)));
    }

    [Fact]
    public void 始発StopTimeのTrackRailIdが未設定のTrainを除外する()
    {
        var station = new StationId(1);
        var departing = MakeDepartingTrain(1, station, departureSeconds: 1000, trackRailId: null);

        var index = DepartureByStationTrackIndexBuilder.Build([departing]);

        Assert.Empty(index);
    }

    [Fact]
    public void RunSegmentsが空のTrainをスキップし例外を投げない()
    {
        var train = NewTrain(1, "1000M");

        var exception = Record.Exception(() => DepartureByStationTrackIndexBuilder.Build([train]));

        Assert.Null(exception);
    }

    [Fact]
    public void 同一駅同一番線のTrainを発車時刻昇順にまとめる()
    {
        var station = new StationId(1);
        var rail = new RailId(1);
        var later = MakeDepartingTrain(1, station, departureSeconds: 1500, rail);
        var earlier = MakeDepartingTrain(2, station, departureSeconds: 1200, rail);

        var index = DepartureByStationTrackIndexBuilder.Build([later, earlier]);

        var list = index[(station, rail)];
        Assert.Equal(2, list.Count);
        Assert.Equal(earlier.Id, list[0].TrainId);
        Assert.Equal(later.Id, list[1].TrainId);
    }
}
using DiaEditCore.Algorithm;

using DiaEditCore.Model;
using DiaEditCore.Model.TimeTable;

using Xunit;

namespace DiaEditCore.Tests.Algorithm;

public class TrainConnectionResolverTests
{
    private static ProjectSettings MakeSettings(int? minTurnaroundSec)
        => new(new ValidationRules(
            MinDwellTimeSec: null,
            MinHeadwaySec: null,
            MinTurnaroundSec: minTurnaroundSec,
            TrackEntryMarginSec: null,
            TrackPassMarginSec: null,
            EnableConflictDetection: false,
            EnableCarLengthCheck: false));

    private static Train MakeTrain(
        int id, string trainNumber, StationId from, StationId to,
        int arrivalSeconds, int departureSeconds, RailId railId)
    {
        var train = new Train
        {
            Id = new TrainId(id),
            TrainNumber = trainNumber,
            ServiceRouteId = new ServiceRouteId(1),
            TrainTypeId = new TrainTypeId(1),
            TrainTypeName = new DisplayName { Name = "普通" },
            Nickname = new DisplayName { Name = "" },
            DefaultVehicleTypeId = new VehicleTypeId(1),
            RunSegments = new List<TrainRunSegment>
            {
                new() { FromStationId = from, ToStationId = to, StationConnectionId = new StationConnectionId(1) },
            },
        };
        train.StopTimes[new StopKey(from, 0)] = new StopTime { DepartureSeconds = departureSeconds, TrackRailId = railId, IsStop = true };
        train.StopTimes[new StopKey(to, 0)] = new StopTime { ArrivalSeconds = arrivalSeconds, TrackRailId = railId, IsStop = true };
        return train;
    }

    // 終着駅専用（ToStationIdのStopTimeのみ・RunSegmentsのToとして使う想定の到着列車）を作るヘルパー
    private static Train MakeArrivingTrain(int id, string trainNumber, StationId from, StationId to, int arrivalSeconds, RailId railId)
    {
        var train = new Train
        {
            Id = new TrainId(id),
            TrainNumber = trainNumber,
            ServiceRouteId = new ServiceRouteId(1),
            TrainTypeId = new TrainTypeId(1),
            TrainTypeName = new DisplayName { Name = "普通" },
            Nickname = new DisplayName { Name = "" },
            DefaultVehicleTypeId = new VehicleTypeId(1),
            RunSegments = new List<TrainRunSegment>
            {
                new() { FromStationId = from, ToStationId = to, StationConnectionId = new StationConnectionId(1) },
            },
        };
        train.StopTimes[new StopKey(to, 0)] = new StopTime { ArrivalSeconds = arrivalSeconds, TrackRailId = railId, IsStop = true };
        return train;
    }

    // 始発駅専用（FromStationIdのStopTimeのみ）を作るヘルパー
    private static Train MakeDepartingTrain(int id, string trainNumber, StationId from, StationId to, int departureSeconds, RailId railId)
    {
        var train = new Train
        {
            Id = new TrainId(id),
            TrainNumber = trainNumber,
            ServiceRouteId = new ServiceRouteId(1),
            TrainTypeId = new TrainTypeId(1),
            TrainTypeName = new DisplayName { Name = "普通" },
            Nickname = new DisplayName { Name = "" },
            DefaultVehicleTypeId = new VehicleTypeId(1),
            RunSegments = new List<TrainRunSegment>
            {
                new() { FromStationId = from, ToStationId = to, StationConnectionId = new StationConnectionId(1) },
            },
        };
        train.StopTimes[new StopKey(from, 0)] = new StopTime { DepartureSeconds = departureSeconds, TrackRailId = railId, IsStop = true };
        return train;
    }

    [Fact]
    public void 正常系_同一番線かつ余裕時分十分なら接続候補として返る()
    {
        var terminal = new StationId(1);
        var railA = new RailId(1);

        var arriving = MakeArrivingTrain(1, "1001M", new StationId(0), terminal, arrivalSeconds: 1000, railA);
        var departing = MakeDepartingTrain(2, "1002M", terminal, new StationId(2), departureSeconds: 1300, railA);

        var index = TrainConnectionResolver.BuildDepartureIndex([arriving, departing]);
        var settings = MakeSettings(minTurnaroundSec: 180);

        var result = TrainConnectionResolver.ResolveNextTrainCandidates(arriving, index, settings);

        Assert.Single(result);
        Assert.Equal(departing.Id, result[0].TrainId);
    }

    [Fact]
    public void 境界値_余裕時分がちょうどMinTurnaroundSecなら合格()
    {
        var terminal = new StationId(1);
        var rail = new RailId(1);

        var arriving = MakeArrivingTrain(1, "1001M", new StationId(0), terminal, arrivalSeconds: 1000, rail);
        var departing = MakeDepartingTrain(2, "1002M", terminal, new StationId(2), departureSeconds: 1180, rail); // ちょうど180秒後

        var index = TrainConnectionResolver.BuildDepartureIndex([arriving, departing]);
        var settings = MakeSettings(minTurnaroundSec: 180);

        var result = TrainConnectionResolver.ResolveNextTrainCandidates(arriving, index, settings);

        Assert.Single(result);
    }

    [Fact]
    public void 境界値_余裕時分が1秒でも不足すれば候補外()
    {
        var terminal = new StationId(1);
        var rail = new RailId(1);

        var arriving = MakeArrivingTrain(1, "1001M", new StationId(0), terminal, arrivalSeconds: 1000, rail);
        var departing = MakeDepartingTrain(2, "1002M", terminal, new StationId(2), departureSeconds: 1179, rail); // 179秒後（1秒不足）

        var index = TrainConnectionResolver.BuildDepartureIndex([arriving, departing]);
        var settings = MakeSettings(minTurnaroundSec: 180);

        var result = TrainConnectionResolver.ResolveNextTrainCandidates(arriving, index, settings);

        Assert.Empty(result);
    }

    [Fact]
    public void 異常系_番線が異なれば候補外()
    {
        var terminal = new StationId(1);
        var railA = new RailId(1);
        var railB = new RailId(2);

        var arriving = MakeArrivingTrain(1, "1001M", new StationId(0), terminal, arrivalSeconds: 1000, railA);
        var departing = MakeDepartingTrain(2, "1002M", terminal, new StationId(2), departureSeconds: 1300, railB);

        var index = TrainConnectionResolver.BuildDepartureIndex([arriving, departing]);
        var settings = MakeSettings(minTurnaroundSec: 180);

        var result = TrainConnectionResolver.ResolveNextTrainCandidates(arriving, index, settings);

        Assert.Empty(result);
    }

    [Fact]
    public void 異常系_駅が異なれば候補外()
    {
        var terminal = new StationId(1);
        var otherStation = new StationId(99);
        var rail = new RailId(1);

        var arriving = MakeArrivingTrain(1, "1001M", new StationId(0), terminal, arrivalSeconds: 1000, rail);
        var departing = MakeDepartingTrain(2, "1002M", otherStation, new StationId(2), departureSeconds: 1300, rail);

        var index = TrainConnectionResolver.BuildDepartureIndex([arriving, departing]);
        var settings = MakeSettings(minTurnaroundSec: 180);

        var result = TrainConnectionResolver.ResolveNextTrainCandidates(arriving, index, settings);

        Assert.Empty(result);
    }

    [Fact]
    public void MinTurnaroundSecがnullなら余裕0以上で合格する()
    {
        var terminal = new StationId(1);
        var rail = new RailId(1);

        var arriving = MakeArrivingTrain(1, "1001M", new StationId(0), terminal, arrivalSeconds: 1000, rail);
        var departing = MakeDepartingTrain(2, "1002M", terminal, new StationId(2), departureSeconds: 1000, rail); // 余裕0秒

        var index = TrainConnectionResolver.BuildDepartureIndex([arriving, departing]);
        var settings = MakeSettings(minTurnaroundSec: null);

        var result = TrainConnectionResolver.ResolveNextTrainCandidates(arriving, index, settings);

        Assert.Single(result);
    }

    [Fact]
    public void MinTurnaroundSecがnullでも到着より前の発車は候補外()
    {
        var terminal = new StationId(1);
        var rail = new RailId(1);

        var arriving = MakeArrivingTrain(1, "1001M", new StationId(0), terminal, arrivalSeconds: 1000, rail);
        var departing = MakeDepartingTrain(2, "1002M", terminal, new StationId(2), departureSeconds: 999, rail); // 到着より前

        var index = TrainConnectionResolver.BuildDepartureIndex([arriving, departing]);
        var settings = MakeSettings(minTurnaroundSec: null);

        var result = TrainConnectionResolver.ResolveNextTrainCandidates(arriving, index, settings);

        Assert.Empty(result);
    }

    [Fact]
    public void 複数候補は発車時刻昇順で返りResolveNextTrainは最短接続を採用する()
    {
        var terminal = new StationId(1);
        var rail = new RailId(1);

        var arriving = MakeArrivingTrain(1, "1001M", new StationId(0), terminal, arrivalSeconds: 1000, rail);
        var laterDeparting = MakeDepartingTrain(2, "1002M", terminal, new StationId(2), departureSeconds: 1500, rail);
        var earlierDeparting = MakeDepartingTrain(3, "1003M", terminal, new StationId(2), departureSeconds: 1300, rail);

        var index = TrainConnectionResolver.BuildDepartureIndex([arriving, laterDeparting, earlierDeparting]);
        var settings = MakeSettings(minTurnaroundSec: 180);

        var candidates = TrainConnectionResolver.ResolveNextTrainCandidates(arriving, index, settings);
        var next = TrainConnectionResolver.ResolveNextTrain(arriving, index, settings);

        Assert.Equal(2, candidates.Count);
        Assert.Equal(earlierDeparting.Id, candidates[0].TrainId);
        Assert.Equal(laterDeparting.Id, candidates[1].TrainId);
        Assert.Equal(earlierDeparting.Id, next);
    }

    [Fact]
    public void 異常系_到着StopTimeのArrivalSecondsが未設定なら空リスト()
    {
        var terminal = new StationId(1);
        var rail = new RailId(1);

        var arriving = MakeArrivingTrain(1, "1001M", new StationId(0), terminal, arrivalSeconds: -1, rail); // 未設定
        var departing = MakeDepartingTrain(2, "1002M", terminal, new StationId(2), departureSeconds: 1300, rail);

        var index = TrainConnectionResolver.BuildDepartureIndex([arriving, departing]);
        var settings = MakeSettings(minTurnaroundSec: 180);

        var result = TrainConnectionResolver.ResolveNextTrainCandidates(arriving, index, settings);

        Assert.Empty(result);
    }

    [Fact]
    public void 異常系_発車StopTimeのTrackRailIdが未設定ならBuildDepartureIndexで除外される()
    {
        var terminal = new StationId(1);
        var rail = new RailId(1);

        var arriving = MakeArrivingTrain(1, "1001M", new StationId(0), terminal, arrivalSeconds: 1000, rail);

        var departing = new Train
        {
            Id = new TrainId(2),
            TrainNumber = "1002M",
            ServiceRouteId = new ServiceRouteId(1),
            TrainTypeId = new TrainTypeId(1),
            TrainTypeName = new DisplayName { Name = "普通" },
            Nickname = new DisplayName { Name = "" },
            DefaultVehicleTypeId = new VehicleTypeId(1),
            RunSegments = new List<TrainRunSegment>
            {
                new() { FromStationId = terminal, ToStationId = new StationId(2), StationConnectionId = new StationConnectionId(1) },
            },
        };
        departing.StopTimes[new StopKey(terminal, 0)] = new StopTime { DepartureSeconds = 1300, TrackRailId = null, IsStop = true };

        var index = TrainConnectionResolver.BuildDepartureIndex([arriving, departing]);
        var settings = MakeSettings(minTurnaroundSec: 180);

        var result = TrainConnectionResolver.ResolveNextTrainCandidates(arriving, index, settings);

        Assert.Empty(result);
    }

    [Fact]
    public void 異常系_RunSegmentsが空のTrainは空リストを返す()
    {
        var arriving = new Train
        {
            Id = new TrainId(1),
            TrainNumber = "1001M",
            ServiceRouteId = new ServiceRouteId(1),
            TrainTypeId = new TrainTypeId(1),
            TrainTypeName = new DisplayName { Name = "普通" },
            Nickname = new DisplayName { Name = "" },
            DefaultVehicleTypeId = new VehicleTypeId(1),
            RunSegments = new List<TrainRunSegment>(),
        };

        var index = TrainConnectionResolver.BuildDepartureIndex([arriving]);
        var settings = MakeSettings(minTurnaroundSec: 180);

        var result = TrainConnectionResolver.ResolveNextTrainCandidates(arriving, index, settings);

        Assert.Empty(result);
    }
}

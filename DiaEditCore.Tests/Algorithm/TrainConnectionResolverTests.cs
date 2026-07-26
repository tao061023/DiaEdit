using DiaEditCore.Model;
using DiaEditCore.Model.TimeTable;

using DiaEditCore.Algorithm;
using DiaEditCore.Serialization.Validation;

using Xunit;

namespace DiaEditCore.Tests.Algorithm;

public class TrainConnectionResolverTests
{
    // -----------------------------
    // ヘルパー
    // -----------------------------

    private static ProjectSettings MakeSettings(int? minTurnaroundSec) => new(
        new ValidationRules(
            MinDwellTimeSec: 30,
            MinHeadwaySec: 120,
            MinTurnaroundSec: minTurnaroundSec,
            TrackEntryMarginSec: 60,
            TrackPassMarginSec: 10,
            EnableConflictDetection: true,
            EnableCarLengthCheck: true));

    private static Train NewTrain(int id, string trainNumber) => new()
    {
        Id = new TrainId(id),
        TrainNumber = trainNumber,
        ServiceRouteId = new ServiceRouteId(1),
        TrainTypeId = new TrainTypeId(1),
        TrainTypeName = new DisplayName { Name = "普通" },
        Nickname = new DisplayName { Name = "" },
        DefaultVehicleTypeId = new VehicleTypeId(1),
    };

    /// <summary>
    /// 到着駅(stationId)にArrivalSeconds/TrackRailIdを持つ「終着列車」を1本作る。
    /// RunSegmentsは [dummyFrom -> stationId] の1本のみ。
    /// </summary>
    private static Train MakeArrivingTrain(int id, StationId stationId, int arrivalSeconds, RailId? trackRailId, string trainNumber = "")
    {
        var dummyFrom = new StationId(9000 + id);
        var train = NewTrain(id, trainNumber);
        train.RunSegments.Add(new TrainRunSegment
        {
            FromStationId = dummyFrom,
            ToStationId = stationId,
            StationConnectionId = new StationConnectionId(1),
        });
        train.StopTimes[new StopKey(stationId, 0)] = new StopTime
        {
            ArrivalSeconds = arrivalSeconds,
            DepartureSeconds = -1,
            TrackRailId = trackRailId,
        };
        return train;
    }

    /// <summary>
    /// 始発駅(stationId)にDepartureSeconds/TrackRailIdを持つ「始発列車」を1本作る。
    /// RunSegmentsは [stationId -> dummyTo] の1本のみ。
    /// </summary>
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
        train.StopTimes[new StopKey(stationId, 0)] = new StopTime
        {
            ArrivalSeconds = -1,
            DepartureSeconds = departureSeconds,
            TrackRailId = trackRailId,
        };
        return train;
    }

    private static Dictionary<(StationId, RailId), List<(int, TrainId)>> BuildIndex(params Train[] departingTrains)
        => TrainConnectionResolver.BuildDepartureIndex(departingTrains);

    // -----------------------------
    // ResolveNextTrainCandidates
    // -----------------------------

    [Fact]
    public void 余裕時分がMinTurnaroundSecちょうどなら候補に含まれる()
    {
        var station = new StationId(1);
        var rail = new RailId(1);
        var arriving = MakeArrivingTrain(1, station, arrivalSeconds: 1000, rail);
        var departing = MakeDepartingTrain(2, station, departureSeconds: 1300, rail); // 余裕300秒
        var index = BuildIndex(departing);
        var settings = MakeSettings(minTurnaroundSec: 300);

        var candidates = TrainConnectionResolver.ResolveNextTrainCandidates(arriving, index, settings);

        Assert.Contains(candidates, c => c.TrainId == departing.Id);
    }

    [Fact]
    public void 余裕時分がMinTurnaroundSec未満なら候補から除外される()
    {
        var station = new StationId(1);
        var rail = new RailId(1);
        var arriving = MakeArrivingTrain(1, station, arrivalSeconds: 1000, rail);
        var departing = MakeDepartingTrain(2, station, departureSeconds: 1299, rail); // 余裕299秒
        var index = BuildIndex(departing);
        var settings = MakeSettings(minTurnaroundSec: 300);

        var candidates = TrainConnectionResolver.ResolveNextTrainCandidates(arriving, index, settings);

        Assert.Empty(candidates);
    }

    [Fact]
    public void MinTurnaroundSecがnullなら余裕時分0以上で候補に含まれる()
    {
        var station = new StationId(1);
        var rail = new RailId(1);
        var arriving = MakeArrivingTrain(1, station, arrivalSeconds: 1000, rail);
        var departing = MakeDepartingTrain(2, station, departureSeconds: 1000, rail); // 余裕0秒
        var index = BuildIndex(departing);
        var settings = MakeSettings(minTurnaroundSec: null);

        var candidates = TrainConnectionResolver.ResolveNextTrainCandidates(arriving, index, settings);

        Assert.Contains(candidates, c => c.TrainId == departing.Id);
    }

    [Fact]
    public void MinTurnaroundSecがnullでも発車が到着より前なら候補から除外される()
    {
        var station = new StationId(1);
        var rail = new RailId(1);
        var arriving = MakeArrivingTrain(1, station, arrivalSeconds: 1000, rail);
        var departing = MakeDepartingTrain(2, station, departureSeconds: 999, rail); // 余裕-1秒
        var index = BuildIndex(departing);
        var settings = MakeSettings(minTurnaroundSec: null);

        var candidates = TrainConnectionResolver.ResolveNextTrainCandidates(arriving, index, settings);

        Assert.Empty(candidates);
    }

    [Fact]
    public void 番線が異なれば候補に含まれない()
    {
        var station = new StationId(1);
        var railA = new RailId(1);
        var railB = new RailId(2);
        var arriving = MakeArrivingTrain(1, station, arrivalSeconds: 1000, railA);
        var departing = MakeDepartingTrain(2, station, departureSeconds: 1300, railB);
        var index = BuildIndex(departing);
        var settings = MakeSettings(minTurnaroundSec: 0);

        var candidates = TrainConnectionResolver.ResolveNextTrainCandidates(arriving, index, settings);

        Assert.Empty(candidates);
    }

    [Fact]
    public void 候補が複数存在すれば発車時刻昇順で返りResolveNextTrainは先頭を採用する()
    {
        var station = new StationId(1);
        var rail = new RailId(1);
        var arriving = MakeArrivingTrain(1, station, arrivalSeconds: 1000, rail);
        var departingLater = MakeDepartingTrain(2, station, departureSeconds: 1500, rail);
        var departingEarlier = MakeDepartingTrain(3, station, departureSeconds: 1300, rail);
        // インデックス構築順をあえて「後発→先発」にしても、結果は発車時刻昇順になることを確認
        var index = BuildIndex(departingLater, departingEarlier);
        var settings = MakeSettings(minTurnaroundSec: 0);

        var candidates = TrainConnectionResolver.ResolveNextTrainCandidates(arriving, index, settings);

        Assert.Equal(2, candidates.Count);
        Assert.Equal(departingEarlier.Id, candidates[0].TrainId);
        Assert.Equal(departingLater.Id, candidates[1].TrainId);

        var next = TrainConnectionResolver.ResolveNextTrain(arriving, index, settings);
        Assert.Equal(departingEarlier.Id, next);
    }

    [Fact]
    public void 候補が0件ならResolveNextTrainCandidatesは空リストResolveNextTrainはnull()
    {
        var station = new StationId(1);
        var rail = new RailId(1);
        var arriving = MakeArrivingTrain(1, station, arrivalSeconds: 1000, rail);
        var index = BuildIndex(); // 出発列車なし
        var settings = MakeSettings(minTurnaroundSec: 0);

        var candidates = TrainConnectionResolver.ResolveNextTrainCandidates(arriving, index, settings);
        var next = TrainConnectionResolver.ResolveNextTrain(arriving, index, settings);

        Assert.Empty(candidates);
        Assert.Null(next);
    }

    [Fact]
    public void arrivingTrain自身が同一番線の発車列車としてインデックスに存在しても自己参照は除外される()
    {
        var station = new StationId(1);
        var rail = new RailId(1);

        // 1本のTrainが「到着駅」と「同一番線からの発車」の両方を持つ折返しケースを想定し、
        // 起点となるarrivingTrainのIdがdepartureIndex内の候補と一致するケースを個別に検証する。
        // ここでは同一Trainを到着列車としても発車列車としても扱えるよう、
        // RunSegmentsとStopTimesを両方持たせる。
        var train = NewTrain(1, "1000M");
        train.RunSegments.Add(new TrainRunSegment
        {
            FromStationId = new StationId(9999),
            ToStationId = station,
            StationConnectionId = new StationConnectionId(1),
        });
        train.StopTimes[new StopKey(station, 0)] = new StopTime
        {
            ArrivalSeconds = 1000,
            DepartureSeconds = 1300, // 折返し発車
            TrackRailId = rail,
        };

        var index = BuildIndex(train); // 自分自身が発車列車としてインデックスに入る
        var settings = MakeSettings(minTurnaroundSec: 0);

        var candidates = TrainConnectionResolver.ResolveNextTrainCandidates(train, index, settings);

        Assert.Empty(candidates);
    }

    [Fact]
    public void 到着StopTimeのArrivalSecondsが未設定なら空リスト()
    {
        var station = new StationId(1);
        var rail = new RailId(1);
        var arriving = MakeArrivingTrain(1, station, arrivalSeconds: -1, rail); // 未設定
        var departing = MakeDepartingTrain(2, station, departureSeconds: 1300, rail);
        var index = BuildIndex(departing);
        var settings = MakeSettings(minTurnaroundSec: 0);

        var candidates = TrainConnectionResolver.ResolveNextTrainCandidates(arriving, index, settings);

        Assert.Empty(candidates);
    }

    [Fact]
    public void 到着StopTimeのTrackRailIdが未設定なら空リスト()
    {
        var station = new StationId(1);
        var arriving = MakeArrivingTrain(1, station, arrivalSeconds: 1000, trackRailId: null);
        var departing = MakeDepartingTrain(2, station, departureSeconds: 1300, new RailId(1));
        var index = BuildIndex(departing);
        var settings = MakeSettings(minTurnaroundSec: 0);

        var candidates = TrainConnectionResolver.ResolveNextTrainCandidates(arriving, index, settings);

        Assert.Empty(candidates);
    }

    [Fact]
    public void arrivingTrainのRunSegmentsが空なら空リスト()
    {
        var train = NewTrain(1, "1000M");
        var index = BuildIndex();
        var settings = MakeSettings(minTurnaroundSec: 0);

        var candidates = TrainConnectionResolver.ResolveNextTrainCandidates(train, index, settings);

        Assert.Empty(candidates);
    }

    // -----------------------------
    // BuildDepartureIndex
    // -----------------------------

    [Fact]
    public void BuildDepartureIndexは始発StopTimeのDepartureSecondsが未設定のTrainを除外する()
    {
        var station = new StationId(1);
        var rail = new RailId(1);
        var departing = MakeDepartingTrain(1, station, departureSeconds: -1, rail); // 未設定

        var index = TrainConnectionResolver.BuildDepartureIndex([departing]);

        Assert.False(index.ContainsKey((station, rail)));
    }

    [Fact]
    public void BuildDepartureIndexは始発StopTimeのTrackRailIdが未設定のTrainを除外する()
    {
        var station = new StationId(1);
        var departing = MakeDepartingTrain(1, station, departureSeconds: 1000, trackRailId: null);

        var index = TrainConnectionResolver.BuildDepartureIndex([departing]);

        Assert.Empty(index);
    }

    [Fact]
    public void BuildDepartureIndexはRunSegmentsが空のTrainをスキップし例外を投げない()
    {
        var train = NewTrain(1, "1000M"); // RunSegments空

        var exception = Record.Exception(() => TrainConnectionResolver.BuildDepartureIndex([train]));

        Assert.Null(exception);
    }

    [Fact]
    public void BuildDepartureIndexは同一駅同一番線のTrainを発車時刻昇順にまとめる()
    {
        var station = new StationId(1);
        var rail = new RailId(1);
        var later = MakeDepartingTrain(1, station, departureSeconds: 1500, rail);
        var earlier = MakeDepartingTrain(2, station, departureSeconds: 1200, rail);

        var index = TrainConnectionResolver.BuildDepartureIndex([later, earlier]);

        var list = index[(station, rail)];
        Assert.Equal(2, list.Count);
        Assert.Equal(earlier.Id, list[0].TrainId);
        Assert.Equal(later.Id, list[1].TrainId);
    }
}
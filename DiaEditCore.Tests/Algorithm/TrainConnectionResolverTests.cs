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
            EnableCarLengthCheck: true), 14400);

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
    /// 終着訪問のVisitSequenceはRunSegments.Count(=1)であるため、StopTimeはStopKey(stationId, 1)に登録する
    /// （StopKey(stationId, 0)は始発訪問用のキーであり、終着訪問のキーとして流用してはならない）。
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
        train.StopTimes[new StopKey(stationId, train.RunSegments.Count)] = new StopTime
        {
            ArrivalSeconds = arrivalSeconds,
            DepartureSeconds = -1,
            TrackRailId = trackRailId,
        };
        return train;
    }

    /// <summary>
    /// 始発駅(stationId)にDepartureSeconds/TrackRailIdを持つ「始発列車」を1本作る。
    /// RunSegmentsは [stationId -> dummyTo] の1本のみ。始発訪問のVisitSequenceは常に0。
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

    /// <summary>
    /// 中間駅を挟まず、station(始発)からstation2(終着)まで直接つながる1区間のTrainを作る
    /// （ResolveUniqueNextTrainMap用：同一Trainが「到着列車」にも「出発列車」にもなり得る
    /// 折返し連鎖シナリオの構築に使う）。
    /// </summary>
    private static Train MakeThroughTrain(
        int id, StationId fromStationId, StationId toStationId,
        int departureSeconds, RailId departureRail,
        int arrivalSeconds, RailId arrivalRail,
        string trainNumber = "")
    {
        var train = NewTrain(id, trainNumber);
        train.RunSegments.Add(new TrainRunSegment
        {
            FromStationId = fromStationId,
            ToStationId = toStationId,
            StationConnectionId = new StationConnectionId(1),
        });
        train.StopTimes[new StopKey(fromStationId, 0)] = new StopTime
        {
            ArrivalSeconds = -1,
            DepartureSeconds = departureSeconds,
            TrackRailId = departureRail,
        };
        train.StopTimes[new StopKey(toStationId, 1)] = new StopTime
        {
            ArrivalSeconds = arrivalSeconds,
            DepartureSeconds = -1,
            TrackRailId = arrivalRail,
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

        var train = NewTrain(1, "1000M");
        train.RunSegments.Add(new TrainRunSegment
        {
            FromStationId = new StationId(9999),
            ToStationId = station,
            StationConnectionId = new StationConnectionId(1),
        });
        // 終着訪問(VisitSequence=1)として登録する
        train.StopTimes[new StopKey(station, 1)] = new StopTime
        {
            ArrivalSeconds = 1000,
            DepartureSeconds = 1300, // 折返し発車（このStopTime自体はTrain自身の終着訪問データであり、
                                      // BuildDepartureIndexが参照する「始発訪問(VisitSequence=0)」の
                                      // FromStationId=9999には対応するStopTimeが無いため、
                                      // このTrainは発車インデックスに一切登録されない）
            TrackRailId = rail,
        };

        var index = BuildIndex(train);
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

    // -----------------------------
    // ResolveUniqueNextTrainMap / ResolveUniquePrevTrainMap
    // -----------------------------

    [Fact]
    public void 候補が1本のみなら通常のNextTrainと同じ結果になる()
    {
        var station = new StationId(1);
        var rail = new RailId(1);
        var arriving = MakeArrivingTrain(1, station, arrivalSeconds: 1000, rail);
        var departing = MakeDepartingTrain(2, station, departureSeconds: 1300, rail);
        var trains = new[] { arriving, departing };
        var index = BuildIndex(departing);
        var settings = MakeSettings(minTurnaroundSec: 0);

        var nextMap = TrainConnectionResolver.ResolveUniqueNextTrainMap(trains, index, settings);
        var prevMap = TrainConnectionResolver.ResolveUniquePrevTrainMap(trains, index, settings);

        Assert.Equal(departing.Id, nextMap[arriving.Id]);
        Assert.Equal(arriving.Id, prevMap[departing.Id]);
    }

    [Fact]
    public void 複数の到着列車が同じ出発列車を候補としても乗継時間最短の1本だけが採用される()
    {
        // 同一(終着駅, 番線)に異なる時刻で到着する2本の到着列車が、
        // 同じ出発列車(departing)を候補として選ぶケース。
        // arrivingLater(到着1500)の方が乗継時間が短い(300秒) -> こちらが唯一のPrevTrainとして採用される
        // arrivingEarlier(到着1000)は乗継時間800秒で敗れ、departingをNextTrainとして採用しない
        var station = new StationId(1);
        var rail = new RailId(1);
        var arrivingEarlier = MakeArrivingTrain(1, station, arrivalSeconds: 1000, rail, "arr-early");
        var arrivingLater = MakeArrivingTrain(2, station, arrivalSeconds: 1500, rail, "arr-late");
        var departing = MakeDepartingTrain(3, station, departureSeconds: 1800, rail, "dep");
        var trains = new[] { arrivingEarlier, arrivingLater, departing };
        var index = BuildIndex(departing);
        var settings = MakeSettings(minTurnaroundSec: 0);

        var nextMap = TrainConnectionResolver.ResolveUniqueNextTrainMap(trains, index, settings);
        var prevMap = TrainConnectionResolver.ResolveUniquePrevTrainMap(trains, index, settings);

        Assert.Equal(departing.Id, nextMap[arrivingLater.Id]);
        Assert.False(nextMap.ContainsKey(arrivingEarlier.Id));
        Assert.Equal(arrivingLater.Id, prevMap[departing.Id]);

        // NextTrainマップの値(departure側)に重複がないこと＝単射であることを直接検証する
        Assert.Single(nextMap.Values.Distinct());
    }

    [Fact]
    public void 同着の場合はTrainIdの値が小さい方が決定的に優先される()
    {
        var station = new StationId(1);
        var rail = new RailId(1);
        var arrivingA = MakeArrivingTrain(5, station, arrivalSeconds: 1000, rail, "A"); // TrainId=5
        var arrivingB = MakeArrivingTrain(2, station, arrivalSeconds: 1000, rail, "B"); // TrainId=2（同着）
        var departing = MakeDepartingTrain(9, station, departureSeconds: 1300, rail, "dep");
        var trains = new[] { arrivingA, arrivingB, departing };
        var index = BuildIndex(departing);
        var settings = MakeSettings(minTurnaroundSec: 0);

        // 走査順（trains配列の順序）を変えても結果が変わらないことを合わせて確認する
        var nextMap1 = TrainConnectionResolver.ResolveUniqueNextTrainMap(trains, index, settings);
        var nextMap2 = TrainConnectionResolver.ResolveUniqueNextTrainMap([departing, arrivingB, arrivingA], index, settings);

        Assert.Equal(arrivingB.Id, GetKeyForValue(nextMap1, departing.Id));
        Assert.Equal(arrivingB.Id, GetKeyForValue(nextMap2, departing.Id));
    }

    [Fact]
    public void 折返し連鎖で同一Trainが到着列車にも出発列車にもなるケースでもマップは単射を保つ()
    {
        // A: station1(発)->station2(着,visit1) / B: station2(発)->station3(着,visit1)
        // という2本の直行列車が、station2で折り返し接続する典型ケース。
        var rail = new RailId(1);
        var trainA = MakeThroughTrain(
            1, new StationId(1), new StationId(2),
            departureSeconds: 500, departureRail: new RailId(99), // 出発駅の番線はstation2の番線と無関係
            arrivalSeconds: 1000, arrivalRail: rail);
        var trainB = MakeThroughTrain(
            2, new StationId(2), new StationId(3),
            departureSeconds: 1300, departureRail: rail,
            arrivalSeconds: 1800, arrivalRail: new RailId(88));

        var trains = new[] { trainA, trainB };
        var index = TrainConnectionResolver.BuildDepartureIndex(trains);
        var settings = MakeSettings(minTurnaroundSec: 0);

        var nextMap = TrainConnectionResolver.ResolveUniqueNextTrainMap(trains, index, settings);
        var prevMap = TrainConnectionResolver.ResolveUniquePrevTrainMap(trains, index, settings);

        Assert.Equal(trainB.Id, nextMap[trainA.Id]);
        Assert.Equal(trainA.Id, prevMap[trainB.Id]);
    }

    private static TrainId GetKeyForValue(Dictionary<TrainId, TrainId> map, TrainId value)
        => map.Single(kv => kv.Value.Equals(value)).Key;
}
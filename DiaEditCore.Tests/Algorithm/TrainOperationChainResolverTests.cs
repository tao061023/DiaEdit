using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.TimeTable.Trains;

using DiaEditCore.Algorithm;
using DiaEditCore.Serialization.Validation;

using Xunit;

namespace DiaEditCore.Tests.Algorithm;

public class TrainOperationChainResolverTests
{
    // -----------------------------
    // ヘルパー
    // -----------------------------

    private static readonly CarCompositionId Comp1 = new(1);

    private static Train NewTrain(int id, string trainNumber = "1000M") => new()
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

    private static void AddRunSegment(Train train, int fromStationId, int toStationId)
        => train.RunSegments.Add(new TrainRunSegment
        {
            FromStationId = new StationId(fromStationId),
            ToStationId = new StationId(toStationId),
            StationConnectionId = new StationConnectionId(1),
        });

    private static ProjectSettings MakeSettings(int? minTurnaroundSec = 0) => new(
        new ValidationRules(
            MinDwellTimeSec: 30,
            MinHeadwaySec: 120,
            MinTurnaroundSec: minTurnaroundSec,
            TrackEntryMarginSec: 60,
            TrackPassMarginSec: 10,
            EnableConflictDetection: true,
            EnableCarLengthCheck: true), 14400);

    /// <summary>StartOp。Comp1に対しResolvedOperationRef(trainOperationId)を1件持つ。
    /// trainOperationId=nullの場合はProvisionalOperationRef（運用未確定）扱いとし、
    /// チェーンの起点にならないことを表す（旧「TrainOperationId未設定」相当）。</summary>
    private static StationWork StartOp(int? trainOperationId)
        => new()
        {
            Type = StationWorkType.StartOp,
            StartOpConsist =
            [
                new StartOpCarSlot
                {
                    Position = 0,
                    CarCompositionId = Comp1,
                    OperationId = trainOperationId is { } id
                        ? new ResolvedOperationRef(new TrainOperationId(id))
                        : new ProvisionalOperationRef("未確定"),
                },
            ],
        };

    /// <summary>PrevTrain。trainOperationIdを指定するとComp1に対する運用番号変更を表す（旧OpNumberChange相当）。
    /// 省略時（null）はPrevTrainOperationOverridesが空＝全Composition継承。</summary>
    private static StationWork PrevTrain(int? trainOperationId = null)
        => new()
        {
            Type = StationWorkType.PrevTrain,
            PrevTrainOperationOverrides = trainOperationId is { } id
                ? [new PrevTrainOperationOverride { CarCompositionId = Comp1, NewOperationId = new TrainOperationId(id) }]
                : [],
        };

    // -----------------------------
    // テスト
    // -----------------------------

    [Fact]
    public void 単独Train_NextTrainなしならStartOpの運用番号がそのまま登録される()
    {
        var train = NewTrain(1);
        AddRunSegment(train, 1, 2);
        train.StopTimesInternal[new StopKey(new StationId(1), 0)] = new StopTime { Works = [StartOp(100)] };
        train.StopTimesInternal[new StopKey(new StationId(2), 0)] = new StopTime();

        var index = TrainConnectionResolver.BuildDepartureIndex([train]);
        var result = TrainOperationChainResolver.Resolve([train], index, MakeSettings());

        Assert.Equal(new TrainOperationId(100), result[(train.Id, Comp1)]);
    }

    [Fact]
    public void StartOpを持たないTrainはtrainOperationIndexに登録されない()
    {
        var train = NewTrain(1);
        AddRunSegment(train, 1, 2);

        var index = TrainConnectionResolver.BuildDepartureIndex([train]);
        var result = TrainOperationChainResolver.Resolve([train], index, MakeSettings());

        Assert.DoesNotContain(result.Keys, k => k.TrainId == train.Id);
    }

    [Fact]
    public void StartOpのOperationIdがProvisionalなら登録されない()
    {
        var train = NewTrain(1);
        AddRunSegment(train, 1, 2);
        train.StopTimesInternal[new StopKey(new StationId(1), 0)] = new StopTime { Works = [StartOp(null)] };

        var index = TrainConnectionResolver.BuildDepartureIndex([train]);
        var result = TrainOperationChainResolver.Resolve([train], index, MakeSettings());

        Assert.DoesNotContain(result.Keys, k => k.TrainId == train.Id);
    }

    [Fact]
    public void NextTrainに接続されればPrevTrainの運用番号変更が無い限り同一運用番号を引き継ぐ()
    {
        var rail = new RailId(1);
        var train1 = NewTrain(1, "1001M");
        AddRunSegment(train1, 1, 2);
        train1.StopTimesInternal[new StopKey(new StationId(1), 0)] = new StopTime { Works = [StartOp(100)] };
        train1.StopTimesInternal[new StopKey(new StationId(2), 0)] = new StopTime { ArrivalSeconds = 1000, DepartureSeconds = -1, TrackRailId = rail };

        var train2 = NewTrain(2, "1002M");
        AddRunSegment(train2, 2, 3);
        train2.StopTimesInternal[new StopKey(new StationId(2), 0)] = new StopTime { ArrivalSeconds = -1, DepartureSeconds = 1300, TrackRailId = rail };

        var index = TrainConnectionResolver.BuildDepartureIndex([train1, train2]);
        var result = TrainOperationChainResolver.Resolve([train1, train2], index, MakeSettings(minTurnaroundSec: 0));

        Assert.Equal(new TrainOperationId(100), result[(train1.Id, Comp1)]);
        Assert.Equal(new TrainOperationId(100), result[(train2.Id, Comp1)]);
    }

    [Fact]
    public void NextTrainの起点StopTimeにPrevTrainの運用番号変更があれば以降の運用番号が切り替わる()
    {
        var rail = new RailId(1);
        var train1 = NewTrain(1, "1001M");
        AddRunSegment(train1, 1, 2);
        train1.StopTimesInternal[new StopKey(new StationId(1), 0)] = new StopTime { Works = [StartOp(100)] };
        train1.StopTimesInternal[new StopKey(new StationId(2), 0)] = new StopTime { ArrivalSeconds = 1000, DepartureSeconds = -1, TrackRailId = rail };

        var train2 = NewTrain(2, "1002M");
        AddRunSegment(train2, 2, 3);
        train2.StopTimesInternal[new StopKey(new StationId(2), 0)] = new StopTime
        {
            ArrivalSeconds = -1,
            DepartureSeconds = 1300,
            TrackRailId = rail,
            Works = [PrevTrain(200)],
        };

        var index = TrainConnectionResolver.BuildDepartureIndex([train1, train2]);
        var result = TrainOperationChainResolver.Resolve([train1, train2], index, MakeSettings());

        Assert.Equal(new TrainOperationId(100), result[(train1.Id, Comp1)]);
        Assert.Equal(new TrainOperationId(200), result[(train2.Id, Comp1)]);
    }

    [Fact]
    public void 連鎖3本以上でも複数回のPrevTrain運用番号変更が正しく反映される()
    {
        var rail = new RailId(1);

        var train1 = NewTrain(1, "1001M");
        AddRunSegment(train1, 1, 2);
        train1.StopTimesInternal[new StopKey(new StationId(1), 0)] = new StopTime { Works = [StartOp(100)] };
        train1.StopTimesInternal[new StopKey(new StationId(2), 0)] = new StopTime { ArrivalSeconds = 1000, DepartureSeconds = -1, TrackRailId = rail };

        var train2 = NewTrain(2, "1002M");
        AddRunSegment(train2, 2, 3);
        train2.StopTimesInternal[new StopKey(new StationId(2), 0)] = new StopTime { ArrivalSeconds = -1, DepartureSeconds = 1300, TrackRailId = rail };
        train2.StopTimesInternal[new StopKey(new StationId(3), 0)] = new StopTime { ArrivalSeconds = 2000, DepartureSeconds = -1, TrackRailId = rail };

        var train3 = NewTrain(3, "1003M");
        AddRunSegment(train3, 3, 4);
        train3.StopTimesInternal[new StopKey(new StationId(3), 0)] = new StopTime
        {
            ArrivalSeconds = -1,
            DepartureSeconds = 2300,
            TrackRailId = rail,
            Works = [PrevTrain(200)],
        };

        var trains = new[] { train1, train2, train3 };
        var index = TrainConnectionResolver.BuildDepartureIndex(trains);
        var result = TrainOperationChainResolver.Resolve(trains, index, MakeSettings());

        Assert.Equal(new TrainOperationId(100), result[(train1.Id, Comp1)]);
        Assert.Equal(new TrainOperationId(100), result[(train2.Id, Comp1)]);
        Assert.Equal(new TrainOperationId(200), result[(train3.Id, Comp1)]);
    }

    [Fact]
    public void NextTrainが見つからなければチェーンはそこで終了する()
    {
        var rail = new RailId(1);
        var otherRail = new RailId(2);

        var train1 = NewTrain(1, "1001M");
        AddRunSegment(train1, 1, 2);
        train1.StopTimesInternal[new StopKey(new StationId(1), 0)] = new StopTime { Works = [StartOp(100)] };
        train1.StopTimesInternal[new StopKey(new StationId(2), 0)] = new StopTime { ArrivalSeconds = 1000, DepartureSeconds = -1, TrackRailId = rail };

        var train2 = NewTrain(2, "1002M");
        AddRunSegment(train2, 2, 3);
        train2.StopTimesInternal[new StopKey(new StationId(2), 0)] = new StopTime { ArrivalSeconds = -1, DepartureSeconds = 1300, TrackRailId = otherRail };

        var trains = new[] { train1, train2 };
        var index = TrainConnectionResolver.BuildDepartureIndex(trains);
        var result = TrainOperationChainResolver.Resolve(trains, index, MakeSettings());

        Assert.Equal(new TrainOperationId(100), result[(train1.Id, Comp1)]);
        Assert.DoesNotContain(result.Keys, k => k.TrainId == train2.Id);
    }

    [Fact]
    public void NextTrainCandidateがResolveに渡されたTrain集合に存在しなければチェーンはそこで終了する()
    {
        var rail = new RailId(1);
        var train1 = NewTrain(1, "1001M");
        AddRunSegment(train1, 1, 2);
        train1.StopTimesInternal[new StopKey(new StationId(1), 0)] = new StopTime { Works = [StartOp(100)] };
        train1.StopTimesInternal[new StopKey(new StationId(2), 0)] = new StopTime { ArrivalSeconds = 1000, DepartureSeconds = -1, TrackRailId = rail };

        var train2 = NewTrain(2, "1002M");
        AddRunSegment(train2, 2, 3);
        train2.StopTimesInternal[new StopKey(new StationId(2), 0)] = new StopTime { ArrivalSeconds = -1, DepartureSeconds = 1300, TrackRailId = rail };

        var index = TrainConnectionResolver.BuildDepartureIndex([train1, train2]);
        var result = TrainOperationChainResolver.Resolve([train1], index, MakeSettings());

        Assert.Equal(new TrainOperationId(100), result[(train1.Id, Comp1)]);
        Assert.Single(result);
    }

    [Fact]
    public void 複数のStartOpから独立したチェーンがそれぞれ登録される()
    {
        var train1 = NewTrain(1, "1001M");
        AddRunSegment(train1, 1, 2);
        train1.StopTimesInternal[new StopKey(new StationId(1), 0)] = new StopTime { Works = [StartOp(100)] };
        train1.StopTimesInternal[new StopKey(new StationId(2), 0)] = new StopTime();

        var train2 = NewTrain(2, "2001M");
        AddRunSegment(train2, 5, 6);
        train2.StopTimesInternal[new StopKey(new StationId(5), 0)] = new StopTime { Works = [StartOp(300)] };
        train2.StopTimesInternal[new StopKey(new StationId(6), 0)] = new StopTime();

        var trains = new[] { train1, train2 };
        var index = TrainConnectionResolver.BuildDepartureIndex(trains);
        var result = TrainOperationChainResolver.Resolve(trains, index, MakeSettings());

        Assert.Equal(new TrainOperationId(100), result[(train1.Id, Comp1)]);
        Assert.Equal(new TrainOperationId(300), result[(train2.Id, Comp1)]);
    }

    [Fact]
    public void 複数の到着列車が同じ出発列車に接続候補となっても運用チェーンは一意マッチングにより破綻しない()
    {
        var rail = new RailId(1);

        var train1 = NewTrain(1, "1001M");
        AddRunSegment(train1, 1, 2);
        train1.StopTimesInternal[new StopKey(new StationId(1), 0)] = new StopTime { Works = [StartOp(100)] };
        train1.StopTimesInternal[new StopKey(new StationId(2), 0)] = new StopTime { ArrivalSeconds = 1000, DepartureSeconds = -1, TrackRailId = rail };

        var train3 = NewTrain(3, "1003M");
        AddRunSegment(train3, 4, 2);
        train3.StopTimesInternal[new StopKey(new StationId(4), 0)] = new StopTime { Works = [StartOp(999)] };
        train3.StopTimesInternal[new StopKey(new StationId(2), 0)] = new StopTime { ArrivalSeconds = 1500, DepartureSeconds = -1, TrackRailId = rail };

        var train2 = NewTrain(2, "1002M");
        AddRunSegment(train2, 2, 3);
        train2.StopTimesInternal[new StopKey(new StationId(2), 0)] = new StopTime { ArrivalSeconds = -1, DepartureSeconds = 1800, TrackRailId = rail };

        var trains = new[] { train1, train3, train2 };
        var index = TrainConnectionResolver.BuildDepartureIndex(trains);
        var result = TrainOperationChainResolver.Resolve(trains, index, MakeSettings());

        Assert.Equal(new TrainOperationId(100), result[(train1.Id, Comp1)]);
        Assert.Equal(new TrainOperationId(999), result[(train3.Id, Comp1)]);
        Assert.Equal(new TrainOperationId(999), result[(train2.Id, Comp1)]);
    }
}
using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.TimeTable;

using DiaEditCore.Algorithm;
using DiaEditCore.Serialization.Validation;

using Xunit;

namespace DiaEditCore.Tests.Algorithm;

public class TrainOperationChainResolverTests
{
    // -----------------------------
    // ヘルパー
    // -----------------------------

    private static Train NewTrain(int id, string trainNumber = "1000M") => new()
    {
        Id = new TrainId(id),
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
            EnableCarLengthCheck: true));

    private static StationWork StartOp(int? trainOperationId)
        => new()
        {
            Type = StationWorkType.StartOp,
            TrainOperationId = trainOperationId is { } id ? new TrainOperationId(id) : null,
        };

    private static StationWork OpNumberChange(int trainOperationId)
        => new() { Type = StationWorkType.OpNumberChange, TrainOperationId = new TrainOperationId(trainOperationId) };

    // -----------------------------
    // テスト
    // -----------------------------

    [Fact]
    public void 単独Train_NextTrainなしならStartOpの運用番号がそのまま登録される()
    {
        var train = NewTrain(1);
        AddRunSegment(train, 1, 2);
        train.StopTimes[new StopKey(new StationId(1), 0)] = new StopTime { Works = [StartOp(100)] };
        train.StopTimes[new StopKey(new StationId(2), 0)] = new StopTime(); // 到着情報なし＝NextTrainは見つからない

        var index = TrainConnectionResolver.BuildDepartureIndex([train]);
        var result = TrainOperationChainResolver.Resolve([train], index, MakeSettings());

        Assert.Equal(new TrainOperationId(100), result[train.Id]);
    }

    [Fact]
    public void StartOpを持たないTrainはtrainOperationIndexに登録されない()
    {
        var train = NewTrain(1);
        AddRunSegment(train, 1, 2);

        var index = TrainConnectionResolver.BuildDepartureIndex([train]);
        var result = TrainOperationChainResolver.Resolve([train], index, MakeSettings());

        Assert.False(result.ContainsKey(train.Id));
    }

    [Fact]
    public void StartOpのTrainOperationIdが未設定なら登録されない()
    {
        var train = NewTrain(1);
        AddRunSegment(train, 1, 2);
        train.StopTimes[new StopKey(new StationId(1), 0)] = new StopTime { Works = [StartOp(null)] };

        var index = TrainConnectionResolver.BuildDepartureIndex([train]);
        var result = TrainOperationChainResolver.Resolve([train], index, MakeSettings());

        Assert.False(result.ContainsKey(train.Id));
    }

    [Fact]
    public void NextTrainに接続されればOpNumberChangeが無い限り同一運用番号を引き継ぐ()
    {
        var rail = new RailId(1);
        var train1 = NewTrain(1, "1001M");
        AddRunSegment(train1, 1, 2);
        train1.StopTimes[new StopKey(new StationId(1), 0)] = new StopTime { Works = [StartOp(100)] };
        train1.StopTimes[new StopKey(new StationId(2), 0)] = new StopTime { ArrivalSeconds = 1000, DepartureSeconds = -1, TrackRailId = rail };

        var train2 = NewTrain(2, "1002M");
        AddRunSegment(train2, 2, 3);
        train2.StopTimes[new StopKey(new StationId(2), 0)] = new StopTime { ArrivalSeconds = -1, DepartureSeconds = 1300, TrackRailId = rail };

        var index = TrainConnectionResolver.BuildDepartureIndex([train1, train2]);
        var result = TrainOperationChainResolver.Resolve([train1, train2], index, MakeSettings(minTurnaroundSec: 0));

        Assert.Equal(new TrainOperationId(100), result[train1.Id]);
        Assert.Equal(new TrainOperationId(100), result[train2.Id]);
    }

    [Fact]
    public void 終着駅StopTimeにOpNumberChangeがあれば以降の運用番号が切り替わる()
    {
        var rail = new RailId(1);
        var train1 = NewTrain(1, "1001M");
        AddRunSegment(train1, 1, 2);
        train1.StopTimes[new StopKey(new StationId(1), 0)] = new StopTime { Works = [StartOp(100)] };
        train1.StopTimes[new StopKey(new StationId(2), 0)] = new StopTime
        {
            ArrivalSeconds = 1000,
            DepartureSeconds = -1,
            TrackRailId = rail,
            Works = [OpNumberChange(200)],
        };

        var train2 = NewTrain(2, "1002M");
        AddRunSegment(train2, 2, 3);
        train2.StopTimes[new StopKey(new StationId(2), 0)] = new StopTime { ArrivalSeconds = -1, DepartureSeconds = 1300, TrackRailId = rail };

        var index = TrainConnectionResolver.BuildDepartureIndex([train1, train2]);
        var result = TrainOperationChainResolver.Resolve([train1, train2], index, MakeSettings());

        Assert.Equal(new TrainOperationId(100), result[train1.Id]);
        Assert.Equal(new TrainOperationId(200), result[train2.Id]);
    }

    [Fact]
    public void 連鎖3本以上でも複数回のOpNumberChangeが正しく反映される()
    {
        var rail = new RailId(1);

        var train1 = NewTrain(1, "1001M");
        AddRunSegment(train1, 1, 2);
        train1.StopTimes[new StopKey(new StationId(1), 0)] = new StopTime { Works = [StartOp(100)] };
        train1.StopTimes[new StopKey(new StationId(2), 0)] = new StopTime { ArrivalSeconds = 1000, DepartureSeconds = -1, TrackRailId = rail };

        var train2 = NewTrain(2, "1002M");
        AddRunSegment(train2, 2, 3);
        train2.StopTimes[new StopKey(new StationId(2), 0)] = new StopTime { ArrivalSeconds = -1, DepartureSeconds = 1300, TrackRailId = rail };
        train2.StopTimes[new StopKey(new StationId(3), 0)] = new StopTime
        {
            ArrivalSeconds = 2000,
            DepartureSeconds = -1,
            TrackRailId = rail,
            Works = [OpNumberChange(200)],
        };

        var train3 = NewTrain(3, "1003M");
        AddRunSegment(train3, 3, 4);
        train3.StopTimes[new StopKey(new StationId(3), 0)] = new StopTime { ArrivalSeconds = -1, DepartureSeconds = 2300, TrackRailId = rail };

        var trains = new[] { train1, train2, train3 };
        var index = TrainConnectionResolver.BuildDepartureIndex(trains);
        var result = TrainOperationChainResolver.Resolve(trains, index, MakeSettings());

        Assert.Equal(new TrainOperationId(100), result[train1.Id]);
        Assert.Equal(new TrainOperationId(100), result[train2.Id]);
        Assert.Equal(new TrainOperationId(200), result[train3.Id]);
    }

    [Fact]
    public void NextTrainが見つからなければチェーンはそこで終了する()
    {
        var rail = new RailId(1);
        var otherRail = new RailId(2);

        var train1 = NewTrain(1, "1001M");
        AddRunSegment(train1, 1, 2);
        train1.StopTimes[new StopKey(new StationId(1), 0)] = new StopTime { Works = [StartOp(100)] };
        train1.StopTimes[new StopKey(new StationId(2), 0)] = new StopTime { ArrivalSeconds = 1000, DepartureSeconds = -1, TrackRailId = rail };

        // train2は番線が異なるため接続候補にならない
        var train2 = NewTrain(2, "1002M");
        AddRunSegment(train2, 2, 3);
        train2.StopTimes[new StopKey(new StationId(2), 0)] = new StopTime { ArrivalSeconds = -1, DepartureSeconds = 1300, TrackRailId = otherRail };

        var trains = new[] { train1, train2 };
        var index = TrainConnectionResolver.BuildDepartureIndex(trains);
        var result = TrainOperationChainResolver.Resolve(trains, index, MakeSettings());

        Assert.Equal(new TrainOperationId(100), result[train1.Id]);
        Assert.False(result.ContainsKey(train2.Id)); // train2自身はStartOpを持たないため登録されない
    }

    [Fact]
    public void NextTrainCandidateがResolveに渡されたTrain集合に存在しなければチェーンはそこで終了する()
    {
        // departureIndexはtrain1・train2の双方から構築するが、Resolve自体にはtrain1しか渡さない。
        // これにより「参照整合性エラー（NextTrain候補が実在しない）」を人為的に再現する。
        // 実際の保存データでは発生しないはずだが、発生した場合でも例外を投げず静かにチェーンを
        // 打ち切ることを保証する（参照整合性そのものの検証はSaveValidationRunner側の責務）。
        var rail = new RailId(1);
        var train1 = NewTrain(1, "1001M");
        AddRunSegment(train1, 1, 2);
        train1.StopTimes[new StopKey(new StationId(1), 0)] = new StopTime { Works = [StartOp(100)] };
        train1.StopTimes[new StopKey(new StationId(2), 0)] = new StopTime { ArrivalSeconds = 1000, DepartureSeconds = -1, TrackRailId = rail };

        var train2 = NewTrain(2, "1002M");
        AddRunSegment(train2, 2, 3);
        train2.StopTimes[new StopKey(new StationId(2), 0)] = new StopTime { ArrivalSeconds = -1, DepartureSeconds = 1300, TrackRailId = rail };

        var index = TrainConnectionResolver.BuildDepartureIndex([train1, train2]);
        var result = TrainOperationChainResolver.Resolve([train1], index, MakeSettings());

        Assert.Equal(new TrainOperationId(100), result[train1.Id]);
        Assert.Single(result); // train2は起点集合(allTrains)に含まれないため、参照整合性エラーとして無視される
    }

    [Fact]
    public void 複数のStartOpから独立したチェーンがそれぞれ登録される()
    {
        var train1 = NewTrain(1, "1001M");
        AddRunSegment(train1, 1, 2);
        train1.StopTimes[new StopKey(new StationId(1), 0)] = new StopTime { Works = [StartOp(100)] };
        train1.StopTimes[new StopKey(new StationId(2), 0)] = new StopTime();

        var train2 = NewTrain(2, "2001M");
        AddRunSegment(train2, 5, 6);
        train2.StopTimes[new StopKey(new StationId(5), 0)] = new StopTime { Works = [StartOp(300)] };
        train2.StopTimes[new StopKey(new StationId(6), 0)] = new StopTime();

        var trains = new[] { train1, train2 };
        var index = TrainConnectionResolver.BuildDepartureIndex(trains);
        var result = TrainOperationChainResolver.Resolve(trains, index, MakeSettings());

        Assert.Equal(new TrainOperationId(100), result[train1.Id]);
        Assert.Equal(new TrainOperationId(300), result[train2.Id]);
    }
}
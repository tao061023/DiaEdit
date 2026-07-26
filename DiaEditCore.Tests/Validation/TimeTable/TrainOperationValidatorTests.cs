using DiaEditCore.Model;
using DiaEditCore.Model.TimeTable;

using DiaEditCore.Serialization.Validation;
using DiaEditCore.Serialization.Validation.Timetable;

using Xunit;

namespace DiaEditCore.Tests.Validation.TimeTable;

public class TrainOperationValidatorTests
{
    private static Train MakeValidTrain(int id, string trainNumber = "1234M") => new()
    {
        Id = new TrainId(id),
        TrainNumber = trainNumber,
        ServiceRouteId = new ServiceRouteId(1),
        TrainTypeId = new TrainTypeId(1),
        TrainTypeName = new DisplayName { Name = "普通" },
        Nickname = new DisplayName { Name = "" },
        DefaultVehicleTypeId = new VehicleTypeId(1),
    };

    private static ValidationContext MakeBaseContext(params Train[] trains) => new()
    {
        Trains = trains,
    };

    private static StationWork MakeStartOp(int operationId, int startOpSeconds = 0) => new()
    {
        Type = StationWorkType.StartOp,
        TrainOperationId = new TrainOperationId(operationId),
        StartOpSeconds = startOpSeconds,
    };

    private static StationWork MakeOpNumberChange(int operationId) => new()
    {
        Type = StationWorkType.OpNumberChange,
        TrainOperationId = new TrainOperationId(operationId),
    };

    [Fact]
    public void StartOpから異なる運用番号へのOpNumberChangeは合格()
    {
        var train = MakeValidTrain(1);
        train.StopTimes[new StopKey(new StationId(1), 0)] = new StopTime
        {
            Works = [MakeStartOp(101)],
        };
        train.StopTimes[new StopKey(new StationId(2), 1)] = new StopTime
        {
            Works = [MakeOpNumberChange(102)],
        };
        var context = MakeBaseContext(train);

        var issues = new TrainOperationValidator().Validate(train, context);

        Assert.Empty(issues);
    }

    [Fact]
    public void StartOpと同一運用番号へのOpNumberChangeは不合格()
    {
        var train = MakeValidTrain(1);
        train.StopTimes[new StopKey(new StationId(1), 0)] = new StopTime
        {
            Works = [MakeStartOp(101)],
        };
        train.StopTimes[new StopKey(new StationId(2), 1)] = new StopTime
        {
            Works = [MakeOpNumberChange(101)],
        };
        var context = MakeBaseContext(train);

        var issues = new TrainOperationValidator().Validate(train, context);

        Assert.Contains(issues, i => i.Message.Contains("Rule 2違反"));
    }

    [Fact]
    public void 直前のOpNumberChangeと同一運用番号への再変更は不合格()
    {
        var train = MakeValidTrain(1);
        train.StopTimes[new StopKey(new StationId(1), 0)] = new StopTime
        {
            Works = [MakeStartOp(101)],
        };
        train.StopTimes[new StopKey(new StationId(2), 1)] = new StopTime
        {
            Works = [MakeOpNumberChange(102)], // 101→102：正当な変更
        };
        train.StopTimes[new StopKey(new StationId(3), 2)] = new StopTime
        {
            Works = [MakeOpNumberChange(102)], // 102→102：無意味な変更
        };
        var context = MakeBaseContext(train);

        var issues = new TrainOperationValidator().Validate(train, context);

        Assert.Contains(issues, i => i.Message.Contains("Rule 2違反"));
    }

    [Fact]
    public void StartOpを持たないTrainのOpNumberChangeは判定不能のためスキップされる()
    {
        // PrevTrain経由で運用を継承したTrainを想定。TrainConnectionResolver/TrainOperationChainResolverが
        // 未実装のため「現在の運用番号」が確定できず、誤検知を避けるためエラーにしない（8.2節項目6・10）。
        var train = MakeValidTrain(1);
        train.StopTimes[new StopKey(new StationId(1), 0)] = new StopTime
        {
            Works = [new StationWork { Type = StationWorkType.PrevTrain }],
        };
        train.StopTimes[new StopKey(new StationId(2), 1)] = new StopTime
        {
            Works = [MakeOpNumberChange(999)],
        };
        var context = MakeBaseContext(train);

        var issues = new TrainOperationValidator().Validate(train, context);

        Assert.Empty(issues);
    }

    [Fact]
    public void TrainOperationId未設定のOpNumberChangeは重複報告しない()
    {
        // TrainOperationIdの必須チェック自体はStationWorkValidatorの責務。
        // TrainOperationValidatorはここでエラーを追加しない（クラッシュしないことも確認）。
        var train = MakeValidTrain(1);
        train.StopTimes[new StopKey(new StationId(1), 0)] = new StopTime
        {
            Works = [MakeStartOp(101)],
        };
        train.StopTimes[new StopKey(new StationId(2), 1)] = new StopTime
        {
            Works = [new StationWork { Type = StationWorkType.OpNumberChange, TrainOperationId = null }],
        };
        var context = MakeBaseContext(train);

        var issues = new TrainOperationValidator().Validate(train, context);

        Assert.Empty(issues);
    }

    [Fact]
    public void StopTimesの辞書登録順に関わらずVisitSequence順で判定される()
    {
        // Dictionaryへの追加順をあえてVisitSequenceの昇順と逆にし、
        // OrderBy(VisitSequence)によるソートが正しく機能することを確認する。
        var train = MakeValidTrain(1);
        train.StopTimes[new StopKey(new StationId(2), 1)] = new StopTime
        {
            Works = [MakeOpNumberChange(101)], // 101→101（StartOpと同一）：本来は不合格になるべき
        };
        train.StopTimes[new StopKey(new StationId(1), 0)] = new StopTime
        {
            Works = [MakeStartOp(101)],
        };
        var context = MakeBaseContext(train);

        var issues = new TrainOperationValidator().Validate(train, context);

        Assert.Contains(issues, i => i.Message.Contains("Rule 2違反"));
    }

    [Fact]
    public void PrevTrainやShunting等はcurrentOpIdに影響しない()
    {
        var train = MakeValidTrain(1);
        train.StopTimes[new StopKey(new StationId(1), 0)] = new StopTime
        {
            Works = [MakeStartOp(101)],
        };
        train.StopTimes[new StopKey(new StationId(2), 1)] = new StopTime
        {
            Works =
            [
                new StationWork
                {
                    Type = StationWorkType.Shunting,
                    StationPathId = new StationPathId(1),
                    StartOpSeconds = 0,
                    EndOpSeconds = 10,
                },
            ],
        };
        train.StopTimes[new StopKey(new StationId(3), 2)] = new StopTime
        {
            Works = [MakeOpNumberChange(101)], // Shuntingを挟んでも直前の実運用番号は101のまま→同一なので不合格
        };
        var context = MakeBaseContext(train);

        var issues = new TrainOperationValidator().Validate(train, context);

        Assert.Contains(issues, i => i.Message.Contains("Rule 2違反"));
    }
}

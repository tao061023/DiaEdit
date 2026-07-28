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

    /// <summary>PrevTrain。TrainOperationIdを設定すると運用番号変更（旧OpNumberChange相当）を表す。省略時は継承のみ。</summary>
    private static StationWork MakePrevTrain(int? trainOperationId = null) => new()
    {
        Type = StationWorkType.PrevTrain,
        TrainOperationId = trainOperationId is { } id ? new TrainOperationId(id) : null,
    };

    [Fact]
    public void PrevTrainの運用番号変更が直前Trainと異なれば合格()
    {
        var target = MakeValidTrain(2);
        target.StopTimes[new StopKey(new StationId(2), 0)] = new StopTime
        {
            Works = [MakePrevTrain(102)],
        };
        var context = MakeBaseContext(target);
        var crossData = new TrainCrossValidationData
        {
            PrevTrainMap = new Dictionary<TrainId, TrainId> { [target.Id] = new TrainId(1) },
            TrainOperationIndex = new Dictionary<TrainId, TrainOperationId> { [new TrainId(1)] = new TrainOperationId(101) },
        };

        var issues = new TrainOperationValidator().Validate(target, context, crossData);

        Assert.Empty(issues);
    }

    [Fact]
    public void PrevTrainの運用番号変更が直前Trainと同一なら不合格()
    {
        var target = MakeValidTrain(2);
        target.StopTimes[new StopKey(new StationId(2), 0)] = new StopTime
        {
            Works = [MakePrevTrain(101)],
        };
        var context = MakeBaseContext(target);
        var crossData = new TrainCrossValidationData
        {
            PrevTrainMap = new Dictionary<TrainId, TrainId> { [target.Id] = new TrainId(1) },
            TrainOperationIndex = new Dictionary<TrainId, TrainOperationId> { [new TrainId(1)] = new TrainOperationId(101) },
        };

        var issues = new TrainOperationValidator().Validate(target, context, crossData);

        Assert.Contains(issues, i => i.Message.Contains("Rule 2違反"));
    }

    [Fact]
    public void PrevTrainのTrainOperationId省略なら継承のみのためRule2の対象外()
    {
        var target = MakeValidTrain(2);
        target.StopTimes[new StopKey(new StationId(2), 0)] = new StopTime
        {
            Works = [MakePrevTrain(trainOperationId: null)],
        };
        var context = MakeBaseContext(target);
        var crossData = new TrainCrossValidationData
        {
            PrevTrainMap = new Dictionary<TrainId, TrainId> { [target.Id] = new TrainId(1) },
            TrainOperationIndex = new Dictionary<TrainId, TrainOperationId> { [new TrainId(1)] = new TrainOperationId(101) },
        };

        var issues = new TrainOperationValidator().Validate(target, context, crossData);

        Assert.Empty(issues);
    }

    [Fact]
    public void StartOpのみでPrevTrainWorkが無ければRule2の対象外()
    {
        var target = MakeValidTrain(1);
        target.StopTimes[new StopKey(new StationId(1), 0)] = new StopTime
        {
            Works = [MakeStartOp(100)],
        };
        var context = MakeBaseContext(target);
        var crossData = new TrainCrossValidationData();

        var issues = new TrainOperationValidator().Validate(target, context, crossData);

        Assert.Empty(issues);
    }

    [Fact]
    public void crossDataがnullなら判定不能のためスキップされる()
    {
        // IValidator<Train>契約を満たす2引数版（SaveValidationRunner未対応の文脈からの呼び出しを想定）。
        var target = MakeValidTrain(2);
        target.StopTimes[new StopKey(new StationId(2), 0)] = new StopTime
        {
            Works = [MakePrevTrain(101)],
        };
        var context = MakeBaseContext(target);

        var issues = new TrainOperationValidator().Validate(target, context); // crossDataなし

        Assert.Empty(issues);
    }

    [Fact]
    public void PrevTrainMapに直前Trainのエントリが無ければ判定不能としてスキップされる()
    {
        var target = MakeValidTrain(2);
        target.StopTimes[new StopKey(new StationId(2), 0)] = new StopTime
        {
            Works = [MakePrevTrain(101)],
        };
        var context = MakeBaseContext(target);
        var crossData = new TrainCrossValidationData(); // PrevTrainMapが空

        var issues = new TrainOperationValidator().Validate(target, context, crossData);

        Assert.Empty(issues);
    }

    [Fact]
    public void TrainOperationIndexに直前Trainのエントリが無ければ判定不能としてスキップされる()
    {
        var target = MakeValidTrain(2);
        target.StopTimes[new StopKey(new StationId(2), 0)] = new StopTime
        {
            Works = [MakePrevTrain(101)],
        };
        var context = MakeBaseContext(target);
        var crossData = new TrainCrossValidationData
        {
            PrevTrainMap = new Dictionary<TrainId, TrainId> { [target.Id] = new TrainId(1) },
            // TrainOperationIndexにTrainId(1)のエントリが無い
        };

        var issues = new TrainOperationValidator().Validate(target, context, crossData);

        Assert.Empty(issues);
    }

    [Fact]
    public void StopTimesの辞書登録順に関わらずPrevTrainWorkが検出される()
    {
        var target = MakeValidTrain(2);
        // Dictionaryへの追加順をVisitSequence昇順と逆にしても、
        // Works走査自体はVisitSequence順に依存しない（PrevTrainは起点駅にのみ現れる想定のため）ことを確認する。
        target.StopTimes[new StopKey(new StationId(3), 1)] = new StopTime();
        target.StopTimes[new StopKey(new StationId(2), 0)] = new StopTime
        {
            Works = [MakePrevTrain(101)],
        };
        var context = MakeBaseContext(target);
        var crossData = new TrainCrossValidationData
        {
            PrevTrainMap = new Dictionary<TrainId, TrainId> { [target.Id] = new TrainId(1) },
            TrainOperationIndex = new Dictionary<TrainId, TrainOperationId> { [new TrainId(1)] = new TrainOperationId(101) },
        };

        var issues = new TrainOperationValidator().Validate(target, context, crossData);

        Assert.Contains(issues, i => i.Message.Contains("Rule 2違反"));
    }
}
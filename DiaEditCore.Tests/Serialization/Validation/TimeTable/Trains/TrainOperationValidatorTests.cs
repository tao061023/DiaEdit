using DiaEditCore.Model;
using DiaEditCore.Model.TimeTable.Trains;

using DiaEditCore.Serialization.Validation;
using DiaEditCore.Serialization.Validation.TimeTable.Trains;
using Xunit;

namespace DiaEditCore.Tests.Serialization.Validation.TimeTable.Trains;

public class TrainOperationValidatorTests
{
    private static readonly CarCompositionId Comp1 = new(1);

    private static Train MakeValidTrain(int id, string trainNumber = "1234M") => new()
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

    private static ValidationContext MakeBaseContext(params Train[] trains) => new()
    {
        Trains = trains,
    };

    private static StationWork MakeStartOp(string operationNumber, int startOpSeconds = 0) => new()
    {
        Type = StationWorkType.StartOp,
        StartOpSeconds = startOpSeconds,
        StartOpConsist =
        [
            new StartOpCarSlot { Position = 0, CarCompositionId = Comp1, OperationNumber = operationNumber },
        ],
    };

    /// <summary>PrevTrain。newOpNumberを指定するとComp1に対する表示上書きを表す。
    /// 省略時はPrevTrainOperationOverridesが空＝継承のみ。</summary>
    private static StationWork MakePrevTrain(string? newOpNumber = null) => new()
    {
        Type = StationWorkType.PrevTrain,
        PrevTrainOperationOverrides = newOpNumber is not null
            ? [new PrevTrainOperationOverride { CarCompositionId = Comp1, NewOpNumber = newOpNumber }]
            : [],
    };

    /// <summary>直前TrainのTrainOperationIndexエントリと、それに対応するTrainOperation実体
    /// （OperationNumber解決用）をまとめて構築するヘルパー。</summary>
    private static TrainCrossValidationData MakeCrossData(
        TrainId targetId, TrainId prevTrainId, TrainOperationId prevOpId, string prevOpNumber)
        => new()
        {
            PrevTrainMap = new Dictionary<TrainId, TrainId> { [targetId] = prevTrainId },
            TrainOperationIndex = new Dictionary<(TrainId, CarCompositionId), TrainOperationId>
            {
                [(prevTrainId, Comp1)] = prevOpId,
            },
            TrainOperationsById = new Dictionary<TrainOperationId, TrainOperation>
            {
                [prevOpId] = new TrainOperation { Id = prevOpId, OperationNumber = prevOpNumber },
            },
        };

    [Fact]
    public void PrevTrainの運用番号表示上書きが直前Trainと異なれば合格()
    {
        var target = MakeValidTrain(2);
        target.StopTimesInternal[new StopKey(new StationId(2), 0)] = new StopTime
        {
            Works = [MakePrevTrain("102")],
        };
        var context = MakeBaseContext(target);
        var crossData = MakeCrossData(target.Id, new TrainId(1), new TrainOperationId(101), "101");

        var issues = new TrainOperationValidator().Validate(target, context, crossData);

        Assert.Empty(issues);
    }

    [Fact]
    public void PrevTrainの運用番号表示上書きが直前Trainと同一なら不合格()
    {
        var target = MakeValidTrain(2);
        target.StopTimesInternal[new StopKey(new StationId(2), 0)] = new StopTime
        {
            Works = [MakePrevTrain("101")],
        };
        var context = MakeBaseContext(target);
        var crossData = MakeCrossData(target.Id, new TrainId(1), new TrainOperationId(101), "101");

        var issues = new TrainOperationValidator().Validate(target, context, crossData);

        Assert.Contains(issues, i => i.Message.Contains("Rule 2違反"));
    }

    [Fact]
    public void PrevTrainのOperationOverride省略なら継承のみのためRule2の対象外()
    {
        var target = MakeValidTrain(2);
        target.StopTimesInternal[new StopKey(new StationId(2), 0)] = new StopTime
        {
            Works = [MakePrevTrain(newOpNumber: null)],
        };
        var context = MakeBaseContext(target);
        var crossData = MakeCrossData(target.Id, new TrainId(1), new TrainOperationId(101), "101");

        var issues = new TrainOperationValidator().Validate(target, context, crossData);

        Assert.Empty(issues);
    }

    [Fact]
    public void StartOpのみでPrevTrainWorkが無ければRule2の対象外()
    {
        var target = MakeValidTrain(1);
        target.StopTimesInternal[new StopKey(new StationId(1), 0)] = new StopTime
        {
            Works = [MakeStartOp("100")],
        };
        var context = MakeBaseContext(target);
        var crossData = new TrainCrossValidationData();

        var issues = new TrainOperationValidator().Validate(target, context, crossData);

        Assert.Empty(issues);
    }

    [Fact]
    public void crossDataがnullなら判定不能のためスキップされる()
    {
        var target = MakeValidTrain(2);
        target.StopTimesInternal[new StopKey(new StationId(2), 0)] = new StopTime
        {
            Works = [MakePrevTrain("101")],
        };
        var context = MakeBaseContext(target);

        var issues = new TrainOperationValidator().Validate(target, context); // crossDataなし

        Assert.Empty(issues);
    }

    [Fact]
    public void PrevTrainMapに直前Trainのエントリが無ければ判定不能としてスキップされる()
    {
        var target = MakeValidTrain(2);
        target.StopTimesInternal[new StopKey(new StationId(2), 0)] = new StopTime
        {
            Works = [MakePrevTrain("101")],
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
        target.StopTimesInternal[new StopKey(new StationId(2), 0)] = new StopTime
        {
            Works = [MakePrevTrain("101")],
        };
        var context = MakeBaseContext(target);
        var crossData = new TrainCrossValidationData
        {
            PrevTrainMap = new Dictionary<TrainId, TrainId> { [target.Id] = new TrainId(1) },
            // TrainOperationIndexに(TrainId(1),Comp1)のエントリが無い
        };

        var issues = new TrainOperationValidator().Validate(target, context, crossData);

        Assert.Empty(issues);
    }

    [Fact]
    public void TrainOperationsByIdに直前Trainの運用が存在しなければ判定不能としてスキップされる()
    {
        // vNEXT新規：TrainOperationIndexにエントリはあるが、対応するTrainOperation実体が
        // TrainOperationsByIdに存在しない（データ不整合）場合、prevOpNumberがnullとなり
        // 等値比較が成立しないため、Rule 2判定はスキップされる（誤検知を避ける安全側の挙動）。
        var target = MakeValidTrain(2);
        target.StopTimesInternal[new StopKey(new StationId(2), 0)] = new StopTime
        {
            Works = [MakePrevTrain("101")],
        };
        var context = MakeBaseContext(target);
        var crossData = new TrainCrossValidationData
        {
            PrevTrainMap = new Dictionary<TrainId, TrainId> { [target.Id] = new TrainId(1) },
            TrainOperationIndex = new Dictionary<(TrainId, CarCompositionId), TrainOperationId>
            {
                [(new TrainId(1), Comp1)] = new TrainOperationId(101),
            },
            // TrainOperationsByIdにTrainOperationId(101)のエントリが無い
        };

        var issues = new TrainOperationValidator().Validate(target, context, crossData);

        Assert.Empty(issues);
    }

    [Fact]
    public void StopTimesの辞書登録順に関わらずPrevTrainWorkが検出される()
    {
        var target = MakeValidTrain(2);
        target.StopTimesInternal[new StopKey(new StationId(3), 1)] = new StopTime();
        target.StopTimesInternal[new StopKey(new StationId(2), 0)] = new StopTime
        {
            Works = [MakePrevTrain("101")],
        };
        var context = MakeBaseContext(target);
        var crossData = MakeCrossData(target.Id, new TrainId(1), new TrainOperationId(101), "101");

        var issues = new TrainOperationValidator().Validate(target, context, crossData);

        Assert.Contains(issues, i => i.Message.Contains("Rule 2違反"));
    }
}
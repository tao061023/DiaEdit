using DiaEditCore.Model;
using DiaEditCore.Model.Cars;
using DiaEditCore.Model.TimeTable;

using DiaEditCore.Serialization.Validation;
using DiaEditCore.Serialization.Validation.Timetable;

using Xunit;

namespace DiaEditCore.Tests.Validation.TimeTable;

public class StopTimeValidatorTests
{
    private static readonly StopTimeValidator Validator = new();
    private static readonly ValidationContext EmptyContext = new();

    [Fact]
    public void Worksが空ならエラーなし()
    {
        var stopTime = new StopTime();

        var issues = Validator.Validate(stopTime, EmptyContext);

        Assert.Empty(issues);
    }

    [Fact]
    public void Rule1_StartOpとPrevTrainの共存でエラー()
    {
        var stopTime = new StopTime
        {
            Works = new List<StationWork>
            {
                new() { Type = StationWorkType.PrevTrain },
                new() { Type = StationWorkType.StartOp, StartOpSeconds = 0, TrainOperationId = new TrainOperationId(1) },
            },
        };

        var issues = Validator.Validate(stopTime, EmptyContext);

        Assert.Contains(issues, i => i.Message.Contains("StartOpとPrevTrain"));
    }

    [Fact]
    public void Rule1_EndOpとNextTrainの共存でエラー()
    {
        var stopTime = new StopTime
        {
            Works = new List<StationWork>
            {
                new() { Type = StationWorkType.EndOp, EndOpSeconds = 100 },
                new() { Type = StationWorkType.NextTrain, NextTrainType = NextTrainType.SameTrain },
            },
        };

        var issues = Validator.Validate(stopTime, EmptyContext);

        Assert.Contains(issues, i => i.Message.Contains("EndOpとNextTrain"));
    }

    [Fact]
    public void Rule4_時刻が配列順に単調非減少なら違反なし()
    {
        var stopTime = new StopTime
        {
            Works = new List<StationWork>
            {
                new() { Type = StationWorkType.PrevTrain },
                new() { Type = StationWorkType.EndOp, EndOpSeconds = 100 },
                new()
                {
                    Type = StationWorkType.Coupling,
                    StartOpSeconds = 100,
                    EndOpSeconds = 200,
                    CutPoints = new List<TrainCutPoint>
                    {
                        new() { TrainId = new TrainId(1), Position = 0, CarCompositionId = new CarCompositionId(1) },
                    },
                },
            },
        };
        var context = new ValidationContext
        {
            CarConsists = new List<CarConsist>
            {
                new()
                {
                    Id = new CarConsistId(1), VehicleTypeId = new VehicleTypeId(1),
                    Type = CarConsistType.Basic, Cars = new List<CarRef>(),
                },
            },
            CarCompositions = new List<CarComposition>
            {
                new()
                {
                    Id = new CarCompositionId(1), Name = "x", Identifier = 1,
                    CarConsistId = new CarConsistId(1),
                },
            },
        };

        var issues = Validator.Validate(stopTime, context);

        Assert.DoesNotContain(issues, i => i.Message.Contains("Rule4"));
    }

    [Fact]
    public void Rule4_時刻が逆行していれば違反()
    {
        var stopTime = new StopTime
        {
            Works = new List<StationWork>
            {
                new() { Type = StationWorkType.EndOp, EndOpSeconds = 200 },
                new() { Type = StationWorkType.StartOp, StartOpSeconds = 100, TrainOperationId = new TrainOperationId(1) },
            },
        };

        var issues = Validator.Validate(stopTime, EmptyContext);

        Assert.Contains(issues, i => i.Message.Contains("Rule4"));
    }

    [Fact]
    public void Rule4_同一要素内でEndがStartより前なら違反()
    {
        var stopTime = new StopTime
        {
            Works = new List<StationWork>
            {
                new()
                {
                    Type = StationWorkType.Shunting,
                    StartOpSeconds = 200,
                    EndOpSeconds = 100,
                    StationPathId = new StationPathId(1),
                },
            },
        };

        var issues = Validator.Validate(stopTime, EmptyContext);

        Assert.Contains(issues, i => i.Message.Contains("StartOpSecondsより前"));
    }

    [Fact]
    public void 各StationWorkの単体検証エラーもWorksプレフィックス付きで伝播する()
    {
        var stopTime = new StopTime
        {
            Works = new List<StationWork> { new() { Type = StationWorkType.None } },
        };

        var issues = Validator.Validate(stopTime, EmptyContext);

        Assert.Contains(issues, i => i.Message.StartsWith("Works[0]:"));
    }
}
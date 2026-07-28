using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.TimeTable;

using DiaEditCore.Serialization.Validation;
using DiaEditCore.Serialization.Validation.Timetable;

using Xunit;

namespace DiaEditCore.Tests.Validation.TimeTable;

public class StationWorkValidatorTests
{
    private static readonly StationWorkValidator Validator = new();
    private static readonly ValidationContext EmptyContext = new();

    [Fact]
    public void Type_None_は常にエラー()
    {
        var work = new StationWork { Type = StationWorkType.None };

        var issues = Validator.Validate(work, EmptyContext);

        Assert.Single(issues);
    }

    [Fact]
    public void StartOp_必須フィールドが揃っていればエラーなし()
    {
        var work = new StationWork
        {
            Type = StationWorkType.StartOp,
            StartOpSeconds = 3600,
            TrainOperationId = new TrainOperationId(1),
        };

        var issues = Validator.Validate(work, EmptyContext);

        Assert.Empty(issues);
    }

    [Fact]
    public void StartOp_StartOpSecondsとTrainOperationId未設定で2件エラー()
    {
        var work = new StationWork { Type = StationWorkType.StartOp };

        var issues = Validator.Validate(work, EmptyContext);

        Assert.Equal(2, issues.Count);
    }

    [Fact]
    public void EndOp_EndOpSeconds未設定でエラー()
    {
        var work = new StationWork { Type = StationWorkType.EndOp };

        var issues = Validator.Validate(work, EmptyContext);

        Assert.Single(issues);
    }

    [Fact]
    public void NextTrain_NextTrainType未設定でエラー()
    {
        var work = new StationWork { Type = StationWorkType.NextTrain };

        var issues = Validator.Validate(work, EmptyContext);

        Assert.Single(issues);
    }

    [Fact]
    public void NextTrain_NextTrainType設定済みならエラーなし()
    {
        var work = new StationWork
        {
            Type = StationWorkType.NextTrain,
            NextTrainType = NextTrainType.SameTrain,
        };

        var issues = Validator.Validate(work, EmptyContext);

        Assert.Empty(issues);
    }

    [Fact]
    public void Shunting_StationPathId未設定でエラー()
    {
        var work = new StationWork
        {
            Type = StationWorkType.Shunting,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
        };

        var issues = Validator.Validate(work, EmptyContext);

        Assert.Contains(issues, i => i.Message.Contains("StationPathId"));
    }

    [Fact]
    public void Coupling_CutPointsが空でエラー()
    {
        var work = new StationWork
        {
            Type = StationWorkType.Coupling,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
        };

        var issues = Validator.Validate(work, EmptyContext);

        Assert.Contains(issues, i => i.Message.Contains("CutPoints"));
    }

    [Fact]
    public void CutPoints内のCarConsistIdが存在しなければエラー()
    {
        var work = new StationWork
        {
            Type = StationWorkType.Coupling,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
            CutPoints = new List<TrainCutPoint>
            {
                new() { TrainId = new TrainId(1), Position = 0, CarConsistId = new CarConsistId(999) },
            },
        };

        var issues = Validator.Validate(work, EmptyContext); // CarConsistsが空なので999は存在しない

        Assert.Contains(issues, i => i.Message.Contains("CarConsistId"));
    }

    [Fact]
    public void Decoupling_CutPointsが空でエラー()
    {
        var work = new StationWork
        {
            Type = StationWorkType.Decoupling,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
        };

        var issues = Validator.Validate(work, EmptyContext);

        Assert.Contains(issues, i => i.Message.Contains("CutPoints"));
    }

    [Fact]
    public void Shunting_StartOpSecondsとEndOpSecondsが両方未設定でエラー()
    {
        var stationPath = new StationPath
        {
            Id = new StationPathId(1),
            FloorUnitId = new FloorUnitId(1),
            Name = "入換経路A",
            Direction = StationPathDirection.Shunting,
            Waypoints = [],
        };
        var work = new StationWork
        {
            Type = StationWorkType.Shunting,
            StationPathId = stationPath.Id, // 実在するIDにして、秒未設定のエラーのみを単独発生させる
        };
        var context = new ValidationContext { StationPaths = [stationPath] };

        var issues = Validator.Validate(work, context);

        Assert.Single(issues);
        Assert.Contains(issues, i => i.Message.Contains("StartOpSeconds/EndOpSeconds"));
    }

    [Fact]
    public void Shunting_存在しないStationPathIdを参照するとエラー()
    {
        var work = new StationWork
        {
            Type = StationWorkType.Shunting,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
            StationPathId = new StationPathId(999),
        };

        var issues = Validator.Validate(work, EmptyContext); // StationPathsが空なので999は存在しない

        Assert.Contains(issues, i => i.Message.Contains("StationPathId") && i.Message.Contains("存在しない"));
    }

    [Fact]
    public void StartOpConsistのPositionが0始まり連番でなければエラー()
    {
        var work = new StationWork
        {
            Type = StationWorkType.StartOp,
            StartOpSeconds = 3600,
            TrainOperationId = new TrainOperationId(1),
            StartOpConsist =
            [
                new StartOpCarSlot { Position = 0, CarConsistId = new CarConsistId(1) },
                new StartOpCarSlot { Position = 2, CarConsistId = new CarConsistId(2) }, // 1が抜けている
            ],
        };

        var issues = Validator.Validate(work, EmptyContext);

        Assert.Contains(issues, i => i.Message.Contains("Position"));
    }

    [Fact]
    public void StartOpConsist内のCarConsistIdが存在しなければエラー()
    {
        var work = new StationWork
        {
            Type = StationWorkType.StartOp,
            StartOpSeconds = 3600,
            TrainOperationId = new TrainOperationId(1),
            StartOpConsist =
            [
                new StartOpCarSlot { Position = 0, CarConsistId = new CarConsistId(999) },
            ],
        };

        var issues = Validator.Validate(work, EmptyContext); // CarConsistsが空なので999は存在しない

        Assert.Contains(issues, i => i.Message.Contains("StartOpConsist") && i.Message.Contains("CarConsistId"));
    }

    [Fact]
    public void PrevTrain_追加フィールド不要でエラーなし()
    {
        var work = new StationWork { Type = StationWorkType.PrevTrain };

        var issues = Validator.Validate(work, EmptyContext);

        Assert.Empty(issues);
    }

    [Fact]
    public void PrevTrain_TrainOperationIdを設定しても運用番号変更としてエラーなし()
    {
        // PrevTrain.TrainOperationIdは運用番号変更（旧OpNumberChange相当）を表す任意フィールド。
        // 設定してもStationWorkValidator単体では（Rule 2等の横断検証を伴わないため）エラーにならない。
        var work = new StationWork
        {
            Type = StationWorkType.PrevTrain,
            TrainOperationId = new TrainOperationId(1),
        };

        var issues = Validator.Validate(work, EmptyContext);

        Assert.Empty(issues);
    }
}
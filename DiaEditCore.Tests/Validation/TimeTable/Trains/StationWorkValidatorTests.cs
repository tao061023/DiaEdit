using DiaEditCore.Model;
using DiaEditCore.Model.Cars;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.TimeTable.Trains;

using DiaEditCore.Serialization.Validation;
using DiaEditCore.Serialization.Validation.Timetable;

using Xunit;

namespace DiaEditCore.Tests.Validation.TimeTable.Trains;

public class StationWorkValidatorTests
{
    private static readonly StationWorkValidator Validator = new();
    private static readonly ValidationContext EmptyContext = new();

    private static ResolvedOperationRef Resolved(int id) => new(new TrainOperationId(id));
    private static ProvisionalOperationRef Provisional(string label) => new(label);

    [Fact]
    public void Type_None_は常にエラー()
    {
        var work = new StationWork { Type = StationWorkType.None };

        var issues = Validator.Validate(work, EmptyContext);

        Assert.Single(issues);
    }

    [Fact]
    public void StartOp_StartOpSecondsが設定されていればエラーなし()
    {
        // TrainOperationIdスカラーは廃止済み（各StartOpCarSlot.OperationIdがrequiredのため
        // 「未設定」自体が型システムで防止される）。StartOpConsistが空でもエラーにはならない。
        var work = new StationWork
        {
            Type = StationWorkType.StartOp,
            StartOpSeconds = 3600,
        };

        var issues = Validator.Validate(work, EmptyContext);

        Assert.Empty(issues);
    }

    [Fact]
    public void StartOp_StartOpSeconds未設定で1件エラー()
    {
        var work = new StationWork { Type = StationWorkType.StartOp };

        var issues = Validator.Validate(work, EmptyContext);

        Assert.Single(issues);
        Assert.Contains(issues, i => i.Message.Contains("StartOpSeconds"));
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
    public void Coupling_CutGroupsが空でエラー()
    {
        var work = new StationWork
        {
            Type = StationWorkType.Coupling,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
        };

        var issues = Validator.Validate(work, EmptyContext);

        Assert.Contains(issues, i => i.Message.Contains("CutGroups"));
    }

    [Fact]
    public void CutGroups内のCarCompositionIdが存在しなければエラー()
    {
        var work = new StationWork
        {
            Type = StationWorkType.Coupling,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
            CutGroups =
            [
                new CutGroup { GroupIndex = 0, CarCompositionId = new CarCompositionId(999), OperationId = Provisional("履歴") },
            ],
        };

        var issues = Validator.Validate(work, EmptyContext); // CarCompositionsが空なので999は存在しない

        Assert.Contains(issues, i => i.Message.Contains("CarCompositionId"));
    }

    [Fact]
    public void Decoupling_CutGroupsが空でエラー()
    {
        var work = new StationWork
        {
            Type = StationWorkType.Decoupling,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
        };

        var issues = Validator.Validate(work, EmptyContext);

        Assert.Contains(issues, i => i.Message.Contains("CutGroups"));
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
            StationPathId = stationPath.Id,
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

        var issues = Validator.Validate(work, EmptyContext);

        Assert.Contains(issues, i => i.Message.Contains("StationPathId") && i.Message.Contains("存在しない"));
    }

    [Fact]
    public void StartOpConsistのPositionが0始まり連番でなければエラー()
    {
        var work = new StationWork
        {
            Type = StationWorkType.StartOp,
            StartOpSeconds = 3600,
            StartOpConsist =
            [
                new StartOpCarSlot { Position = 0, CarCompositionId = new CarCompositionId(1), OperationId = Resolved(1) },
                new StartOpCarSlot { Position = 2, CarCompositionId = new CarCompositionId(2), OperationId = Resolved(2) }, // 1が抜けている
            ],
        };

        var issues = Validator.Validate(work, EmptyContext);

        Assert.Contains(issues, i => i.Message.Contains("Position"));
    }

    [Fact]
    public void StartOpConsist内のCarCompositionIdが存在しなければエラー()
    {
        var work = new StationWork
        {
            Type = StationWorkType.StartOp,
            StartOpSeconds = 3600,
            StartOpConsist =
            [
                new StartOpCarSlot { Position = 0, CarCompositionId = new CarCompositionId(999), OperationId = Resolved(1) },
            ],
        };

        var issues = Validator.Validate(work, EmptyContext); // CarCompositionsが空なので999は存在しない

        Assert.Contains(issues, i => i.Message.Contains("StartOpConsist") && i.Message.Contains("CarCompositionId"));
    }

    [Fact]
    public void PrevTrain_追加フィールド不要でエラーなし()
    {
        var work = new StationWork { Type = StationWorkType.PrevTrain };

        var issues = Validator.Validate(work, EmptyContext);

        Assert.Empty(issues);
    }

    // ==== StartOpCarSlot/CutGroup.OperationId（OperationRef）検証 ====

    [Fact]
    public void StartOpConsistのOperationIdがResolvedOperationRefで実在するTrainOperationと一致すればエラーなし()
    {
        var trainOperation = new TrainOperation { Id = new TrainOperationId(1), OperationNumber = "101" };
        var work = new StationWork
        {
            Type = StationWorkType.StartOp,
            StartOpSeconds = 3600,
            StartOpConsist =
            [
                new StartOpCarSlot { Position = 0, CarCompositionId = new CarCompositionId(1), OperationId = Resolved(1) },
            ],
        };
        var context = new ValidationContext
        {
            CarCompositions = [new CarComposition { Id = new CarCompositionId(1), Name = "トウ01", Identifier = 1, CarConsistId = new CarConsistId(1) }],
            TrainOperations = [trainOperation],
        };

        var issues = Validator.Validate(work, context);

        Assert.Empty(issues);
    }

    [Fact]
    public void StartOpConsistのOperationIdがResolvedOperationRefで実在するTrainOperationと一致しなければエラー()
    {
        var work = new StationWork
        {
            Type = StationWorkType.StartOp,
            StartOpSeconds = 3600,
            StartOpConsist =
            [
                new StartOpCarSlot { Position = 0, CarCompositionId = new CarCompositionId(1), OperationId = Resolved(999) },
            ],
        };
        var context = new ValidationContext
        {
            CarCompositions = [new CarComposition { Id = new CarCompositionId(1), Name = "トウ01", Identifier = 1, CarConsistId = new CarConsistId(1) }],
            TrainOperations = [], // 999は存在しない
        };

        var issues = Validator.Validate(work, context);

        Assert.Contains(issues, i => i.Message.Contains("StartOpConsist") && i.Message.Contains("OperationId"));
    }

    [Fact]
    public void StartOpConsistのOperationIdがProvisionalOperationRefなら実在チェックされない()
    {
        // 現状の実装：改訂案のRule 5表はCutGroupのみを規定しており、StartOpCarSlotで
        // ProvisionalOperationRefを許容してよいかは未確定のためチェックをスキップしている。
        var work = new StationWork
        {
            Type = StationWorkType.StartOp,
            StartOpSeconds = 3600,
            StartOpConsist =
            [
                new StartOpCarSlot { Position = 0, CarCompositionId = new CarCompositionId(1), OperationId = Provisional("未確定") },
            ],
        };
        var context = new ValidationContext
        {
            CarCompositions = [new CarComposition { Id = new CarCompositionId(1), Name = "トウ01", Identifier = 1, CarConsistId = new CarConsistId(1) }],
            TrainOperations = [],
        };

        var issues = Validator.Validate(work, context);

        Assert.Empty(issues);
    }

    [Fact]
    public void Decoupling_CutGroupsのOperationIdがResolvedOperationRefで実在しなければエラー()
    {
        var work = new StationWork
        {
            Type = StationWorkType.Decoupling,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
            CutGroups =
            [
                new CutGroup { GroupIndex = 0, CarCompositionId = new CarCompositionId(1), OperationId = Resolved(999) },
            ],
        };
        var context = new ValidationContext
        {
            CarCompositions = [new CarComposition { Id = new CarCompositionId(1), Name = "トウ01", Identifier = 1, CarConsistId = new CarConsistId(1) }],
            TrainOperations = [], // 999は存在しない
        };

        var issues = Validator.Validate(work, context);

        Assert.Contains(issues, i => i.Message.Contains("Decoupling") && i.Message.Contains("OperationId") && i.Message.Contains("一致しない"));
    }

    [Fact]
    public void Decoupling_CutGroups間でProvisionalOperationRefのLabelが重複していればエラー()
    {
        var work = new StationWork
        {
            Type = StationWorkType.Decoupling,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
            CutGroups =
            [
                new CutGroup { GroupIndex = 0, CarCompositionId = new CarCompositionId(1), OperationId = Provisional("101") },
                new CutGroup { GroupIndex = 1, CarCompositionId = new CarCompositionId(2), OperationId = Provisional("101") }, // 重複
            ],
        };
        var context = new ValidationContext
        {
            CarCompositions =
            [
                new CarComposition { Id = new CarCompositionId(1), Name = "トウ01", Identifier = 1, CarConsistId = new CarConsistId(1) },
                new CarComposition { Id = new CarCompositionId(2), Name = "トウ02", Identifier = 2, CarConsistId = new CarConsistId(1) },
            ],
        };

        var issues = Validator.Validate(work, context);

        Assert.Contains(issues, i => i.Message.Contains("重複"));
    }

    [Fact]
    public void Decoupling_CutGroups間でProvisionalOperationRefのLabelが異なればエラーなし()
    {
        var work = new StationWork
        {
            Type = StationWorkType.Decoupling,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
            CutGroups =
            [
                new CutGroup { GroupIndex = 0, CarCompositionId = new CarCompositionId(1), OperationId = Provisional("101") },
                new CutGroup { GroupIndex = 1, CarCompositionId = new CarCompositionId(2), OperationId = Provisional("102") },
            ],
        };
        var context = new ValidationContext
        {
            CarCompositions =
            [
                new CarComposition { Id = new CarCompositionId(1), Name = "トウ01", Identifier = 1, CarConsistId = new CarConsistId(1) },
                new CarComposition { Id = new CarCompositionId(2), Name = "トウ02", Identifier = 2, CarConsistId = new CarConsistId(1) },
            ],
        };

        var issues = Validator.Validate(work, context);

        Assert.DoesNotContain(issues, i => i.Message.Contains("重複"));
    }

    [Fact]
    public void Coupling_CutGroupsのOperationIdがProvisionalOperationRefなら実在しなくてもエラーにならない()
    {
        var work = new StationWork
        {
            Type = StationWorkType.Coupling,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
            CutGroups =
            [
                new CutGroup { GroupIndex = 0, CarCompositionId = new CarCompositionId(1), OperationId = Provisional("999") }, // 履歴の自由記述として許容
            ],
        };
        var context = new ValidationContext
        {
            CarCompositions = [new CarComposition { Id = new CarCompositionId(1), Name = "トウ01", Identifier = 1, CarConsistId = new CarConsistId(1) }],
            TrainOperations = [],
        };

        var issues = Validator.Validate(work, context);

        Assert.DoesNotContain(issues, i => i.Message.Contains("OperationId"));
    }

    [Fact]
    public void Coupling_CutGroupsのOperationIdがResolvedOperationRefならエラー()
    {
        // Rule 5④：CouplingはProvisionalOperationRefのみ許容
        var work = new StationWork
        {
            Type = StationWorkType.Coupling,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
            CutGroups =
            [
                new CutGroup { GroupIndex = 0, CarCompositionId = new CarCompositionId(1), OperationId = Resolved(1) },
            ],
        };
        var context = new ValidationContext
        {
            CarCompositions = [new CarComposition { Id = new CarCompositionId(1), Name = "トウ01", Identifier = 1, CarConsistId = new CarConsistId(1) }],
            TrainOperations = [new TrainOperation { Id = new TrainOperationId(1), OperationNumber = "101" }],
        };

        var issues = Validator.Validate(work, context);

        Assert.Contains(issues, i => i.Message.Contains("Coupling") && i.Message.Contains("ProvisionalOperationRefのみ許容"));
    }

    // ==== CutGroups.GroupIndex重複禁止（新設） ====

    [Fact]
    public void CutGroups間でGroupIndexが重複していればエラー()
    {
        var work = new StationWork
        {
            Type = StationWorkType.Decoupling,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
            CutGroups =
            [
                new CutGroup { GroupIndex = 0, CarCompositionId = new CarCompositionId(1), OperationId = Provisional("101") },
                new CutGroup { GroupIndex = 0, CarCompositionId = new CarCompositionId(2), OperationId = Provisional("102") }, // GroupIndex重複
            ],
        };
        var context = new ValidationContext
        {
            CarCompositions =
            [
                new CarComposition { Id = new CarCompositionId(1), Name = "トウ01", Identifier = 1, CarConsistId = new CarConsistId(1) },
                new CarComposition { Id = new CarCompositionId(2), Name = "トウ02", Identifier = 2, CarConsistId = new CarConsistId(1) },
            ],
        };

        var issues = Validator.Validate(work, context);

        Assert.Contains(issues, i => i.Message.Contains("GroupIndex") && i.Message.Contains("重複"));
    }

    [Fact]
    public void CutGroups間でGroupIndexが重複していなければエラーなし()
    {
        var work = new StationWork
        {
            Type = StationWorkType.Decoupling,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
            CutGroups =
            [
                new CutGroup { GroupIndex = 0, CarCompositionId = new CarCompositionId(1), OperationId = Provisional("101") },
                new CutGroup { GroupIndex = 1, CarCompositionId = new CarCompositionId(2), OperationId = Provisional("102") },
            ],
        };
        var context = new ValidationContext
        {
            CarCompositions =
            [
                new CarComposition { Id = new CarCompositionId(1), Name = "トウ01", Identifier = 1, CarConsistId = new CarConsistId(1) },
                new CarComposition { Id = new CarCompositionId(2), Name = "トウ02", Identifier = 2, CarConsistId = new CarConsistId(1) },
            ],
        };

        var issues = Validator.Validate(work, context);

        Assert.DoesNotContain(issues, i => i.Message.Contains("GroupIndex") && i.Message.Contains("重複"));
    }

    // ==== PrevTrainOperationOverrides（新設） ====

    [Fact]
    public void PrevTrainOperationOverridesのCarCompositionIdとNewOperationIdが実在すればエラーなし()
    {
        var work = new StationWork
        {
            Type = StationWorkType.PrevTrain,
            PrevTrainOperationOverrides =
            [
                new PrevTrainOperationOverride { CarCompositionId = new CarCompositionId(1), NewOperationId = new TrainOperationId(1) },
            ],
        };
        var context = new ValidationContext
        {
            CarCompositions = [new CarComposition { Id = new CarCompositionId(1), Name = "トウ01", Identifier = 1, CarConsistId = new CarConsistId(1) }],
            TrainOperations = [new TrainOperation { Id = new TrainOperationId(1), OperationNumber = "101" }],
        };

        var issues = Validator.Validate(work, context);

        Assert.Empty(issues);
    }

    [Fact]
    public void PrevTrainOperationOverridesのCarCompositionIdが存在しなければエラー()
    {
        var work = new StationWork
        {
            Type = StationWorkType.PrevTrain,
            PrevTrainOperationOverrides =
            [
                new PrevTrainOperationOverride { CarCompositionId = new CarCompositionId(999), NewOperationId = new TrainOperationId(1) },
            ],
        };
        var context = new ValidationContext
        {
            TrainOperations = [new TrainOperation { Id = new TrainOperationId(1), OperationNumber = "101" }],
        };

        var issues = Validator.Validate(work, context);

        Assert.Contains(issues, i => i.Message.Contains("PrevTrainOperationOverrides") && i.Message.Contains("CarCompositionId"));
    }

    [Fact]
    public void PrevTrainOperationOverridesのNewOperationIdが実在しなければエラー()
    {
        var work = new StationWork
        {
            Type = StationWorkType.PrevTrain,
            PrevTrainOperationOverrides =
            [
                new PrevTrainOperationOverride { CarCompositionId = new CarCompositionId(1), NewOperationId = new TrainOperationId(999) },
            ],
        };
        var context = new ValidationContext
        {
            CarCompositions = [new CarComposition { Id = new CarCompositionId(1), Name = "トウ01", Identifier = 1, CarConsistId = new CarConsistId(1) }],
            TrainOperations = [],
        };

        var issues = Validator.Validate(work, context);

        Assert.Contains(issues, i => i.Message.Contains("PrevTrainOperationOverrides") && i.Message.Contains("NewOperationId"));
    }

    // ==== 型別排他制約（新設） ====

    [Fact]
    public void PrevTrainOperationOverridesをPrevTrain以外で使うとエラー()
    {
        var work = new StationWork
        {
            Type = StationWorkType.EndOp,
            EndOpSeconds = 60,
            PrevTrainOperationOverrides =
            [
                new PrevTrainOperationOverride { CarCompositionId = new CarCompositionId(1), NewOperationId = new TrainOperationId(1) },
            ],
        };

        var issues = Validator.Validate(work, EmptyContext);

        Assert.Contains(issues, i => i.Message.Contains("PrevTrainOperationOverridesはPrevTrainでのみ使用可能"));
    }

    [Fact]
    public void SplitOriginをPrevTrain以外で使うとエラー()
    {
        var work = new StationWork
        {
            Type = StationWorkType.StartOp,
            StartOpSeconds = 3600,
            SplitOrigin = new SplitOriginRef { OriginTrainId = new TrainId(1), OriginStopKey = new StopKey(new StationId(1), 0) },
        };

        var issues = Validator.Validate(work, EmptyContext);

        Assert.Contains(issues, i => i.Message.Contains("SplitOriginはPrevTrainでのみ使用可能"));
    }

    [Fact]
    public void SplitOriginをPrevTrainで使うのはエラーにならない()
    {
        var work = new StationWork
        {
            Type = StationWorkType.PrevTrain,
            SplitOrigin = new SplitOriginRef { OriginTrainId = new TrainId(1), OriginStopKey = new StopKey(new StationId(1), 0) },
        };

        var issues = Validator.Validate(work, EmptyContext);

        Assert.DoesNotContain(issues, i => i.Message.Contains("SplitOrigin"));
    }
}
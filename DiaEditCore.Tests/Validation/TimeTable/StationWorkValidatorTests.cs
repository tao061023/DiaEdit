using DiaEditCore.Model;
using DiaEditCore.Model.Cars;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.TimeTable.Trains;

using DiaEditCore.Serialization.Validation;
using DiaEditCore.Serialization.Validation.Timetable;

using Xunit;

namespace DiaEditCore.Tests.Validation.TimeTable;

public class StationWorkValidatorTests
{
    private static readonly StationWorkValidator Validator = new();
    private static readonly ValidationContext EmptyContext = new();

    private static OperationRef Resolved(int id) => new ResolvedOperationRef(new TrainOperationId(id));
    private static OperationRef Provisional(string label) => new ProvisionalOperationRef(label);

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
        // v11.44改訂：StationWork.TrainOperationIdは廃止済み。StartOpSecondsのみが必須。
        var work = new StationWork
        {
            Type = StationWorkType.StartOp,
            StartOpSeconds = 3600,
        };

        var issues = Validator.Validate(work, EmptyContext);

        Assert.Empty(issues);
    }

    [Fact]
    public void StartOp_StartOpSeconds未設定でエラー()
    {
        // v11.44改訂：TrainOperationId未設定チェックは型システムで構造的に防止済みのため廃止。
        // StartOpSeconds未設定の1件のみがエラーとなる。
        var work = new StationWork { Type = StationWorkType.StartOp };

        var issues = Validator.Validate(work, EmptyContext);

        Assert.Single(issues);
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
            CutGroups = new List<CutGroup>
            {
                new() { GroupIndex = 0, CarCompositionId = new CarCompositionId(999), OperationId = Provisional("履歴") },
            },
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
                new StartOpCarSlot { Position = 0, CarCompositionId = new CarCompositionId(1), OperationId = Provisional("a") },
                new StartOpCarSlot { Position = 2, CarCompositionId = new CarCompositionId(2), OperationId = Provisional("b") }, // 1が抜けている
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
                new StartOpCarSlot { Position = 0, CarCompositionId = new CarCompositionId(999), OperationId = Provisional("a") },
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

    // ==== CutGroups.GroupIndexの重複禁止（v11.44新設） ====

    [Fact]
    public void Decoupling_CutGroups間でGroupIndexが重複していればエラー()
    {
        var work = new StationWork
        {
            Type = StationWorkType.Decoupling,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
            CutGroups =
            [
                new() { GroupIndex = 0, CarCompositionId = new CarCompositionId(1), OperationId = Provisional("a") },
                new() { GroupIndex = 0, CarCompositionId = new CarCompositionId(2), OperationId = Provisional("b") }, // 重複
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
    public void Decoupling_CutGroupsのGroupIndexが重複していなければエラーなし()
    {
        var work = new StationWork
        {
            Type = StationWorkType.Decoupling,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
            CutGroups =
            [
                new() { GroupIndex = 0, CarCompositionId = new CarCompositionId(1), OperationId = Provisional("a") },
                new() { GroupIndex = 1, CarCompositionId = new CarCompositionId(2), OperationId = Provisional("b") },
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

    // ==== StartOpCarSlot.OperationId（Rule 5改訂） ====

    [Fact]
    public void StartOpConsistのOperationIdがResolvedで実在するTrainOperationと一致すればエラーなし()
    {
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
            TrainOperations = [new TrainOperation { Id = new TrainOperationId(1), OperationNumber = "101" }],
        };

        var issues = Validator.Validate(work, context);

        Assert.Empty(issues);
    }

    [Fact]
    public void StartOpConsistのOperationIdがResolvedで実在するTrainOperationと一致しなければエラー()
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
    public void StartOpConsistのOperationIdがProvisionalなら実在チェック対象外()
    {
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
        };

        var issues = Validator.Validate(work, context);

        Assert.Empty(issues);
    }

    // ==== CutGroup.OperationId（Rule 5改訂） ====

    [Fact]
    public void Decoupling_CutGroupsのOperationIdがResolvedで実在しなければエラー()
    {
        var work = new StationWork
        {
            Type = StationWorkType.Decoupling,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
            CutGroups =
            [
                new() { GroupIndex = 0, CarCompositionId = new CarCompositionId(1), OperationId = Resolved(999) },
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
                new() { GroupIndex = 0, CarCompositionId = new CarCompositionId(1), OperationId = Provisional("A") },
                new() { GroupIndex = 1, CarCompositionId = new CarCompositionId(2), OperationId = Provisional("A") }, // 重複
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

        Assert.Contains(issues, i => i.Message.Contains("Label") && i.Message.Contains("重複"));
    }

    [Fact]
    public void Decoupling_CutGroupsのProvisionalLabelが異なればエラーなし()
    {
        var work = new StationWork
        {
            Type = StationWorkType.Decoupling,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
            CutGroups =
            [
                new() { GroupIndex = 0, CarCompositionId = new CarCompositionId(1), OperationId = Provisional("A") },
                new() { GroupIndex = 1, CarCompositionId = new CarCompositionId(2), OperationId = Provisional("B") },
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

        Assert.DoesNotContain(issues, i => i.Message.Contains("Label") && i.Message.Contains("重複"));
    }

    [Fact]
    public void Coupling_CutGroupsにResolvedOperationRefが来たらエラー()
    {
        // Rule 5④：CouplingはProvisionalOperationRefのみ許容
        var work = new StationWork
        {
            Type = StationWorkType.Coupling,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
            CutGroups =
            [
                new() { GroupIndex = 0, CarCompositionId = new CarCompositionId(1), OperationId = Resolved(1) },
            ],
        };
        var context = new ValidationContext
        {
            CarCompositions = [new CarComposition { Id = new CarCompositionId(1), Name = "トウ01", Identifier = 1, CarConsistId = new CarConsistId(1) }],
            TrainOperations = [new TrainOperation { Id = new TrainOperationId(1), OperationNumber = "101" }],
        };

        var issues = Validator.Validate(work, context);

        Assert.Contains(issues, i => i.Message.Contains("Coupling") && i.Message.Contains("ProvisionalOperationRef"));
    }

    [Fact]
    public void Coupling_CutGroupsのProvisionalOperationRefは実在しなくてもLabel重複してもエラーにならない()
    {
        var work = new StationWork
        {
            Type = StationWorkType.Coupling,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
            CutGroups =
            [
                new() { GroupIndex = 0, CarCompositionId = new CarCompositionId(1), OperationId = Provisional("999") },
                new() { GroupIndex = 1, CarCompositionId = new CarCompositionId(2), OperationId = Provisional("999") }, // 履歴の自由記述なので重複OK
            ],
        };
        var context = new ValidationContext
        {
            CarCompositions =
            [
                new CarComposition { Id = new CarCompositionId(1), Name = "トウ01", Identifier = 1, CarConsistId = new CarConsistId(1) },
                new CarComposition { Id = new CarCompositionId(2), Name = "トウ02", Identifier = 2, CarConsistId = new CarConsistId(1) },
            ],
            TrainOperations = [], // 999は存在しないが、Couplingなので対象外
        };

        var issues = Validator.Validate(work, context);

        Assert.Empty(issues);
    }

    // ==== PrevTrainOperationOverrides ====

    [Fact]
    public void PrevTrainOperationOverrides内のCarCompositionIdが存在しなければエラー()
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
    public void PrevTrainOperationOverrides内のNewOperationIdが実在しなければエラー()
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
            TrainOperations = [], // 999は存在しない
        };

        var issues = Validator.Validate(work, context);

        Assert.Contains(issues, i => i.Message.Contains("PrevTrainOperationOverrides") && i.Message.Contains("NewOperationId"));
    }

    [Fact]
    public void PrevTrainOperationOverridesが実在するCompositionとOperationを参照していればエラーなし()
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

    // ==== SplitOrigin（v11.44新設。PrevTrain以外での使用禁止） ====

    [Fact]
    public void SplitOriginはPrevTrain以外に付随するとエラー()
    {
        var work = new StationWork
        {
            Type = StationWorkType.StartOp,
            StartOpSeconds = 3600,
            SplitOrigin = new SplitOriginRef { OriginTrainId = new TrainId(1), OriginStopKey = new StopKey(new StationId(1), 0) },
        };

        var issues = Validator.Validate(work, EmptyContext);

        Assert.Contains(issues, i => i.Message.Contains("SplitOrigin"));
    }

    [Fact]
    public void SplitOriginはPrevTrainに付随すればエラーなし()
    {
        var work = new StationWork
        {
            Type = StationWorkType.PrevTrain,
            SplitOrigin = new SplitOriginRef { OriginTrainId = new TrainId(1), OriginStopKey = new StopKey(new StationId(1), 0) },
        };

        var issues = Validator.Validate(work, EmptyContext);

        Assert.Empty(issues);
    }
}
using DiaEditCore.Model;
using DiaEditCore.Model.Cars;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.TimeTable.Trains;

using DiaEditCore.Serialization.Validation;
using DiaEditCore.Serialization.Validation.TimeTable.Trains;
using Xunit;

namespace DiaEditCore.Tests.Serialization.Validation.TimeTable.Trains;

public class StationWorkValidatorTests
{
    private static readonly StationWorkValidator Validator = new();
    private static readonly ValidationContext EmptyContext = new();

    private static ResolvedOperationRef Resolved(int id) => new(new TrainOperationId(id));
    private static ProvisionalOperationRef Provisional(string label) => new(label);

    private static CutGroupEntry Entry(int carCompositionId, OperationRef? operationId = null)
        => new() { CarCompositionId = new CarCompositionId(carCompositionId), OperationId = operationId ?? Resolved(carCompositionId) };

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

    // ==== Coupling（vNEXT：CouplingDetail/PartnerTrainId方式） ====

    [Fact]
    public void Coupling_CouplingDetailが未設定でエラー()
    {
        var work = new StationWork
        {
            Type = StationWorkType.Coupling,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
        };

        var issues = Validator.Validate(work, EmptyContext);

        Assert.Contains(issues, i => i.Message.Contains("CouplingDetail"));
    }

    [Fact]
    public void Coupling_PartnerTrainIdが存在しなければエラー()
    {
        var work = new StationWork
        {
            Type = StationWorkType.Coupling,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
            CouplingDetail = new CouplingWork
            {
                PartnerTrainId = new TrainId(999),
                PartnerStopKey = new StopKey(new StationId(1), 0),
            },
        };

        var issues = Validator.Validate(work, EmptyContext); // Trainsが空なので999は存在しない

        Assert.Contains(issues, i => i.Message.Contains("PartnerTrainId") && i.Message.Contains("存在しない"));
    }

    [Fact]
    public void Coupling_PartnerStopKeyがPartnerTrainのStopTimesに存在しなければエラー()
    {
        var partner = new Train
        {
            Id = new TrainId(2),
            TimeTableSetId = new TimeTableSetId(1),
            TrainNumber = "9000M",
            ServiceRouteId = new ServiceRouteId(1),
            TrainTypeId = new TrainTypeId(1),
            TrainTypeName = new DisplayName { Name = "普通" },
            Nickname = new DisplayName { Name = "" },
            DefaultVehicleTypeId = new VehicleTypeId(1),
        };
        var work = new StationWork
        {
            Type = StationWorkType.Coupling,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
            CouplingDetail = new CouplingWork
            {
                PartnerTrainId = partner.Id,
                PartnerStopKey = new StopKey(new StationId(999), 0), // partnerのStopTimesに存在しない
            },
        };
        var context = new ValidationContext { Trains = [partner] };

        var issues = Validator.Validate(work, context);

        Assert.Contains(issues, i => i.Message.Contains("PartnerStopKey") && i.Message.Contains("存在しない"));
    }

    [Fact]
    public void Coupling_PartnerTrainIdPartnerStopKeyとも実在すればエラーなし()
    {
        var partner = new Train
        {
            Id = new TrainId(2),
            TimeTableSetId = new TimeTableSetId(1),
            TrainNumber = "9000M",
            ServiceRouteId = new ServiceRouteId(1),
            TrainTypeId = new TrainTypeId(1),
            TrainTypeName = new DisplayName { Name = "普通" },
            Nickname = new DisplayName { Name = "" },
            DefaultVehicleTypeId = new VehicleTypeId(1),
        };
        var partnerStopKey = new StopKey(new StationId(1), 0);
        partner.StopTimesInternal[partnerStopKey] = new StopTime();

        var work = new StationWork
        {
            Type = StationWorkType.Coupling,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
            CouplingDetail = new CouplingWork { PartnerTrainId = partner.Id, PartnerStopKey = partnerStopKey },
        };
        var context = new ValidationContext { Trains = [partner] };

        var issues = Validator.Validate(work, context);

        Assert.Empty(issues);
    }

    // ==== Decoupling（vNEXT：DecouplingDetail/FrontGroup・RearGroup方式） ====

    [Fact]
    public void Decoupling_DecouplingDetailが未設定でエラー()
    {
        var work = new StationWork
        {
            Type = StationWorkType.Decoupling,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
        };

        var issues = Validator.Validate(work, EmptyContext);

        Assert.Contains(issues, i => i.Message.Contains("DecouplingDetail"));
    }

    [Fact]
    public void Decoupling_FrontGroupが空でエラー()
    {
        var work = new StationWork
        {
            Type = StationWorkType.Decoupling,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
            DecouplingDetail = new DecouplingWork
            {
                FrontGroup = [],
                RearGroup = [Entry(1)],
            },
        };

        var issues = Validator.Validate(work, EmptyContext);

        Assert.Contains(issues, i => i.Message.Contains("FrontGroup"));
    }

    [Fact]
    public void Decoupling_RearGroupが空でエラー()
    {
        var work = new StationWork
        {
            Type = StationWorkType.Decoupling,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
            DecouplingDetail = new DecouplingWork
            {
                FrontGroup = [Entry(1)],
                RearGroup = [],
            },
        };

        var issues = Validator.Validate(work, EmptyContext);

        Assert.Contains(issues, i => i.Message.Contains("RearGroup"));
    }

    [Fact]
    public void Decoupling_FrontGroupとRearGroup間でCarCompositionIdが重複していればエラー()
    {
        // Rule 7（置換）：同一編成が両側に属することは物理的に不可能
        var work = new StationWork
        {
            Type = StationWorkType.Decoupling,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
            DecouplingDetail = new DecouplingWork
            {
                FrontGroup = [Entry(1)],
                RearGroup = [Entry(1)], // 重複
            },
        };
        var context = new ValidationContext
        {
            CarCompositions = [new CarComposition { Id = new CarCompositionId(1), Name = "トウ01", Identifier = 1, CarConsistId = new CarConsistId(1) }],
        };

        var issues = Validator.Validate(work, context);

        Assert.Contains(issues, i => i.Message.Contains("FrontGroup") && i.Message.Contains("RearGroup") && i.Message.Contains("重複"));
    }

    [Fact]
    public void Decoupling_FrontGroupとRearGroup間でCarCompositionIdが重複していなければエラーなし()
    {
        var work = new StationWork
        {
            Type = StationWorkType.Decoupling,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
            DecouplingDetail = new DecouplingWork
            {
                FrontGroup = [Entry(1)],
                RearGroup = [Entry(2)],
            },
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
    public void Decoupling内のCarCompositionIdが存在しなければエラー()
    {
        var work = new StationWork
        {
            Type = StationWorkType.Decoupling,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
            DecouplingDetail = new DecouplingWork
            {
                FrontGroup = [Entry(999)],
                RearGroup = [Entry(2)],
            },
        };
        var context = new ValidationContext
        {
            CarCompositions = [new CarComposition { Id = new CarCompositionId(2), Name = "トウ02", Identifier = 2, CarConsistId = new CarConsistId(1) }],
        };

        var issues = Validator.Validate(work, context); // 999は存在しない

        Assert.Contains(issues, i => i.Message.Contains("CarCompositionId"));
    }

    [Fact]
    public void Decoupling_OperationIdがResolvedOperationRefで実在しなければエラー()
    {
        var work = new StationWork
        {
            Type = StationWorkType.Decoupling,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
            DecouplingDetail = new DecouplingWork
            {
                FrontGroup = [Entry(1, Resolved(999))],
                RearGroup = [Entry(2)],
            },
        };
        var context = new ValidationContext
        {
            CarCompositions =
            [
                new CarComposition { Id = new CarCompositionId(1), Name = "トウ01", Identifier = 1, CarConsistId = new CarConsistId(1) },
                new CarComposition { Id = new CarCompositionId(2), Name = "トウ02", Identifier = 2, CarConsistId = new CarConsistId(1) },
            ],
            TrainOperations = [], // 999は存在しない
        };

        var issues = Validator.Validate(work, context);

        Assert.Contains(issues, i => i.Message.Contains("Decoupling") && i.Message.Contains("OperationId") && i.Message.Contains("一致しない"));
    }

    [Fact]
    public void Decoupling_FrontGroupとRearGroup間でProvisionalOperationRefのLabelが重複していればエラー()
    {
        var work = new StationWork
        {
            Type = StationWorkType.Decoupling,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
            DecouplingDetail = new DecouplingWork
            {
                FrontGroup = [Entry(1, Provisional("101"))],
                RearGroup = [Entry(2, Provisional("101"))], // 重複
            },
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

        Assert.Contains(issues, i => i.Message.Contains("重複") && i.Message.Contains("Provisional"));
    }

    [Fact]
    public void Decoupling_FrontGroupとRearGroup間でProvisionalOperationRefのLabelが異なればエラーなし()
    {
        var work = new StationWork
        {
            Type = StationWorkType.Decoupling,
            StartOpSeconds = 0,
            EndOpSeconds = 60,
            DecouplingDetail = new DecouplingWork
            {
                FrontGroup = [Entry(1, Provisional("101"))],
                RearGroup = [Entry(2, Provisional("102"))],
            },
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

    // ==== 型別排他制約（DecouplingDetail/CouplingDetail） ====

    [Fact]
    public void DecouplingDetailをDecoupling以外で使うとエラー()
    {
        var work = new StationWork
        {
            Type = StationWorkType.EndOp,
            EndOpSeconds = 60,
            DecouplingDetail = new DecouplingWork { FrontGroup = [Entry(1)], RearGroup = [Entry(2)] },
        };

        var issues = Validator.Validate(work, EmptyContext);

        Assert.Contains(issues, i => i.Message.Contains("DecouplingDetailはDecouplingでのみ使用可能"));
    }

    [Fact]
    public void CouplingDetailをCoupling以外で使うとエラー()
    {
        var work = new StationWork
        {
            Type = StationWorkType.EndOp,
            EndOpSeconds = 60,
            CouplingDetail = new CouplingWork { PartnerTrainId = new TrainId(1), PartnerStopKey = new StopKey(new StationId(1), 0) },
        };

        var issues = Validator.Validate(work, EmptyContext);

        Assert.Contains(issues, i => i.Message.Contains("CouplingDetailはCouplingでのみ使用可能"));
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

    // ==== StartOpCarSlot.OperationId（OperationRef）検証 ====

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

    // ==== PrevTrainOperationOverrides ====

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

    // ==== 型別排他制約（既存） ====

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
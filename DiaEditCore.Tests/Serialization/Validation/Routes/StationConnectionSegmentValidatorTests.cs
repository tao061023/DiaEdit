using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.Stations;

using DiaEditCore.Serialization.Validation;
using DiaEditCore.Serialization.Validation.Routes;
using Xunit;

namespace DiaEditCore.Tests.Serialization.Validation.Routes;

public class StationConnectionSegmentValidatorTests
{
    private static readonly StationId StA = new(1);
    private static readonly StationId StB = new(2);
    private static readonly StationId StC = new(3); // 「違う駅」ケース用
    private static readonly EntryPointId EpA = new(10);
    private static readonly EntryPointId EpB = new(20);
    private static readonly EntryPointId EpNotExist = new(999);
    private static readonly FloorUnitId FuA = new(100);
    private static readonly FloorUnitId FuB = new(200);
    private static readonly FloorUnitId FuNotExist = new(999);

    // v12.29：BaseRunTimeSec引数はStationConnectionSegmentからの項目削除に伴い廃止。
    private static StationConnectionSegment MakeTarget(
        StationId stationIdA, StationId stationIdB,
        EntryPointId? entryPointIdA = null, EntryPointId? entryPointIdB = null) => new()
        {
            Id = new StationConnectionSegmentId(1),
            StationIdA = stationIdA,
            StationIdB = stationIdB,
            EntryPointIdA = entryPointIdA ?? EpA,
            EntryPointIdB = entryPointIdB ?? EpB,
            MainRouteId = new MainRouteId(1),
        };

    private static EntryPoint MakeEntryPoint(EntryPointId id, FloorUnitId floorUnitId) => new()
    {
        Id = id,
        Base = new FloorUnitObjectBase { FloorUnitId = floorUnitId, Position = new Point(0, 0) },
        Type = EntryPointType.Both,
    };

    private static FloorUnit MakeFloorUnit(FloorUnitId id, StationId stationId, int displayOrder = 0) => new()
    {
        Id = id,
        StationId = stationId,
        DisplayOrder = displayOrder,
    };

    private static ValidationContext EmptyContext() => new();

    // StationIdA=StA・StationIdB=StBに対して、EpA→FuA(StA)・EpB→FuB(StB)が正しく揃った文脈
    private static ValidationContext ValidContext() => new()
    {
        EntryPoints = new[] { MakeEntryPoint(EpA, FuA), MakeEntryPoint(EpB, FuB) },
        FloorUnits = new[] { MakeFloorUnit(FuA, StA), MakeFloorUnit(FuB, StB) },
    };

    [Fact]
    public void 有効な値であれば合格()
    {
        var target = MakeTarget(StA, StB);

        var issues = new StationConnectionSegmentValidator().Validate(target, ValidContext());

        Assert.Empty(issues);
    }

    [Fact]
    public void StationIdAとStationIdBが同一だと不合格()
    {
        var target = MakeTarget(StA, StA);

        // StationIdA=StationIdB=StAなので、EntryPoint整合性の前提が崩れる（EpBはStB向けに作られている）。
        // ここではEntryPoint整合性エラーを混入させないよう、両EPともStA所属のFloorUnitに揃えた文脈を使う。
        var context = new ValidationContext
        {
            EntryPoints = new[] { MakeEntryPoint(EpA, FuA), MakeEntryPoint(EpB, FuA) },
            FloorUnits = new[] { MakeFloorUnit(FuA, StA) },
        };

        var issues = new StationConnectionSegmentValidator().Validate(target, context);

        Assert.Contains(issues, i => i.Message.Contains("StationIdA"));
        Assert.DoesNotContain(issues, i => i.Message.Contains("EntryPointId"));
    }

    // ---- ここからEntryPoint駅整合性のケース ----

    [Fact]
    public void EntryPointIdAが存在しないと不合格()
    {
        var target = MakeTarget(StA, StB, entryPointIdA: EpNotExist);
        var context = new ValidationContext
        {
            EntryPoints = new[] { MakeEntryPoint(EpB, FuB) },
            FloorUnits = new[] { MakeFloorUnit(FuA, StA), MakeFloorUnit(FuB, StB) },
        };

        var issues = new StationConnectionSegmentValidator().Validate(target, context);

        Assert.Contains(issues, i => i.Message.Contains("EntryPointIdA") && i.Message.Contains("存在しない"));
    }

    [Fact]
    public void EntryPointIdBが存在しないと不合格()
    {
        var target = MakeTarget(StA, StB, entryPointIdB: EpNotExist);
        var context = new ValidationContext
        {
            EntryPoints = new[] { MakeEntryPoint(EpA, FuA) },
            FloorUnits = new[] { MakeFloorUnit(FuA, StA), MakeFloorUnit(FuB, StB) },
        };

        var issues = new StationConnectionSegmentValidator().Validate(target, context);

        Assert.Contains(issues, i => i.Message.Contains("EntryPointIdB") && i.Message.Contains("存在しない"));
    }

    [Fact]
    public void EntryPointIdAのFloorUnitが存在しないと不合格()
    {
        var target = MakeTarget(StA, StB);
        var context = new ValidationContext
        {
            EntryPoints = new[] { MakeEntryPoint(EpA, FuNotExist), MakeEntryPoint(EpB, FuB) },
            FloorUnits = new[] { MakeFloorUnit(FuB, StB) },
        };

        var issues = new StationConnectionSegmentValidator().Validate(target, context);

        Assert.Contains(issues, i => i.Message.Contains("EntryPointIdA") && i.Message.Contains("FloorUnit"));
    }

    [Fact]
    public void EntryPointIdBのFloorUnitが存在しないと不合格()
    {
        var target = MakeTarget(StA, StB);
        var context = new ValidationContext
        {
            EntryPoints = new[] { MakeEntryPoint(EpA, FuA), MakeEntryPoint(EpB, FuNotExist) },
            FloorUnits = new[] { MakeFloorUnit(FuA, StA) },
        };

        var issues = new StationConnectionSegmentValidator().Validate(target, context);

        Assert.Contains(issues, i => i.Message.Contains("EntryPointIdB") && i.Message.Contains("FloorUnit"));
    }

    [Fact]
    public void EntryPointIdAが違う駅のFloorUnitを指すと不合格()
    {
        var target = MakeTarget(StA, StB);
        var context = new ValidationContext
        {
            // EpAはFuA(StC所属)を指す。target.StationIdAはStAなので不一致
            EntryPoints = new[] { MakeEntryPoint(EpA, FuA), MakeEntryPoint(EpB, FuB) },
            FloorUnits = new[] { MakeFloorUnit(FuA, StC), MakeFloorUnit(FuB, StB) },
        };

        var issues = new StationConnectionSegmentValidator().Validate(target, context);

        Assert.Contains(issues, i => i.Message.Contains("EntryPointIdA") && i.Message.Contains("StationIdA"));
    }

    [Fact]
    public void EntryPointIdBが違う駅のFloorUnitを指すと不合格()
    {
        var target = MakeTarget(StA, StB);
        var context = new ValidationContext
        {
            EntryPoints = new[] { MakeEntryPoint(EpA, FuA), MakeEntryPoint(EpB, FuB) },
            FloorUnits = new[] { MakeFloorUnit(FuA, StA), MakeFloorUnit(FuB, StC) },
        };

        var issues = new StationConnectionSegmentValidator().Validate(target, context);

        Assert.Contains(issues, i => i.Message.Contains("EntryPointIdB") && i.Message.Contains("StationIdB"));
    }

    [Fact]
    public void AとB両方でEntryPoint不整合があると両方報告される()
    {
        var target = MakeTarget(StA, StB);
        var context = new ValidationContext
        {
            EntryPoints = new[] { MakeEntryPoint(EpA, FuNotExist), MakeEntryPoint(EpB, FuNotExist) },
            FloorUnits = Array.Empty<FloorUnit>(),
        };

        var issues = new StationConnectionSegmentValidator().Validate(target, context);

        Assert.Equal(2, issues.Count);
        Assert.Contains(issues, i => i.Message.Contains("EntryPointIdA"));
        Assert.Contains(issues, i => i.Message.Contains("EntryPointIdB"));
    }
}
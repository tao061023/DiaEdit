using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.Stations;

using DiaEditCore.Serialization.Validation;
using DiaEditCore.Serialization.Validation.Routes;

using Xunit;

namespace DiaEditCore.Tests.Validation.Routes;

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

    private static StationConnectionSegment MakeTarget(
        StationId fromStationId, StationId toStationId, int baseRunTimeSec,
        EntryPointId? fromEntryPointId = null, EntryPointId? toEntryPointId = null) => new()
        {
            Id = new StationConnectionSegmentId(1),
            FromStationId = fromStationId,
            ToStationId = toStationId,
            FromEntryPointId = fromEntryPointId ?? EpA,
            ToEntryPointId = toEntryPointId ?? EpB,
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

    // FromStationId=StA・ToStationId=StBに対して、EpA→FuA(StA)・EpB→FuB(StB)が正しく揃った文脈
    private static ValidationContext ValidContext() => new()
    {
        EntryPoints = new[] { MakeEntryPoint(EpA, FuA), MakeEntryPoint(EpB, FuB) },
        FloorUnits = new[] { MakeFloorUnit(FuA, StA), MakeFloorUnit(FuB, StB) },
    };

    [Fact]
    public void 有効な値であれば合格()
    {
        var target = MakeTarget(StA, StB, 300);

        var issues = new StationConnectionSegmentValidator().Validate(target, ValidContext());

        Assert.Empty(issues);
    }

    [Fact]
    public void BaseRunTimeSecが0なら合格()
    {
        var target = MakeTarget(StA, StB, 0);

        var issues = new StationConnectionSegmentValidator().Validate(target, ValidContext());

        Assert.Empty(issues);
    }

    [Fact]
    public void FromStationIdとToStationIdが同一だと不合格()
    {
        var target = MakeTarget(StA, StA, 300);

        // FromStationId=ToStationId=StAなので、EntryPoint整合性の前提が崩れる（EpBはStB向けに作られている）。
        // ここではEntryPoint整合性エラーを混入させないよう、両EPともStA所属のFloorUnitに揃えた文脈を使う。
        var context = new ValidationContext
        {
            EntryPoints = new[] { MakeEntryPoint(EpA, FuA), MakeEntryPoint(EpB, FuA) },
            FloorUnits = new[] { MakeFloorUnit(FuA, StA) },
        };

        var issues = new StationConnectionSegmentValidator().Validate(target, context);

        Assert.Contains(issues, i => i.Message.Contains("FromStationId"));
        Assert.DoesNotContain(issues, i => i.Message.Contains("EntryPointId"));
    }

    // ---- ここから①EntryPoint駅整合性の新規ケース ----

    [Fact]
    public void FromEntryPointIdが存在しないと不合格()
    {
        var target = MakeTarget(StA, StB, 300, fromEntryPointId: EpNotExist);
        var context = new ValidationContext
        {
            EntryPoints = new[] { MakeEntryPoint(EpB, FuB) },
            FloorUnits = new[] { MakeFloorUnit(FuA, StA), MakeFloorUnit(FuB, StB) },
        };

        var issues = new StationConnectionSegmentValidator().Validate(target, context);

        Assert.Contains(issues, i => i.Message.Contains("FromEntryPointId") && i.Message.Contains("存在しない"));
    }

    [Fact]
    public void ToEntryPointIdが存在しないと不合格()
    {
        var target = MakeTarget(StA, StB, 300, toEntryPointId: EpNotExist);
        var context = new ValidationContext
        {
            EntryPoints = new[] { MakeEntryPoint(EpA, FuA) },
            FloorUnits = new[] { MakeFloorUnit(FuA, StA), MakeFloorUnit(FuB, StB) },
        };

        var issues = new StationConnectionSegmentValidator().Validate(target, context);

        Assert.Contains(issues, i => i.Message.Contains("ToEntryPointId") && i.Message.Contains("存在しない"));
    }

    [Fact]
    public void FromEntryPointIdのFloorUnitが存在しないと不合格()
    {
        var target = MakeTarget(StA, StB, 300);
        var context = new ValidationContext
        {
            EntryPoints = new[] { MakeEntryPoint(EpA, FuNotExist), MakeEntryPoint(EpB, FuB) },
            FloorUnits = new[] { MakeFloorUnit(FuB, StB) },
        };

        var issues = new StationConnectionSegmentValidator().Validate(target, context);

        Assert.Contains(issues, i => i.Message.Contains("FromEntryPointId") && i.Message.Contains("FloorUnit"));
    }

    [Fact]
    public void ToEntryPointIdのFloorUnitが存在しないと不合格()
    {
        var target = MakeTarget(StA, StB, 300);
        var context = new ValidationContext
        {
            EntryPoints = new[] { MakeEntryPoint(EpA, FuA), MakeEntryPoint(EpB, FuNotExist) },
            FloorUnits = new[] { MakeFloorUnit(FuA, StA) },
        };

        var issues = new StationConnectionSegmentValidator().Validate(target, context);

        Assert.Contains(issues, i => i.Message.Contains("ToEntryPointId") && i.Message.Contains("FloorUnit"));
    }

    [Fact]
    public void FromEntryPointIdが違う駅のFloorUnitを指すと不合格()
    {
        var target = MakeTarget(StA, StB, 300);
        var context = new ValidationContext
        {
            // EpAはFuA(StC所属)を指す。target.FromStationIdはStAなので不一致
            EntryPoints = new[] { MakeEntryPoint(EpA, FuA), MakeEntryPoint(EpB, FuB) },
            FloorUnits = new[] { MakeFloorUnit(FuA, StC), MakeFloorUnit(FuB, StB) },
        };

        var issues = new StationConnectionSegmentValidator().Validate(target, context);

        Assert.Contains(issues, i => i.Message.Contains("FromEntryPointId") && i.Message.Contains("FromStationId"));
    }

    [Fact]
    public void ToEntryPointIdが違う駅のFloorUnitを指すと不合格()
    {
        var target = MakeTarget(StA, StB, 300);
        var context = new ValidationContext
        {
            EntryPoints = new[] { MakeEntryPoint(EpA, FuA), MakeEntryPoint(EpB, FuB) },
            FloorUnits = new[] { MakeFloorUnit(FuA, StA), MakeFloorUnit(FuB, StC) },
        };

        var issues = new StationConnectionSegmentValidator().Validate(target, context);

        Assert.Contains(issues, i => i.Message.Contains("ToEntryPointId") && i.Message.Contains("ToStationId"));
    }

    [Fact]
    public void FromとTo両方でEntryPoint不整合があると両方報告される()
    {
        var target = MakeTarget(StA, StB, 300);
        var context = new ValidationContext
        {
            EntryPoints = new[] { MakeEntryPoint(EpA, FuNotExist), MakeEntryPoint(EpB, FuNotExist) },
            FloorUnits = Array.Empty<FloorUnit>(),
        };

        var issues = new StationConnectionSegmentValidator().Validate(target, context);

        Assert.Equal(2, issues.Count);
        Assert.Contains(issues, i => i.Message.Contains("FromEntryPointId"));
        Assert.Contains(issues, i => i.Message.Contains("ToEntryPointId"));
    }
}
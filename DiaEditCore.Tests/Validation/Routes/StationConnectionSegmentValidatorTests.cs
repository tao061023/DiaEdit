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
    private static readonly EntryPointId EpA = new(10);
    private static readonly EntryPointId EpB = new(20);

    private static StationConnectionSegment MakeTarget(
        StationId fromStationId, StationId toStationId, int baseRunTimeSec) => new()
    {
        Id = new StationConnectionSegmentId(1),
        FromStationId = fromStationId,
        ToStationId = toStationId,
        FromEntryPointId = EpA,
        ToEntryPointId = EpB,
        MainRouteId = new MainRouteId(1),
        BaseRunTimeSec = baseRunTimeSec,
    };

    private static ValidationContext EmptyContext() => new();

    [Fact]
    public void 有効な値であれば合格()
    {
        var target = MakeTarget(StA, StB, 300);

        var issues = new StationConnectionSegmentValidator().Validate(target, EmptyContext());

        Assert.Empty(issues);
    }

    [Fact]
    public void BaseRunTimeSecが0なら合格()
    {
        var target = MakeTarget(StA, StB, 0);

        var issues = new StationConnectionSegmentValidator().Validate(target, EmptyContext());

        Assert.Empty(issues);
    }

    [Fact]
    public void BaseRunTimeSecが負値だと不合格()
    {
        var target = MakeTarget(StA, StB, -1);

        var issues = new StationConnectionSegmentValidator().Validate(target, EmptyContext());

        Assert.Contains(issues, i => i.Message.Contains("BaseRunTimeSec"));
    }

    [Fact]
    public void FromStationIdとToStationIdが同一だと不合格()
    {
        var target = MakeTarget(StA, StA, 300);

        var issues = new StationConnectionSegmentValidator().Validate(target, EmptyContext());

        Assert.Contains(issues, i => i.Message.Contains("FromStationId"));
    }

    [Fact]
    public void BaseRunTimeSec負値かつ駅同一の場合は両方検出される()
    {
        var target = MakeTarget(StA, StA, -1);

        var issues = new StationConnectionSegmentValidator().Validate(target, EmptyContext());

        Assert.Equal(2, issues.Count);
        Assert.Contains(issues, i => i.Message.Contains("BaseRunTimeSec"));
        Assert.Contains(issues, i => i.Message.Contains("FromStationId"));
    }
}

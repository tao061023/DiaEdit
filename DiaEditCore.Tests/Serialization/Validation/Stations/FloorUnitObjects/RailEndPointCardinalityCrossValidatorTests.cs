namespace DiaEditCore.Tests.Serialization.Validation.Stations.FloorUnitObjects;

using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;
using DiaEditCore.Serialization.Validation;
using DiaEditCore.Serialization.Validation.Stations.FloorUnitObjects;

using Xunit;

public sealed class RailEndpointCardinalityCrossValidatorTests
{
    private static Rail MakeRail(int id, RailEndpointRef a, RailEndpointRef b) => new()
    {
        Id = new RailId(id),
        LengthM = 10,
        SpeedLimitKph = 25,
        Role = RailRole.Normal,
        EndpointA = a,
        EndpointB = b
    };

    private static ValidationContext Context(params Rail[] rails) => new()
    {
        Rails = rails
    };

    // ---------------------------
    // 1. EntryPoint：1本のみ接続 → issueなし
    // ---------------------------
    [Fact]
    public void Run_EntryPointWithSingleRail_NoIssue()
    {
        var context = Context(
            MakeRail(1, new EntryPointEndpointRef(new EntryPointId(10)), new NoneEndpointRef())
        );

        var issues = RailEndpointCardinalityCrossValidator.Run(context);

        Assert.Empty(issues);
    }

    // ---------------------------
    // 2. EntryPoint：2本のRailから参照 → issue（許容数1超過）
    // ---------------------------
    [Fact]
    public void Run_EntryPointWithTwoRails_ReportsIssue()
    {
        var entryPoint = new EntryPointEndpointRef(new EntryPointId(10));
        var context = Context(
            MakeRail(1, entryPoint, new NoneEndpointRef()),
            MakeRail(2, entryPoint, new NoneEndpointRef())
        );

        var issues = RailEndpointCardinalityCrossValidator.Run(context);

        Assert.Single(issues);
    }

    // ---------------------------
    // 3. BufferStop：2本のRailから参照 → issue（許容数1超過）
    // ---------------------------
    [Fact]
    public void Run_BufferStopWithTwoRails_ReportsIssue()
    {
        var bufferStop = new BufferStopEndpointRef(new BufferStopId(1));
        var context = Context(
            MakeRail(1, bufferStop, new NoneEndpointRef()),
            MakeRail(2, bufferStop, new NoneEndpointRef())
        );

        var issues = RailEndpointCardinalityCrossValidator.Run(context);

        Assert.Single(issues);
    }

    // ---------------------------
    // 4. BoundaryPoint：2本のRailから参照 → 許容数2のためissueなし
    // ---------------------------
    [Fact]
    public void Run_BoundaryPointWithTwoRails_NoIssue()
    {
        var boundaryPoint = new BoundaryPointEndpointRef(new BoundaryPointId(1));
        var context = Context(
            MakeRail(1, boundaryPoint, new NoneEndpointRef()),
            MakeRail(2, boundaryPoint, new NoneEndpointRef())
        );

        var issues = RailEndpointCardinalityCrossValidator.Run(context);

        Assert.Empty(issues);
    }

    // ---------------------------
    // 5. BoundaryPoint：3本のRailから参照 → issue（許容数2超過）
    // ---------------------------
    [Fact]
    public void Run_BoundaryPointWithThreeRails_ReportsIssue()
    {
        var boundaryPoint = new BoundaryPointEndpointRef(new BoundaryPointId(1));
        var context = Context(
            MakeRail(1, boundaryPoint, new NoneEndpointRef()),
            MakeRail(2, boundaryPoint, new NoneEndpointRef()),
            MakeRail(3, boundaryPoint, new NoneEndpointRef())
        );

        var issues = RailEndpointCardinalityCrossValidator.Run(context);

        Assert.Single(issues);
    }

    // ---------------------------
    // 6. Switcher：異なるポート同士は独立してカウントされる → issueなし
    // ---------------------------
    [Fact]
    public void Run_SwitcherDifferentPorts_NoIssue()
    {
        var switcherId = new SwitcherId(5);
        var context = Context(
            MakeRail(1, new SwitcherEndpointRef(switcherId, 0), new NoneEndpointRef()),
            MakeRail(2, new SwitcherEndpointRef(switcherId, 1), new NoneEndpointRef())
        );

        var issues = RailEndpointCardinalityCrossValidator.Run(context);

        Assert.Empty(issues);
    }

    // ---------------------------
    // 7. Switcher：同一ポートに2本のRail → issue（許容数1超過）
    // ---------------------------
    [Fact]
    public void Run_SwitcherSamePort_ReportsIssue()
    {
        var switcherEndpoint = new SwitcherEndpointRef(new SwitcherId(5), 0);
        var context = Context(
            MakeRail(1, switcherEndpoint, new NoneEndpointRef()),
            MakeRail(2, switcherEndpoint, new NoneEndpointRef())
        );

        var issues = RailEndpointCardinalityCrossValidator.Run(context);

        Assert.Single(issues);
    }

    // ---------------------------
    // 8. NoneEndpointRef（未接続）：どれだけ重複しても検証対象外 → issueなし
    // ---------------------------
    [Fact]
    public void Run_NoneEndpointRef_IsExcludedFromValidation()
    {
        var context = Context(
            MakeRail(1, new NoneEndpointRef(), new NoneEndpointRef()),
            MakeRail(2, new NoneEndpointRef(), new NoneEndpointRef()),
            MakeRail(3, new NoneEndpointRef(), new NoneEndpointRef())
        );

        var issues = RailEndpointCardinalityCrossValidator.Run(context);

        Assert.Empty(issues);
    }

    // ---------------------------
    // 9. RailのEndpointAとEndpointB両方が検証対象になることの確認
    //    （EndpointAだけでなくEndpointB側の重複も検知できるか）
    // ---------------------------
    [Fact]
    public void Run_DuplicateOnEndpointBSide_ReportsIssue()
    {
        var entryPoint = new EntryPointEndpointRef(new EntryPointId(10));
        var context = Context(
            MakeRail(1, new NoneEndpointRef(), entryPoint),
            MakeRail(2, new NoneEndpointRef(), entryPoint)
        );

        var issues = RailEndpointCardinalityCrossValidator.Run(context);

        Assert.Single(issues);
    }

    // ---------------------------
    // 10. Railが1件もない → issueなし（空コレクションの安全な処理）
    // ---------------------------
    [Fact]
    public void Run_NoRails_NoIssue()
    {
        var context = Context();

        var issues = RailEndpointCardinalityCrossValidator.Run(context);

        Assert.Empty(issues);
    }
}
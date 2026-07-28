using DiaEditCore.Model;
using DiaEditCore.Model.Stations;

using DiaEditCore.Serialization.Validation;
using DiaEditCore.Serialization.Validation.Stations;

using Xunit;

namespace DiaEditCore.Tests.Validation.Stations;

public class RailValidatorTests
{
    private static readonly BoundaryPointId Bp1 = new(1);
    private static readonly BoundaryPointId Bp2 = new(2);

    private static Rail MakeRail(RailRoll roll, string name) => new()
    {
        Id = new RailId(1),
        Name = name,
        LengthM = 100,
        SpeedLimitKph = 25,
        Roll = roll,
        EndpointA = new BoundaryPointEndpointRef(Bp1),
        EndpointB = new BoundaryPointEndpointRef(Bp2),
    };

    private static ValidationContext EmptyContext() => new();

    [Fact]
    public void RollがTrackで名前ありなら合格()
    {
        var target = MakeRail(RailRoll.Track, "1番線");

        var issues = new RailValidator().Validate(target, EmptyContext());

        Assert.Empty(issues);
    }

    [Fact]
    public void RollがTrackで名前が空文字列だと不合格()
    {
        var target = MakeRail(RailRoll.Track, "");

        var issues = new RailValidator().Validate(target, EmptyContext());

        Assert.Contains(issues, i => i.Message.Contains("Track"));
    }

    [Fact]
    public void RollがNormalで名前が空文字列でも合格()
    {
        var target = MakeRail(RailRoll.Normal, "");

        var issues = new RailValidator().Validate(target, EmptyContext());

        Assert.Empty(issues);
    }

    [Fact]
    public void RollがShuntingで名前が空文字列でも合格()
    {
        var target = MakeRail(RailRoll.Shunting, "");

        var issues = new RailValidator().Validate(target, EmptyContext());

        Assert.Empty(issues);
    }

    [Fact]
    public void RollがNormalで名前ありでも合格()
    {
        var target = MakeRail(RailRoll.Normal, "引上線");

        var issues = new RailValidator().Validate(target, EmptyContext());

        Assert.Empty(issues);
    }

    [Fact]
    public void RollがTrackで名前が空白のみの場合は現状の実装では合格扱いになる()
    {
        // IsNullOrEmpty判定のため、空白文字だけの名前は「空ではない」とみなされ通過する。
        // 実運用上の名前として不適切な可能性はあるが、現行実装の挙動として明示的に記録する。
        var target = MakeRail(RailRoll.Track, "   ");

        var issues = new RailValidator().Validate(target, EmptyContext());

        Assert.Empty(issues);
    }
}

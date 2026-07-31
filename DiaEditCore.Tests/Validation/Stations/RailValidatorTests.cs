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

    private static Rail MakeRail(RailRole Role, string name) => new()
    {
        Id = new RailId(1),
        Name = name,
        LengthM = 100,
        SpeedLimitKph = 25,
        Role = Role,
        EndpointA = new BoundaryPointEndpointRef(Bp1),
        EndpointB = new BoundaryPointEndpointRef(Bp2),
    };

    private static ValidationContext EmptyContext() => new();

    [Fact]
    public void RoleがTrackで名前ありなら合格()
    {
        var target = MakeRail(RailRole.Track, "1番線");

        var issues = new RailValidator().Validate(target, EmptyContext());

        Assert.Empty(issues);
    }

    [Fact]
    public void RoleがTrackで名前が空文字列だと不合格()
    {
        var target = MakeRail(RailRole.Track, "");

        var issues = new RailValidator().Validate(target, EmptyContext());

        Assert.Contains(issues, i => i.Message.Contains("Track"));
    }

    [Fact]
    public void RoleがNormalで名前が空文字列でも合格()
    {
        var target = MakeRail(RailRole.Normal, "");

        var issues = new RailValidator().Validate(target, EmptyContext());

        Assert.Empty(issues);
    }

    [Fact]
    public void RoleがShuntingで名前が空文字列でも合格()
    {
        var target = MakeRail(RailRole.Shunting, "");

        var issues = new RailValidator().Validate(target, EmptyContext());

        Assert.Empty(issues);
    }

    [Fact]
    public void RoleがNormalで名前ありでも合格()
    {
        var target = MakeRail(RailRole.Normal, "引上線");

        var issues = new RailValidator().Validate(target, EmptyContext());

        Assert.Empty(issues);
    }

    [Fact]
    public void RoleがTrackで名前が空白のみの場合は現状の実装では合格扱いになる()
    {
        // IsNullOrEmpty判定のため、空白文字だけの名前は「空ではない」とみなされ通過する。
        // 実運用上の名前として不適切な可能性はあるが、現行実装の挙動として明示的に記録する。
        var target = MakeRail(RailRole.Track, "   ");

        var issues = new RailValidator().Validate(target, EmptyContext());

        Assert.Empty(issues);
    }
}

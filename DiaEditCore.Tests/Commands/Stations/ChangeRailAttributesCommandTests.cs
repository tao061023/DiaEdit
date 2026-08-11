namespace DiaEditCore.Tests.Commands.Stations;

using DiaEditCore.Commands.Stations;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using Xunit;

public sealed class ChangeRailAttributesCommandTests
{
    private static Rail MakeRail() => new()
    {
        Id = new RailId(1),
        Name = "旧線路名",
        LengthM = 100.0,
        SpeedLimitKph = 60.0,
        Role = RailRole.Normal,
        EndpointA = new NoneEndpointRef(),
        EndpointB = new NoneEndpointRef()
    };

    private static TimeTableSetCache EmptyCache() => new();

    [Fact]
    public void Execute_AppliesAllFields()
    {
        var rail = MakeRail();
        var newValues = new RailSnapshot("新線路名", 250.0, 90.0, RailRole.Track);

        var command = new ChangeRailAttributesCommand(rail, newValues, EmptyCache());
        command.Execute();

        Assert.Equal("新線路名", rail.Name);
        Assert.Equal(250.0, rail.LengthM);
        Assert.Equal(90.0, rail.SpeedLimitKph);
        Assert.Equal(RailRole.Track, rail.Role);
    }

    [Fact]
    public void Undo_RestoresOriginalValues()
    {
        var rail = MakeRail();
        var newValues = new RailSnapshot("新線路名", 250.0, 90.0, RailRole.Shunting);

        var command = new ChangeRailAttributesCommand(rail, newValues, EmptyCache());
        command.Execute();
        command.Undo();

        Assert.Equal("旧線路名", rail.Name);
        Assert.Equal(100.0, rail.LengthM);
        Assert.Equal(60.0, rail.SpeedLimitKph);
        Assert.Equal(RailRole.Normal, rail.Role);
    }

    [Fact]
    public void Execute_DoesNotAffectEndpointsOrControlPoints()
    {
        // EndpointA/EndpointB/ControlPointsはこのコマンドのスコープ外であり、
        // Execute()前後で一切変化しないことを確認する。
        var rail = MakeRail();
        var originalEndpointA = rail.EndpointA;
        var originalEndpointB = rail.EndpointB;
        var newValues = new RailSnapshot("新線路名", 250.0, 90.0, RailRole.Track);

        var command = new ChangeRailAttributesCommand(rail, newValues, EmptyCache());
        command.Execute();

        Assert.Same(originalEndpointA, rail.EndpointA);
        Assert.Same(originalEndpointB, rail.EndpointB);
        Assert.Empty(rail.ControlPoints);
    }

    [Fact]
    public void AffectedIds_ContainsOnlySelf_WhenCacheIsEmpty()
    {
        var rail = MakeRail();
        var newValues = new RailSnapshot("新線路名", 250.0, 90.0, RailRole.Track);

        var command = new ChangeRailAttributesCommand(rail, newValues, EmptyCache());

        Assert.Single(command.AffectedIds);
    }
}
namespace DiaEditCore.Tests.Commands.Stations;

using System.Collections.Generic;
using DiaEditCore.Commands.Stations;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using Xunit;

public sealed class CreateRailCommandTests
{
    [Fact]
    public void Execute_AddsRailToList_WithAllocatedId()
    {
        var rails = new List<Rail>();

        var command = new CreateRailCommand(rails, "新線路", 150.0, 80.0, RailRole.Normal);
        command.Execute();

        Assert.Single(rails);
        Assert.Equal(1, rails[0].Id.Value);
        Assert.Equal("新線路", rails[0].Name);
        Assert.Equal(150.0, rails[0].LengthM);
        Assert.Equal(80.0, rails[0].SpeedLimitKph);
        Assert.Equal(RailRole.Normal, rails[0].Role);
    }

    [Fact]
    public void Execute_CreatesWithNoneEndpointsAndEmptyControlPoints()
    {
        var rails = new List<Rail>();

        var command = new CreateRailCommand(rails, "新線路", 150.0, 80.0, RailRole.Normal);
        command.Execute();

        Assert.IsType<NoneEndpointRef>(rails[0].EndpointA);
        Assert.IsType<NoneEndpointRef>(rails[0].EndpointB);
        Assert.Empty(rails[0].ControlPoints);
    }

    [Fact]
    public void Execute_AllocatesMaxPlusOne_NotFillingGaps()
    {
        var rails = new List<Rail>
        {
            new() { Id = new RailId(1), LengthM = 10, SpeedLimitKph = 60, Role = RailRole.Normal, EndpointA = new NoneEndpointRef(), EndpointB = new NoneEndpointRef() },
            new() { Id = new RailId(5), LengthM = 10, SpeedLimitKph = 60, Role = RailRole.Normal, EndpointA = new NoneEndpointRef(), EndpointB = new NoneEndpointRef() }
        };

        var command = new CreateRailCommand(rails, "新線路", 150.0, 80.0, RailRole.Normal);
        command.Execute();

        Assert.Equal(6, command.Created!.Id.Value);
    }

    [Fact]
    public void Undo_RemovesCreatedRailFromList()
    {
        var rails = new List<Rail>();

        var command = new CreateRailCommand(rails, "新線路", 150.0, 80.0, RailRole.Normal);
        command.Execute();
        command.Undo();

        Assert.Empty(rails);
    }

    [Fact]
    public void AffectedIds_IsEmpty()
    {
        var rails = new List<Rail>();

        var command = new CreateRailCommand(rails, "新線路", 150.0, 80.0, RailRole.Normal);

        Assert.Empty(command.AffectedIds);
    }
}

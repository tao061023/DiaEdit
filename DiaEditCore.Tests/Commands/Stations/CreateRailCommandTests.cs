namespace DiaEditCore.Tests.Commands.Stations;

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

public sealed class DeleteRailCommandTests
{
    private static Rail MakeRail(int id) => new()
    {
        Id = new RailId(id),
        Name = $"線路{id}",
        LengthM = 100.0,
        SpeedLimitKph = 60.0,
        Role = RailRole.Normal,
        EndpointA = new NoneEndpointRef(),
        EndpointB = new NoneEndpointRef()
    };

    private static TimeTableSetCache EmptyCache() => new();

    [Fact]
    public void Execute_RemovesRailFromList()
    {
        var rail = MakeRail(1);
        var rails = new List<Rail> { rail };

        var command = new DeleteRailCommand(rails, rail, EmptyCache());
        command.Execute();

        Assert.Empty(rails);
    }

    [Fact]
    public void Undo_RestoresDeletedRail()
    {
        var rail = MakeRail(1);
        var rails = new List<Rail> { rail };

        var command = new DeleteRailCommand(rails, rail, EmptyCache());
        command.Execute();
        command.Undo();

        Assert.Single(rails);
        Assert.Same(rail, rails[0]);
    }

    [Fact]
    public void Constructor_DoesNotThrow_SinceRailHasNoIncomingReferencesInCurrentModel()
    {
        // 現行モデルではRailIdを逆参照するオブジェクトが存在しないため（v12.13確認済み）、
        // 空のキャッシュに対して常にコンストラクタが成功することを確認する。
        var rail = MakeRail(1);
        var rails = new List<Rail> { rail };

        var command = new DeleteRailCommand(rails, rail, EmptyCache());
        Assert.NotNull(command);
    }

    [Fact]
    public void Execute_DoesNotAffectOtherRails()
    {
        var rail1 = MakeRail(1);
        var rail2 = MakeRail(2);
        var rails = new List<Rail> { rail1, rail2 };

        var command = new DeleteRailCommand(rails, rail1, EmptyCache());
        command.Execute();

        Assert.Single(rails);
        Assert.Same(rail2, rails[0]);
    }
}
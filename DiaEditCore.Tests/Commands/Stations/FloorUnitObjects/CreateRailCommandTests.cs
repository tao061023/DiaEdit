namespace DiaEditCore.Tests.Commands.Stations.FloorUnitObjects;

using System.Collections.Generic;
using System.Linq;

using DiaEditCore.Commands.Stations.FloorUnitObjects;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;
using DiaEditCore.Session;

using Xunit;

public sealed class CreateRailCommandTests
{
    [Fact]
    public void Execute_AddsRailToList_WithAllocatedId()
    {
        var rails = new List<Rail>();
        var idAllocator = new IdAllocator<RailId>(v => new RailId(v), rails.Select(r => r.Id.Value));

        var command = new CreateRailCommand(rails, idAllocator, "新線路", 150.0, 80.0, RailRole.Normal);
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
        var idAllocator = new IdAllocator<RailId>(v => new RailId(v), rails.Select(r => r.Id.Value));

        var command = new CreateRailCommand(rails, idAllocator, "新線路", 150.0, 80.0, RailRole.Normal);
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

        var idAllocator = new IdAllocator<RailId>(v => new RailId(v), rails.Select(r => r.Id.Value));

        var command = new CreateRailCommand(rails, idAllocator, "新線路", 150.0, 80.0, RailRole.Normal);
        command.Execute();

        Assert.Equal(6, command.Created!.Id.Value);
    }

    [Fact]
    public void Undo_RemovesCreatedRailFromList()
    {
        var rails = new List<Rail>();
        var idAllocator = new IdAllocator<RailId>(v => new RailId(v), rails.Select(r => r.Id.Value));

        var command = new CreateRailCommand(rails, idAllocator, "新線路", 150.0, 80.0, RailRole.Normal);
        command.Execute();
        command.Undo();

        Assert.Empty(rails);
    }

    [Fact]
    public void AffectedIds_IsEmpty()
    {
        var rails = new List<Rail>();
        var idAllocator = new IdAllocator<RailId>(v => new RailId(v), rails.Select(r => r.Id.Value));

        var command = new CreateRailCommand(rails, idAllocator, "新線路", 150.0, 80.0, RailRole.Normal);

        Assert.Empty(command.AffectedIds);
    }
    
    [Fact]
    public void Undo後に別コマンドで再作成しても同一Idが再利用されない()
    {
        // §9.2項目27の中核回帰テスト：Undo後の「別インスタンスによる」再作成でId重複が起きないこと。
        // 同一コマンドインスタンス内のUndo→Redo（Created再利用）とは別のシナリオである点に注意。
        var rails = new List<Rail>();
        var idAllocator = new IdAllocator<RailId>(v => new RailId(v), rails.Select(r => r.Id.Value));

        var first = new CreateRailCommand(rails, idAllocator, "線路A", 100.0, 60.0, RailRole.Normal);
        first.Execute();
        var firstId = first.Created!.Id;
        first.Undo();

        var second = new CreateRailCommand(rails, idAllocator, "線路B", 100.0, 60.0, RailRole.Normal);
        second.Execute();

        Assert.NotEqual(firstId, second.Created!.Id);
    }
}

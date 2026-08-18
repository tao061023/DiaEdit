namespace DiaEditCore.Tests.Commands.Stations;

using DiaEditCore.Commands;
using DiaEditCore.Commands.Stations;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Session;
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

    private static readonly ValidationRules DefaultValidationRules = new(
        MinDwellTimeSec: null,
        MinHeadwaySec: null,
        MinTurnaroundSec: null,
        TrackEntryMarginSec: null,
        TrackPassMarginSec: null,
        EnableConflictDetection: true,
        EnableCarLengthCheck: true);

    private static ProjectFile MakeEmptyProject() => new()
    {
        SchemaVersion = 1,
        ProjectSettings = new ProjectSettings(DefaultValidationRules),
    };

    /// <summary>
    /// v12.21：コンストラクタ引数がTimeTableSetCache cache → ProjectSession sessionへ移行したため、
    /// 旧EmptyCache()（new TimeTableSetCache()を直接構築）をProjectSessionベースへ置き換えた。
    /// Load()直後はキャッシュが空の状態でクリーン（_cacheDirty=false）になるため、
    /// 「空キャッシュを渡す」という以前のテスト意図はそのまま踏襲できる。
    /// </summary>
    private static ProjectSession MakeSession()
    {
        var session = new ProjectSession(new CommandInvoker());
        session.Load(MakeEmptyProject());
        return session;
    }

    [Fact]
    public void Execute_AppliesAllFields()
    {
        var rail = MakeRail();
        var newValues = new RailSnapshot("新線路名", 250.0, 90.0, RailRole.Track);

        var command = new ChangeRailAttributesCommand(rail, newValues, MakeSession());
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

        var command = new ChangeRailAttributesCommand(rail, newValues, MakeSession());
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

        var command = new ChangeRailAttributesCommand(rail, newValues, MakeSession());
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

        var command = new ChangeRailAttributesCommand(rail, newValues, MakeSession());

        Assert.Single(command.AffectedIds);
    }
}
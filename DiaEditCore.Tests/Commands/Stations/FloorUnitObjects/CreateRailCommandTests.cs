namespace DiaEditCore.Tests.Commands.Stations.FloorUnitObjects;

using System.Collections.Generic;
using System.Linq;

using DiaEditCore.Commands.Stations.FloorUnitObjects;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;
using DiaEditCore.Session;

using Xunit;

/// <summary>
/// v13.9変更：CreateRailCommandはEndpointA/Bファクトリを必須で受け取るようになったため
/// （NoneEndpoint実体化・RailCreationWorkflow生成順序入れ替えに伴う再設計）、
/// 本テストではRailCreationWorkflowを経由せず「確定済みの参照を返すダミーファクトリ」を
/// 直接渡す形で、Rail自身の生成ロジック（Id採番・Redo非重複・AffectedIds空集合）のみを検証する。
/// 端点オブジェクト自体の生成・アタッチの正しさはRailCreationWorkflow側の結合テストの責務とする。
/// </summary>
public sealed class CreateRailCommandTests
{
    /// <summary>
    /// テスト用の確定済みダミー端点。NoneEndpointIdの値自体に意味はなく、
    /// 「ファクトリが評価されて何らかのRailEndpointRefが設定されること」の確認にのみ使う。
    /// </summary>
    private static RailEndpointRef DummyEndpoint(int id) => new NoneEndpointRef(new NoneEndpointId(id));

    [Fact]
    public void Execute_AddsRailToList_WithAllocatedId()
    {
        var rails = new List<Rail>();
        var idAllocator = new IdAllocator<RailId>(v => new RailId(v), rails.Select(r => r.Id.Value));

        var command = new CreateRailCommand(
            rails, idAllocator, "新線路", 150.0, 80.0, RailRole.Normal,
            () => DummyEndpoint(1), () => DummyEndpoint(2));
        command.Execute();

        Assert.Single(rails);
        Assert.Equal(1, rails[0].Id.Value);
        Assert.Equal("新線路", rails[0].Name);
        Assert.Equal(150.0, rails[0].LengthM);
        Assert.Equal(80.0, rails[0].SpeedLimitKph);
        Assert.Equal(RailRole.Normal, rails[0].Role);
    }

    [Fact]
    public void Execute_SetsEndpointsFromFactories_AndEmptyControlPoints()
    {
        // 旧テスト名Execute_CreatesWithNoneEndpointsAndEmptyControlPointsから改名：
        // v13.9以降はNoneEndpointRefが既定値ではなく、ファクトリが返す値がそのまま設定される
        // ことを検証する（Noneに限定されない）。
        var rails = new List<Rail>();
        var idAllocator = new IdAllocator<RailId>(v => new RailId(v), rails.Select(r => r.Id.Value));
        var endpointA = DummyEndpoint(1);
        var endpointB = DummyEndpoint(2);

        var command = new CreateRailCommand(
            rails, idAllocator, "新線路", 150.0, 80.0, RailRole.Normal,
            () => endpointA, () => endpointB);
        command.Execute();

        Assert.Same(endpointA, rails[0].EndpointA);
        Assert.Same(endpointB, rails[0].EndpointB);
        Assert.Empty(rails[0].ControlPoints);
    }

    [Fact]
    public void Execute_AllocatesMaxPlusOne_NotFillingGaps()
    {
        var rails = new List<Rail>
        {
            new() { Id = new RailId(1), LengthM = 10, SpeedLimitKph = 60, Role = RailRole.Normal, EndpointA = DummyEndpoint(1), EndpointB = DummyEndpoint(2) },
            new() { Id = new RailId(5), LengthM = 10, SpeedLimitKph = 60, Role = RailRole.Normal, EndpointA = DummyEndpoint(1), EndpointB = DummyEndpoint(2) }
        };

        var idAllocator = new IdAllocator<RailId>(v => new RailId(v), rails.Select(r => r.Id.Value));

        var command = new CreateRailCommand(
            rails, idAllocator, "新線路", 150.0, 80.0, RailRole.Normal,
            () => DummyEndpoint(3), () => DummyEndpoint(4));
        command.Execute();

        Assert.Equal(6, command.Created!.Id.Value);
    }

    [Fact]
    public void Undo_RemovesCreatedRailFromList()
    {
        var rails = new List<Rail>();
        var idAllocator = new IdAllocator<RailId>(v => new RailId(v), rails.Select(r => r.Id.Value));

        var command = new CreateRailCommand(
            rails, idAllocator, "新線路", 150.0, 80.0, RailRole.Normal,
            () => DummyEndpoint(1), () => DummyEndpoint(2));
        command.Execute();
        command.Undo();

        Assert.Empty(rails);
    }

    [Fact]
    public void AffectedIds_IsEmpty()
    {
        var rails = new List<Rail>();
        var idAllocator = new IdAllocator<RailId>(v => new RailId(v), rails.Select(r => r.Id.Value));

        var command = new CreateRailCommand(
            rails, idAllocator, "新線路", 150.0, 80.0, RailRole.Normal,
            () => DummyEndpoint(1), () => DummyEndpoint(2));

        Assert.Empty(command.AffectedIds);
    }

    [Fact]
    public void Undo後に別コマンドで再作成しても同一Idが再利用されない()
    {
        // §9.2項目27の中核回帰テスト：Undo後の「別インスタンスによる」再作成でId重複が起きないこと。
        // 同一コマンドインスタンス内のUndo→Redo（Created再利用）とは別のシナリオである点に注意。
        var rails = new List<Rail>();
        var idAllocator = new IdAllocator<RailId>(v => new RailId(v), rails.Select(r => r.Id.Value));

        var first = new CreateRailCommand(
            rails, idAllocator, "線路A", 100.0, 60.0, RailRole.Normal,
            () => DummyEndpoint(1), () => DummyEndpoint(2));
        first.Execute();
        var firstId = first.Created!.Id;
        first.Undo();

        var second = new CreateRailCommand(
            rails, idAllocator, "線路B", 100.0, 60.0, RailRole.Normal,
            () => DummyEndpoint(3), () => DummyEndpoint(4));
        second.Execute();

        Assert.NotEqual(firstId, second.Created!.Id);
    }

    [Fact]
    public void Redo経路ではEndpointファクトリが再評価されずCreatedインスタンスがそのまま再挿入される()
    {
        // v13.9新設：ファクトリの副作用（あれば）が二重発火しないことの確認。
        // Apply()の実装がCreated is not nullの分岐でファクトリ呼び出し自体をスキップする設計を
        // 直接検証する回帰テスト（AttachRailEndpointsCommand廃止に伴う統合で新設）。
        var rails = new List<Rail>();
        var idAllocator = new IdAllocator<RailId>(v => new RailId(v), rails.Select(r => r.Id.Value));
        var factoryACallCount = 0;
        var factoryBCallCount = 0;

        var command = new CreateRailCommand(
            rails, idAllocator, "新線路", 150.0, 80.0, RailRole.Normal,
            () => { factoryACallCount++; return DummyEndpoint(1); },
            () => { factoryBCallCount++; return DummyEndpoint(2); });

        command.Execute(); // 初回：ファクトリ評価あり
        command.Undo();
        command.Execute(); // Redo経路：ファクトリ再評価なし

        Assert.Equal(1, factoryACallCount);
        Assert.Equal(1, factoryBCallCount);
        Assert.Single(rails);
    }
}
namespace DiaEditCore.Tests.Commands.Stations.FloorUnitObjects;

using System.Collections.Generic;
using System.Linq;

using DiaEditCore.Commands.Stations.FloorUnitObjects;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;
using DiaEditCore.Session;

using Xunit;

/// <summary>
/// CreateFloorUnitObjectCommand&lt;TId, T&gt;汎用実装をPlatformへ適用した際の挙動検証（§9.2項目34）。
/// BoundaryPoint／BufferStop／EntryPointは汎用コマンド新設時（v13.9）に検証済みのため、
/// Platform固有の追加フィールド（FacingRailIds／EffectiveLength）がファクトリクロージャ経由で
/// 正しく設定されることのみを対象とする（汎用コマンド本体のUndo/Redo基本動作は
/// 既存のBoundaryPoint等向けテストが担保済みのため重複させない）。
/// </summary>
public sealed class CreateFloorUnitObjectCommandPlatformTests
{
    private static FloorUnitObjectBase MakeBase() =>
        new() { FloorUnitId = new FloorUnitId(1), Position = new Point(0, 0) };

    [Fact]
    public void Execute_AddsPlatformToList_WithFactorySuppliedFields()
    {
        var platforms = new List<Platform>();
        var idAllocator = new IdAllocator<PlatformId>(v => new PlatformId(v), platforms.Select(p => p.Id.Value));
        var facingRailIds = new List<RailId> { new(1), new(2) };

        var command = new CreateFloorUnitObjectCommand<PlatformId, Platform>(
            platforms,
            idAllocator,
            id => new Platform
            {
                Id = id,
                Base = MakeBase(),
                Name = "1番線",
                FacingRailIds = facingRailIds,
                EffectiveLength = 200.0
            },
            p => new PlatformObjectId(p.Id));

        command.Execute();

        Assert.Single(platforms);
        Assert.Equal(1, platforms[0].Id.Value);
        Assert.Equal("1番線", platforms[0].Name);
        Assert.Equal(facingRailIds, platforms[0].FacingRailIds);
        Assert.Equal(200.0, platforms[0].EffectiveLength);
    }

    [Fact]
    public void Execute_AllowsNullEffectiveLength()
    {
        // EffectiveLength未設定時はFacingRailIdsのLengthMへフォールバックする仕様
        // （Platform.cs：「未設定の場合、FacingRailIdsのLengthMにフォールバック」）。
        // このコマンド自体はnullをそのまま素通しすることのみを確認する
        // （フォールバック解決自体はUI／Resolver側の責務でありコマンドの責務外）。
        var platforms = new List<Platform>();
        var idAllocator = new IdAllocator<PlatformId>(v => new PlatformId(v), platforms.Select(p => p.Id.Value));

        var command = new CreateFloorUnitObjectCommand<PlatformId, Platform>(
            platforms,
            idAllocator,
            id => new Platform
            {
                Id = id,
                Base = MakeBase(),
                Name = "2番線",
                FacingRailIds = new List<RailId> { new(3) },
                EffectiveLength = null
            },
            p => new PlatformObjectId(p.Id));

        command.Execute();

        Assert.Null(platforms[0].EffectiveLength);
    }

    [Fact]
    public void Undo_RemovesCreatedPlatformFromList()
    {
        var platforms = new List<Platform>();
        var idAllocator = new IdAllocator<PlatformId>(v => new PlatformId(v), platforms.Select(p => p.Id.Value));

        var command = new CreateFloorUnitObjectCommand<PlatformId, Platform>(
            platforms,
            idAllocator,
            id => new Platform { Id = id, Base = MakeBase(), FacingRailIds = new List<RailId> { new(1) } },
            p => new PlatformObjectId(p.Id));

        command.Execute();
        command.Undo();

        Assert.Empty(platforms);
    }

    [Fact]
    public void AffectedIds_IsEmpty()
    {
        var platforms = new List<Platform>();
        var idAllocator = new IdAllocator<PlatformId>(v => new PlatformId(v), platforms.Select(p => p.Id.Value));

        var command = new CreateFloorUnitObjectCommand<PlatformId, Platform>(
            platforms,
            idAllocator,
            id => new Platform { Id = id, Base = MakeBase(), FacingRailIds = new List<RailId> { new(1) } },
            p => new PlatformObjectId(p.Id));

        Assert.Empty(command.AffectedIds);
    }

    [Fact]
    public void Redo経路ではファクトリが再評価されずCreatedインスタンスがそのまま再挿入される()
    {
        var platforms = new List<Platform>();
        var idAllocator = new IdAllocator<PlatformId>(v => new PlatformId(v), platforms.Select(p => p.Id.Value));
        var factoryCallCount = 0;

        var command = new CreateFloorUnitObjectCommand<PlatformId, Platform>(
            platforms,
            idAllocator,
            id =>
            {
                factoryCallCount++;
                return new Platform { Id = id, Base = MakeBase(), FacingRailIds = new List<RailId> { new(1) } };
            },
            p => new PlatformObjectId(p.Id));

        command.Execute();
        command.Undo();
        command.Execute();

        Assert.Equal(1, factoryCallCount);
        Assert.Single(platforms);
    }
}
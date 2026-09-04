namespace DiaEditCore.Session;

using DiaEditCore.ChangeNotification;
using DiaEditCore.Algorithm.CacheBuilder;
using DiaEditCore.Commands;
using DiaEditCore.Model;

/// <summary>
/// 読込中のProjectFileとその派生キャッシュ(TimeTableSetCache)のライフサイクルを一元管理する。
/// CommandInvokerからのICacheChangeObserver通知を受けてキャッシュをdirty化し、
/// 次にキャッシュへアクセスする直前に遅延再構築する（discard-and-regenerateの原則、§8.2項目1）。
///
/// 構造的防止の方針：生のTimeTableSetCacheを各Commandのコンストラクタへ
/// 直接渡す現行シグネチャは、呼び出し側がRebuildを忘れても静的に検知できないため廃止する。
/// 各Commandは本クラスを受け取り、GetCache()経由でのみキャッシュへアクセスする形に統一する。
///
/// Composition層での登録単位：Singleton（CommandInvoker・ChangeNotificationBridgeと同じライフタイム）。
/// </summary>
public sealed class ProjectSession : ICacheChangeObserver
{
    private readonly CommandInvoker _invoker;
    private TimeTableSetCache _cache = new();
    private bool _cacheDirty = true;

    public ProjectFile Current { get; private set; } = null!;

    // §9.2項目27：モデル種別ごとの単調カウンタ。Load()で初期化される。
    // 新規モデルをCreateパターンに乗せるたびにここへ1行追加する規約とする（6.1節）。
    public IdAllocator<StationId> StationIds { get; private set; } = null!;
    public IdAllocator<RailId> RailIds { get; private set; } = null!;
    public IdAllocator<FloorUnitId> FloorUnitIds { get; private set; } = null!;

    // Rail横展開（6.2節第一弾）：Rail作成＝両端点オブジェクト作成と一体（RailCreationWorkflow）のため、
    // 端点3種のIdAllocatorも同じ規約でここに追加する。Switcherは既存端点接続への収束検出フロー
    // （9.4.4節・別導線）でのみ生成されるため、本ワークフローの対象外（IdAllocatorも現時点では追加しない）。
    public IdAllocator<BoundaryPointId> BoundaryPointIds { get; private set; } = null!;
    public IdAllocator<EntryPointId> EntryPointIds { get; private set; } = null!;
    public IdAllocator<BufferStopId> BufferStopIds { get; private set; } = null!;

    public ProjectSession(CommandInvoker invoker)
    {
        _invoker = invoker;
        _invoker.Subscribe(this);
    }

    public void Load(ProjectFile project)
    {
        Current = project;
        _cache = new TimeTableSetCache();
        _cacheDirty = true;

        StationIds = new IdAllocator<StationId>(v => new StationId(v), project.Stations.Select(s => s.Id.Value));
        RailIds = new IdAllocator<RailId>(v => new RailId(v), project.Rails.Select(r => r.Id.Value));
        FloorUnitIds = new IdAllocator<FloorUnitId>(v => new FloorUnitId(v), project.FloorUnits.Select(f => f.Id.Value));

        BoundaryPointIds = new IdAllocator<BoundaryPointId>(v => new BoundaryPointId(v), project.BoundaryPoints.Select(b => b.Id.Value));
        EntryPointIds = new IdAllocator<EntryPointId>(v => new EntryPointId(v), project.EntryPoints.Select(e => e.Id.Value));
        BufferStopIds = new IdAllocator<BufferStopId>(v => new BufferStopId(v), project.BufferStops.Select(b => b.Id.Value));

        RebuildCacheIfDirty();
    }

    void ICacheChangeObserver.OnChanged(IReadOnlySet<ObjectId> affectedIds)
    {
        // 差分更新は行わずフルダーティ化に統一する（設計判断の理由は8.2項目1の議論を参照）。
        // affectedIdsの中身は現時点では使わないが、将来インデックス単位の部分再構築を
        // 導入する場合の入口として、シグネチャ上は保持しておく。
        _cacheDirty = true;
    }

    /// <summary>
    /// 派生キャッシュを取得する。dirtyなら全Builderを再実行してから返す。
    /// Commandのコンストラクタは、生のTimeTableSetCacheではなく本メソッド経由でのみ
    /// キャッシュへアクセスすること（構造的防止の主眼）。
    /// </summary>
    public TimeTableSetCache GetCache()
    {
        if (Current is null)
            throw new InvalidOperationException("ProjectSession.Load()が呼ばれる前にGetCache()が呼ばれました。");
        RebuildCacheIfDirty();
        return _cache;
    }

    private void RebuildCacheIfDirty()
    {
        if (!_cacheDirty) return;

        _cache.RebuildAll(
            Current.Trains,
            Current.StationConnections,
            Current.StationConnectionSegments,
            Current.TemporaryRestrictions,
            Current.MainRoutes,
            Current.ServiceRoutes);

        var floorUnitDependentIndex = FloorUnitDependentIndexBuilder.Build(
            Current.BoundaryPoints,
            Current.EntryPoints,
            Current.BufferStops,
            Current.Switchers,
            Current.Platforms,
            Current.StationPaths);
        _cache.FloorUnitDependentIndex.Clear();
        foreach (var kv in floorUnitDependentIndex)
            _cache.FloorUnitDependentIndex[kv.Key] = kv.Value;

        var departureIndex = DepartureByStationTrackIndexBuilder.Build(Current.Trains);
        _cache.DepartureByStationTrackIndex.Clear();
        foreach (var kv in departureIndex)
            _cache.DepartureByStationTrackIndex[kv.Key] = kv.Value;

        var stopKeyRefIndex = StopKeyReferenceIndexBuilder.Build(Current.Trains);
        _cache.StopKeyReferenceIndex.Clear();
        foreach (var kv in stopKeyRefIndex)
            _cache.StopKeyReferenceIndex[kv.Key] = kv.Value;

        // ServiceRoutesByMainRouteIndex（v12.6新設、UI表示専用：MainRoute編集時の影響範囲表示）。
        // RebuildAll側でClear()はされるが構築はRebuildAllの外側（本メソッド）の責務のため、
        // 他の3インデックスと同様にここで明示的に呼び直す必要がある
        // （v12.20監査で発覚：この呼び出しが漏れており、GetCache()を1回でも呼ぶと
        //   本インデックスが二度と再構築されない状態になっていた）。
        var serviceRoutesByMainRouteIndex = ServiceRoutesByMainRouteIndexBuilder.Build(Current.ServiceRoutes);
        _cache.ServiceRoutesByMainRouteIndex.Clear();
        foreach (var kv in serviceRoutesByMainRouteIndex)
            _cache.ServiceRoutesByMainRouteIndex[kv.Key] = kv.Value;

        // Rail向け専用インデックス（PlatformFacingRailIndex／TemporaryRestrictionByRailIndex／
        // RailUsedByStopTimeIndex）はv12.20で実装見送りを確定済み（設計書§5.13.4・§5.14.3参照）。
        // 消費者がDeleteRailCommand唯一・Rail削除頻度の低さを理由に、専用Builderは作らず
        // DeleteRailCommandのコンストラクタ内で対象コレクションを直接線形走査する方式で代替した。
        // 本メソッドでの対応は不要（旧TODOコメントは古い設計判断を指していたため削除）。

        _cacheDirty = false;
    }
}
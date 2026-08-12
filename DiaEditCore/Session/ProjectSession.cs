namespace DiaEditCore.Session;

using DiaEditCore.ChangeNotification;
using DiaEditCore.Algorithm;
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

    public ProjectSession(CommandInvoker invoker)
    {
        _invoker = invoker;
        _invoker.Subscribe(this);
    }

    /// <summary>
    /// プロジェクトを読み込み、キャッシュを即座にフル構築する
    /// （Load直後は画面表示に必要なため、遅延を待たず同期的に構築する）。
    /// </summary>
    public void Load(ProjectFile project)
    {
        Current = project;
        _cache = new TimeTableSetCache();
        _cacheDirty = true;
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
            Current.TemporaryRestrictions);

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

        // TODO: RailUsedByStopTimeIndex等、Rail向け新設インデックスのBuilder呼び出しをここに追加

        _cacheDirty = false;
    }
}
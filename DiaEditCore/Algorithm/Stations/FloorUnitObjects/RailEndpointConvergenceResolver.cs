namespace DiaEditCore.Algorithm.Stations.FloorUnitObjects;

using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;

/// <summary>
/// 収束点に集まっているRail端点1件を表す値。
/// </summary>
/// <remarks>
/// <see cref="RailEnd"/>はRailMerger.RailEnd（A/B）を再利用する（新規enumを増やさない。）
/// </remarks>
public readonly record struct RailEndpointLocation
{
    /// <summary>収束点に端点を持つRailのId。</summary>
    public RailId RailId { get; }

    /// <summary>そのRailのうち、収束点に位置する側の端（A/B）。</summary>
    public RailEnd End { get; }

    /// <summary>収束点における実際のRail端点参照。</summary>
    public RailEndpointRef Ref { get; }

    public RailEndpointLocation(RailId RailId, RailEnd End, RailEndpointRef Ref)
    {
        this.RailId = RailId;
        this.End = End;
        this.Ref = Ref;
    }
}

/// <summary>
/// Rail端点収束変換ルールに基づく分類結果の種別。
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description><c>Vanish</c>：N=0。削除されたRail自身のみがこの端点を参照していた（孤立・行き止まり）。端点オブジェクト自体を削除してよい（再分類の余地がない）。</description></item>
/// <item><description><c>Keep</c>：N=1、既存フロー据置（ユーザーが種別を明示選択）。他に参照Railが残っているため端点オブジェクトは維持する。</description></item>
/// <item><description><c>BoundaryPoint</c>：N=2。</description></item>
/// <item><description><c>SwitcherNew</c>：N=3,4かつ収束集合中に既存Switcher参照が無い（新規Switcher作成）。</description></item>
/// <item><description><c>SwitcherExpand</c>：N=3,4かつ収束集合中に既存Switcher参照が含まれる（既存Switcher拡張）。</description></item>
/// <item><description><c>Error</c>：N&gt;=5。</description></item>
/// </list>
/// </remarks>
public enum ConvergenceKind { Vanish, Keep, BoundaryPoint, SwitcherNew, SwitcherExpand, Error }

/// <summary>
/// <see cref="RailEndpointConvergenceResolver.Classify"/>の戻り値。分類結果と、Errorケースのみ設定されるメッセージを保持する。
/// </summary>
public readonly record struct ConvergenceClassification(ConvergenceKind Kind, string? ErrorMessage = null)
{
    /// <summary>
    /// Error以外の分類結果を生成する。
    /// </summary>
    /// <param name="kind">分類結果の種別。</param>
    /// <returns><paramref name="kind"/>を保持し、<see cref="ConvergenceClassification.ErrorMessage"/>がnullの値。</returns>
    public static ConvergenceClassification Ok(ConvergenceKind kind) => new(kind);

    /// <summary>
    /// Errorケースの分類結果を生成する。
    /// </summary>
    /// <param name="message">エラー内容を説明するメッセージ。</param>
    /// <returns><see cref="ConvergenceKind.Error"/>と<paramref name="message"/>を保持する値。</returns>
    public static ConvergenceClassification Fail(string message) => new(ConvergenceKind.Error, message);
}

/// <summary>
/// 「ある座標にどのRail端点が収束しているか」を検出し、収束数から変換種別を分類する純粋関数群。
/// </summary>
/// <remarks>
/// 対象がドラッグで新規に動いてきた端点か既存の確定済み端点かは区別しない（座標一致のみで判定）。
/// Switcherは複数PortIndexが同一オブジェクト（＝同一Base.Position）に属するため、
/// 「異なるPortIndexのSwitcherEndpointRef」も同一座標として自然に収束集合へ含まれる。
/// </remarks>
public static class RailEndpointConvergenceResolver
{
    /// <summary>
    /// RailEndpointRefが指す実体オブジェクトのBase.Positionを解決する。
    /// </summary>
    /// <param name="endpointRef">解決対象のRail端点参照。</param>
    /// <param name="noneEndpoints">現在のNoneEndpointコレクション。</param>
    /// <param name="boundaryPoints">現在のBoundaryPointコレクション。</param>
    /// <param name="entryPoints">現在のEntryPointコレクション。</param>
    /// <param name="bufferStops">現在のBufferStopコレクション。</param>
    /// <param name="switchers">現在のSwitcherコレクション。</param>
    /// <returns>参照先オブジェクトの<see cref="Point"/>座標。</returns>
    /// <exception cref="InvalidOperationException">
    /// 該当オブジェクトがコレクション内に見つからない場合。収束検出はセッション内の生きたコレクションに
    /// 対してのみ呼ばれる想定のため、見つからない場合は呼び出し側のデータ不整合とみなす。
    /// </exception>
    /// <exception cref="NotSupportedException"><paramref name="endpointRef"/>が未知の派生型の場合。</exception>
    public static Point ResolvePosition(
        RailEndpointRef endpointRef,
        IReadOnlyList<NoneEndpoint> noneEndpoints,
        IReadOnlyList<BoundaryPoint> boundaryPoints,
        IReadOnlyList<EntryPoint> entryPoints,
        IReadOnlyList<BufferStop> bufferStops,
        IReadOnlyList<Switcher> switchers)
    {
        switch (endpointRef)
        {
            case NoneEndpointRef n:
                return Find(noneEndpoints, x => x.Id.Equals(n.Id), x => x.Base.Position, "NoneEndpoint", n.Id);
            case BoundaryPointEndpointRef b:
                return Find(boundaryPoints, x => x.Id.Equals(b.Id), x => x.Base.Position, "BoundaryPoint", b.Id);
            case EntryPointEndpointRef e:
                return Find(entryPoints, x => x.Id.Equals(e.Id), x => x.Base.Position, "EntryPoint", e.Id);
            case BufferStopEndpointRef bs:
                return Find(bufferStops, x => x.Id.Equals(bs.Id), x => x.Base.Position, "BufferStop", bs.Id);
            case SwitcherEndpointRef sw:
                return Find(switchers, x => x.Id.Equals(sw.Id), x => x.Base.Position, "Switcher", sw.Id);
            default:
                throw new NotSupportedException($"未知のRailEndpointRef型: {endpointRef.GetType().Name}");
        }
    }

    /// <summary>
    /// リスト内から条件に一致する要素を線形探索し、座標を取り出す内部ヘルパー。
    /// </summary>
    /// <typeparam name="T">探索対象の要素型。</typeparam>
    /// <typeparam name="TId">エラーメッセージに埋め込むId型。</typeparam>
    /// <param name="list">探索対象のコレクション。</param>
    /// <param name="pred">一致判定条件。</param>
    /// <param name="pos">一致要素から座標を取り出す関数。</param>
    /// <param name="typeName">見つからなかった場合のエラーメッセージに使う型名。</param>
    /// <param name="id">見つからなかった場合のエラーメッセージに使うId。</param>
    /// <returns>一致した要素の座標。</returns>
    /// <exception cref="InvalidOperationException">一致する要素が見つからない場合。</exception>
    private static Point Find<T, TId>(IReadOnlyList<T> list, Func<T, bool> pred, Func<T, Point> pos, string typeName, TId id)
    {
        foreach (var item in list)
        {
            if (pred(item)) return pos(item);
        }
        throw new InvalidOperationException($"収束検出: 参照先{typeName}(Id={id})がコレクション内に見つかりません。");
    }

    /// <summary>
    /// 指定座標に収束している(RailId, RailEnd, RailEndpointRef)の組を、全Railを走査して列挙する。
    /// </summary>
    /// <param name="position">収束を検出する座標。</param>
    /// <param name="rails">走査対象の全Rail。</param>
    /// <param name="noneEndpoints">現在のNoneEndpointコレクション。</param>
    /// <param name="boundaryPoints">現在のBoundaryPointコレクション。</param>
    /// <param name="entryPoints">現在のEntryPointコレクション。</param>
    /// <param name="bufferStops">現在のBufferStopコレクション。</param>
    /// <param name="switchers">現在のSwitcherコレクション。</param>
    /// <returns><paramref name="position"/>に収束しているRail端点の一覧。0件の場合は空リスト。</returns>
    /// <exception cref="InvalidOperationException">
    /// いずれかのRail端点が指す実体オブジェクトが見つからない場合（<see cref="ResolvePosition"/>経由）。
    /// </exception>
    /// <remarks>
    /// 同一Railの両端が同一座標に収束するケース（自己ループ）は現行モデルでは考慮不要
    /// （Rail作成・移動のいずれの経路でも両端を同一点にする操作はUI上想定されていないため、
    /// 検出はするが特別扱いはしない＝両端とも収束集合に普通に加わる）。
    /// </remarks>
    public static IReadOnlyList<RailEndpointLocation> FindConvergingEndpoints(
        Point position,
        IReadOnlyList<Rail> rails,
        IReadOnlyList<NoneEndpoint> noneEndpoints,
        IReadOnlyList<BoundaryPoint> boundaryPoints,
        IReadOnlyList<EntryPoint> entryPoints,
        IReadOnlyList<BufferStop> bufferStops,
        IReadOnlyList<Switcher> switchers)
    {
        var result = new List<RailEndpointLocation>();

        foreach (var rail in rails)
        {
            var posA = ResolvePosition(rail.EndpointA, noneEndpoints, boundaryPoints, entryPoints, bufferStops, switchers);
            if (posA.Equals(position))
                result.Add(new RailEndpointLocation(rail.Id, RailEnd.A, rail.EndpointA));

            var posB = ResolvePosition(rail.EndpointB, noneEndpoints, boundaryPoints, entryPoints, bufferStops, switchers);
            if (posB.Equals(position))
                result.Add(new RailEndpointLocation(rail.Id, RailEnd.B, rail.EndpointB));
        }

        return result;
    }

    /// <summary>
    /// 収束集合から変換種別を判定する。
    /// </summary>
    /// <param name="converging"><see cref="FindConvergingEndpoints"/>が返す収束集合。</param>
    /// <returns>
    /// 収束数に応じた<see cref="ConvergenceClassification"/>。N=3,4の場合、収束集合中に既存
    /// <see cref="SwitcherEndpointRef"/>が1件でも含まれていれば<see cref="ConvergenceKind.SwitcherExpand"/>、
    /// 含まれていなければ<see cref="ConvergenceKind.SwitcherNew"/>を返す。N&gt;=5は
    /// <see cref="ConvergenceClassification.Fail"/>によるエラーを返す（例外は投げない）。
    /// </returns>
    /// <remarks>
    /// N=0（削除方向で呼ばれ、当該座標を参照するRailがもう1本もない場合）を独立ケースとして扱う点が
    /// v13.13時点との差分：Rail削除に伴う「再分類」呼び出しで初めて起こりうるケースであり、
    /// Rail新規作成・ドラッグ方向の呼び出しでは通常発生しない（自分自身は集合に含まれるため）。
    /// 前提：同一Switcherの複数ポートが収束集合に混在していても、Idが同じなので拡張と判定して問題ない。
    /// </remarks>
    public static ConvergenceClassification Classify(IReadOnlyList<RailEndpointLocation> converging)
    {
        var n = converging.Count;

        if (n == 0)
            return ConvergenceClassification.Ok(ConvergenceKind.Vanish);

        if (n == 1)
            return ConvergenceClassification.Ok(ConvergenceKind.Keep);

        if (n == 2)
            return ConvergenceClassification.Ok(ConvergenceKind.BoundaryPoint);

        if (n is 3 or 4)
        {
            var hasExistingSwitcher = converging.Any(c => c.Ref is SwitcherEndpointRef);
            return ConvergenceClassification.Ok(
                hasExistingSwitcher ? ConvergenceKind.SwitcherExpand : ConvergenceKind.SwitcherNew);
        }

        return ConvergenceClassification.Fail("任意の点を参照するRailの数は4以下である必要があります");
    }

    /// <summary>
    /// 新規Switcher作成時のPort機械採番（暫定案、v13.13：Rail.Id昇順で0〜N-1）。
    /// </summary>
    /// <param name="converging">
    /// Port割当対象の収束集合。呼び出し前提：<see cref="Classify"/>が
    /// <see cref="ConvergenceKind.SwitcherNew"/>を返したケースでのみ使う
    /// （<see cref="ConvergenceKind.SwitcherExpand"/>は既存PortCountへの追加のため別ロジックが必要、本メソッドの対象外）。
    /// </param>
    /// <returns>
    /// 各<see cref="RailEndpointLocation"/>に対応するPortIndexの割当。同一Rail.Id内の順序に依存しない
    /// 一意な並びを保証するため、Rail.Id→(必要ならEnd)で安定ソートした結果を0始まりで返す。
    /// </returns>
    public static IReadOnlyList<(RailEndpointLocation Location, int PortIndex)> AssignSwitcherPorts(
        IReadOnlyList<RailEndpointLocation> converging)
    {
        return converging
            .OrderBy(c => c.RailId.Value)
            .ThenBy(c => c.End)
            .Select((loc, index) => (loc, index))
            .ToList();
    }

    /// <summary>
    /// 収束変換により消滅する既存EntryPoint/BoundaryPoint/BufferStop/Switcherを
    /// StationPath.Waypointsが参照していないか確認する。
    /// </summary>
    /// <param name="disappearingIds">
    /// 収束によって消滅する側（＝収束集合中、これから作成するBoundaryPoint/Switcher以外の既存
    /// BoundaryPoint/EntryPoint/BufferStop/SwitcherのObjectId）。
    /// 既存Switcherが「拡張」される場合（<see cref="ConvergenceKind.SwitcherExpand"/>）はSwitcher自体は
    /// 消滅しない点に注意（呼び出し側でdisappearingIdsに含めないこと）。
    /// </param>
    /// <param name="stationPaths">検証対象の全StationPath。</param>
    /// <returns>
    /// <paramref name="disappearingIds"/>のいずれかをWaypointsに含むStationPathのId一覧。
    /// 該当が無ければ空リスト。
    /// </returns>
    /// <remarks>
    /// DependencyResolver.ResolveDirectDependentsには本チェックに相当するルールが存在しない
    /// （BoundaryPointObjectId／EntryPointObjectId／BufferStopObjectId／SwitcherObjectIdは
    /// いずれも意図的な終端[]。StationWork CRUD横展開と一体実装の方針でスコープ外とされている）。
    /// そのためDeleteRailCommandがPlatform/TemporaryRestriction/Trainに対して行っているのと同じ
    /// 「専用Indexを作らずStationPathsを直接線形走査する」パターンを踏襲する。
    /// NoneEndpointは対象外：StationPathSuggesterの前提（Rail端点種別が全て確定済みであること）により、
    /// NoneEndpointがWaypointsへ現れることは構造的にない。
    /// </remarks>
    public static IReadOnlyList<StationPathId> FindBlockingStationPaths(
        IReadOnlyCollection<ObjectId> disappearingIds,
        IReadOnlyList<StationPath> stationPaths)
    {
        if (disappearingIds.Count == 0) return [];

        var disappearingSet = new HashSet<ObjectId>(disappearingIds);

        return stationPaths
            .Where(sp => sp.Waypoints.Any(w => disappearingSet.Contains(w.ToObjectId())))
            .Select(sp => sp.Id)
            .ToList();
    }
}
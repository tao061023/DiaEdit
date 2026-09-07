namespace DiaEditCore.Algorithm.Stations.FloorUnitObjects;

using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;

/// <summary>
/// 収束点に集まっているRail端点1件を表す。RailMerger.RailEnd（A/B）を再利用する
/// （新規enumを増やさない。名前空間はDiaEditCore.Algorithm.Stations.FloorUnitObjectsで同一）。
/// </summary>
public readonly record struct RailEndpointLocation(RailId RailId, RailEnd End, RailEndpointRef Ref);

/// <summary>
/// §9.2項目33の収束変換ルールに基づく分類結果。
/// Vanish：N=0。削除されたRail自身のみがこの端点を参照していた（孤立・行き止まり）。
///         端点オブジェクト自体を削除してよい（再分類の余地がない）。
/// Keep：N=1、既存フロー据置（ユーザーが種別を明示選択、項目35）。他に参照Railが残っているため
///       端点オブジェクトは維持する。
/// BoundaryPoint：N=2。
/// SwitcherNew：N=3,4かつ収束集合中に既存Switcher参照が無い（新規Switcher作成）。
/// SwitcherExpand：N=3,4かつ収束集合中に既存Switcher参照が含まれる（既存Switcher拡張）。
/// Error：N&gt;=5。
/// </summary>
public enum ConvergenceKind { Vanish, Keep, BoundaryPoint, SwitcherNew, SwitcherExpand, Error }

public readonly record struct ConvergenceClassification(ConvergenceKind Kind, string? ErrorMessage = null)
{
    public static ConvergenceClassification Ok(ConvergenceKind kind) => new(kind);
    public static ConvergenceClassification Fail(string message) => new(ConvergenceKind.Error, message);
}

/// <summary>
/// 「ある座標にどのRail端点が収束しているか」を検出し、収束数から変換種別を分類する純粋関数群。
/// 対象がドラッグで新規に動いてきた端点か既存の確定済み端点かは区別しない（座標一致のみで判定）。
/// Switcherは複数PortIndexが同一オブジェクト（＝同一Base.Position）に属するため、
/// 「異なるPortIndexのSwitcherEndpointRef」も同一座標として自然に収束集合へ含まれる。
/// </summary>
public static class RailEndpointConvergenceResolver
{
    /// <summary>
    /// RailEndpointRefが指す実体オブジェクトのBase.Positionを解決する。
    /// 該当オブジェクトが見つからない場合は呼び出し側のデータ不整合とみなし例外を投げる
    /// （収束検出はセッション内の生きたコレクションに対してのみ呼ばれる想定のため）。
    /// </summary>
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
    /// 同一Railの両端が同一座標に収束するケース（自己ループ）は現行モデルでは考慮不要
    /// （Rail作成・移動のいずれの経路でも両端を同一点にする操作はUI上想定されていないため、
    /// 検出はするが特別扱いはしない＝両端とも収束集合に普通に加わる）。
    /// </summary>
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
    /// 収束集合から変換種別を判定する（§9.2項目33の収束変換ルール、v13.13確定＋N=0のVanish追加）。
    /// N=0（削除方向で呼ばれ、当該座標を参照するRailがもう1本もない場合）を独立ケースとして扱う点が
    /// v13.13時点との差分：Rail削除に伴う「再分類」呼び出しで初めて起こりうるケースであり、
    /// Rail新規作成・ドラッグ方向の呼び出しでは通常発生しない（自分自身は集合に含まれるため）。
    /// N=3,4の場合、収束集合中に既存SwitcherEndpointRefが1件でも含まれていれば「既存Switcher拡張」、
    /// 含まれていなければ「新規Switcher作成」と判定する。
    /// 前提：同一Switcherの複数ポートが収束集合に混在していても、Idが同じなので拡張と判定して問題ない。
    /// </summary>
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
    /// タスク3：新規Switcher作成時のPort機械採番（暫定案、v13.13：Rail.Id昇順で0〜N-1）。
    /// 呼び出し前提：Classify()がSwitcherNewを返したケースでのみ使う（SwitcherExpandは
    /// 既存PortCountへの追加のため別ロジックが必要、本メソッドの対象外）。
    /// 戻り値：各RailEndpointLocationに対応するPortIndexの割当（同一Rail.Id内の順序に依存しない
    /// 一意な並びを保証するため、Rail.Id→(必要ならEnd)で安定ソートする）。
    /// </summary>
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
    /// タスク4：収束変換により消滅する既存EntryPoint/BoundaryPoint/BufferStop/Switcherを
    /// StationPath.Waypointsが参照していないか確認する。
    ///
    /// DependencyResolver.ResolveDirectDependentsには本チェックに相当するルールが存在しない
    /// （BoundaryPointObjectId／EntryPointObjectId／BufferStopObjectId／SwitcherObjectIdは
    /// いずれも意図的な終端[]。StationWork CRUD横展開と一体実装の方針でスコープ外とされている）。
    /// そのためDeleteRailCommandがPlatform/TemporaryRestriction/Trainに対して行っているのと同じ
    /// 「専用Indexを作らずStationPathsを直接線形走査する」パターンを踏襲する。
    ///
    /// NoneEndpointは対象外：StationPathSuggesterの前提（Rail端点種別が全て確定済みであること）により、
    /// NoneEndpointがWaypointsへ現れることは構造的にない。
    ///
    /// 呼び出し前提：disappearingIdsは収束によって消滅する側（＝収束集合中、これから作成する
    /// BoundaryPoint/Switcher以外の既存BoundaryPoint/EntryPoint/BufferStop/SwitcherのObjectId）。
    /// 既存Switcherが「拡張」される場合（ConvergenceKind.SwitcherExpand）はSwitcher自体は消滅しない点に注意
    /// （呼び出し側でdisappearingIdsに含めないこと）。
    /// </summary>
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
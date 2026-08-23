namespace DiaEditCore.Algorithm;

using DiaEditCore.Model;
using DiaEditCore.Model.Routes;


/// <summary>
/// 向き解決済みの1ホップ分の発着情報。StationConnectionSegment（生データ、無向のA/Bペア）とは
/// 意図的に語彙を分ける：本レコードは常に「発側→着側」の向きが確定した後の出力である。
/// 無向のA/B語彙をそのまま使い回すと、呼び出し側が向き解決済みかどうかを型から読み取れなくなるため
/// （§9.1 SCS direction-agnostic renameセッションでの指摘）、From/To語彙に戻す。
/// </summary>
public sealed record EntryPointSequenceElement(
    StationId FromStationId,
    StationId ToStationId,
    EntryPointId FromEntryPointId,
    EntryPointId ToEntryPointId);

public static class EntryPointSequenceResolver
{
    /// <summary>
    /// 系統(i)：呼び出し側が既に走行方向の意図（fromStationId/toStationId）を持っている場合の
    /// 単一SCS向け無向マッチング。RailSequenceResolver.FindRailBetweenと同じ精神で、
    /// StationIdA/StationIdBのどちらがfrom/toに一致するかを見て向きを確定する。
    /// 一致しない場合はnull（呼び出し側で「このSCSは該当ホップではない」として扱う）。
    /// </summary>
    public static EntryPointSequenceElement? ResolveOriented(
        StationConnectionSegment seg,
        StationId fromStationId,
        StationId toStationId)
    {
        if (seg.StationIdA == fromStationId && seg.StationIdB == toStationId)
            return new EntryPointSequenceElement(fromStationId, toStationId, seg.EntryPointIdA, seg.EntryPointIdB);

        if (seg.StationIdA == toStationId && seg.StationIdB == fromStationId)
            return new EntryPointSequenceElement(fromStationId, toStationId, seg.EntryPointIdB, seg.EntryPointIdA);

        return null;
    }

    /// <summary>
    /// 系統(ii)：呼び出し側がStationConnection自体の情報（sc.Direction）しか持たない場合、
    /// MainRoute.StationOrder上の隣接関係から各SegmentのA/Bどちらが発側かを機械的に解決する。
    /// 都度導出・非保存。
    ///
    /// 権威あるMainRouteの選定：各Segment自身が保持するMainRouteId（sc.MainRouteIdではなく
    /// segment.MainRouteId）を使う。sc.MainRouteIdとの一致はStationConnectionValidatorが
    /// 保存時に保証する前提（§9.1セッション確定）のため、都度導出側はSegment自身の情報だけで
    /// 自己完結できる（呼び出し文脈への依存を減らす防御的設計）。
    ///
    /// ループ対応：MainRoute.StationOrderが環状（先頭駅=末尾駅）の場合でも、Index比較をmod演算で
    /// 行うことで境界を正しくまたげる（非ループの場合はmodが実質無効化されるだけで同じ式で扱える）。
    ///
    /// 各Segmentは、StationConnectionValidatorの検証によりStationOrder上で隣接するペアである
    /// ことが保存時に保証されている前提のため、直前ホップからの継承（チェーン）に頼らず、
    /// 全Segmentを毎回StationOrder上のIndexから独立に解決する（前ホップの解決結果に依存しないため
    /// 部分的なデータ不整合が後続ホップへ伝播しない、という利点もある）。
    ///
    /// 向きが解決できないSegment（MainRoute未検出／StationOrder上で隣接していない等の
    /// データ不整合）は結果から除外する（discard-and-regenerateの都度導出処理として、
    /// 例外を送出せず可能な範囲で結果を返す既存の防御的実装方針を踏襲。保存時の検出は
    /// StationConnectionValidator／StationConnectionSegmentValidator側の責務）。
    /// </summary>
    public static IReadOnlyList<EntryPointSequenceElement> Resolve(
        StationConnection sc,
        IReadOnlyList<StationConnectionSegment> allSegments,
        IReadOnlyList<MainRoute> allMainRoutes)
    {
        var result = new List<EntryPointSequenceElement>(sc.Segments.Count);
        var mainRouteCache = new Dictionary<MainRouteId, MainRoute?>();

        foreach (var segId in sc.Segments)
        {
            var seg = allSegments.FirstOrDefault(s => s.Id == segId);
            if (seg is null) continue; // 参照整合性エラーは別途保存時検証で検出する想定

            if (!mainRouteCache.TryGetValue(seg.MainRouteId, out var mainRoute))
            {
                mainRoute = allMainRoutes.FirstOrDefault(mr => mr.Id == seg.MainRouteId);
                mainRouteCache[seg.MainRouteId] = mainRoute;
            }
            if (mainRoute is null) continue; // MainRoute未検出＝向き解決不能。このSegmentのみスキップ

            var element = ResolveDirectionByStationOrder(seg, sc.Direction, mainRoute.StationOrder, mainRoute.IsLoop);
            if (element is not null) result.Add(element);
            // else: StationOrder上で隣接していない＝データ不整合。スキップ
            // （StationConnectionValidatorが保存時に検出する想定）
        }

        return result;
    }

    /// <summary>
    /// 1つのSegmentについて、StationOrder上でのIndex差分(diff)から発側を機械的に解決する。
    ///
    /// v12.29修正：当初はidxAからstep分進んだ先がidxBと一致するかをmod演算で判定していたが、
    /// count=2（2駅のみの路線、単純な単線区間で最も頻出する構成）でDown/Upのstepの符号が
    /// 打ち消し合って区別できなくなるバグがあった（+1 mod 2 と -1 mod 2 が同値になるため）。
    /// diffベースの判定に切り替えることで、隣接判定（diff==±1）とループ境界判定
    /// （|diff|==count-1、count>=3でのみ意味を持つ）を明確に分離し、count=2での曖昧さを解消する。
    /// </summary>
    private static EntryPointSequenceElement? ResolveDirectionByStationOrder(
        StationConnectionSegment seg,
        StationConnectionDirection direction,
        IReadOnlyList<StationId> stationOrder,
        bool isLoop)
    {
        var count = stationOrder.Count;
        if (count < 2) return null;

        var idxA = IndexOfStation(stationOrder, seg.StationIdA);
        var idxB = IndexOfStation(stationOrder, seg.StationIdB);
        if (idxA < 0 || idxB < 0) return null;

        var diff = idxB - idxA;

        bool ascendingIsAtoB;
        if (diff == 1)
        {
            ascendingIsAtoB = true; // 通常の隣接：Aが低index側
        }
        else if (diff == -1)
        {
            ascendingIsAtoB = false; // 通常の隣接：Bが低index側
        }
        else if (isLoop && count >= 3 && Math.Abs(diff) == count - 1)
        {
            // ループ境界（index 0とindex count-1のペア）。isLoop==trueの場合のみ有効な
            // 隣接とみなす（v12.29修正：isLoopを見ずにIndex差分だけで判定すると、
            // count==3のとき「真ん中の駅を飛ばした不整合データ」と「本物のループ境界」が
            // 区別できず、前者を誤って正常として受理してしまうバグがあった）。
            // 昇順（Down）は末尾→先頭へ折り返すため、count-1側を保持する方が「発側」となる。
            ascendingIsAtoB = idxA == count - 1;
        }
        else
        {
            return null; // StationOrder上で隣接していない（データ不整合、またはisLoop=falseでの境界誤爆）
        }

        var aIsFrom = direction == StationConnectionDirection.Down ? ascendingIsAtoB : !ascendingIsAtoB;

        return aIsFrom
            ? new EntryPointSequenceElement(seg.StationIdA, seg.StationIdB, seg.EntryPointIdA, seg.EntryPointIdB)
            : new EntryPointSequenceElement(seg.StationIdB, seg.StationIdA, seg.EntryPointIdB, seg.EntryPointIdA);
    }

    /// <summary>
    /// IReadOnlyList&lt;T&gt;にはIndexOfが定義されていない（List&lt;T&gt;専用）ため、
    /// MainRoute.StationOrderがIReadOnlyList&lt;StationId&gt;として渡ってきても動くよう
    /// 手動で走査する。readonly record structの値等価性(StationId.Equals)で比較する。
    /// </summary>
    private static int IndexOfStation(IReadOnlyList<StationId> stationOrder, StationId target)
    {
        for (var i = 0; i < stationOrder.Count; i++)
        {
            if (stationOrder[i] == target) return i;
        }
        return -1;
    }
}
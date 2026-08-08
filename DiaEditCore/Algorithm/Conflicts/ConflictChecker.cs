using DiaEditCore.Model;

namespace DiaEditCore.Algorithm.Conflicts;

/// <summary>
/// 交差支障検知：あるオブジェクトを列車が占有しているとき、別の列車が同じオブジェクトを同時に占有していないかを検証する汎用チェッカー。 <br/>
/// 「対象オブジェクトID」と「占有時間帯」のみをキーとし、Track・StationPath・StationConnectionSegmentの3用途を同一の仕組みで扱う。
/// </summary>
public sealed class ConflictChecker
{
    public sealed record Occupancy(TrainId TrainId, int StartSeconds, int EndSeconds);

    public ObjectId TargetObjectId { get; }
    private readonly List<Occupancy> _occupancyRanges;

    public ConflictChecker(ObjectId targetObjectId, IReadOnlyList<Occupancy> occupancyRanges)
    {
        TargetObjectId = targetObjectId;
        _occupancyRanges = occupancyRanges.ToList();
    }

    /// <summary>
    /// 同一targetObjectId内の時間帯重複を検出する（スイープライン方式）。 <br/>
    ///
    /// 1. occupancyRangesをstartSeconds昇順にソート                         … O(n log n) <br/>
    /// 2. endSecondsを比較キーとする最小ヒープ（アクティブ区間集合）を用意 <br/>
    /// 3. ソート順に各区間を走査： <br/>
    ///    a. ヒープ先頭のendSecondsが現区間のstartSeconds以下なら順次pop      … 全体でO(n log n) <br/>
    ///    b. ヒープに残っている区間は全て現区間と重複しているため、重複ペアとして記録  … O(k) <br/>
    ///    c. 現区間をヒープにpush                                            … O(log n) <br/>
    ///
    /// 計算量：合計O((n + k) log n)。 <br/>
    /// 停止性：ソート・ヒープ操作とも有限要素の1回走査のみで完結し必ず停止する。 <br/>
    /// 一意性：開始時刻が同値の場合の走査順によって戻り値配列内の表示順は変わりうるが、検出される重複ペアの集合自体は入力順序に依存せず一意。 <br/>
    /// </summary>
    public IReadOnlyList<(TrainId A, TrainId B)> CheckOverlap()
    {
        var sorted = _occupancyRanges.OrderBy(o => o.StartSeconds).ToList();
        var active = new PriorityQueue<Occupancy, int>(); // key = EndSeconds
        var result = new List<(TrainId, TrainId)>();

        foreach (var cur in sorted)
        {
            while (active.Count > 0 && active.Peek().EndSeconds <= cur.StartSeconds)
                active.Dequeue();

            foreach (var kept in active.UnorderedItems)
                result.Add((kept.Element.TrainId, cur.TrainId));

            active.Enqueue(cur, cur.EndSeconds);
        }

        return result;
    }
}
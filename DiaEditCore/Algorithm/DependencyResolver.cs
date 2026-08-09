namespace DiaEditCore.Algorithm;

using DiaEditCore.Model;
using DiaEditCore.Model.TimeTable;

/// <summary>
/// 変更対象オブジェクト群から、依存関係グラフ（TimeTableSetCacheの逆引きインデックス）を辿って
/// 影響を受ける全オブジェクトIDを算出する。 <br/>
///
/// 停止性：プロジェクト内のオブジェクト総数は有限で、visited集合が既訪問ノードの再訪問を防ぐため、
///        依存グラフに循環（pairedMainRoute等の双方向参照）が存在しても必ず停止する。 <br/>
/// 一意性：ルールテーブル（ResolveDirectDependents）とキャッシュのインデックスが決定的な限り、
///        同一changedIds・同一プロジェクト状態から常に同一結果となる。 <br/>
/// 計算量：O(V+E)（V=影響を受けたオブジェクト数、E=辿った依存エッジ数） <br/>
///
/// 呼び出しタイミング：削除系操作では、変更対象のインデックスがまだ整合している削除実行前に呼ぶこと。 <br/>
/// execute()実行時点で一度だけ呼び、結果をコマンドのメンバ変数として保持し、undo()時には再算出しない。
/// </summary>
public static class DependencyResolver
{
    public static IReadOnlySet<ObjectId> ResolveAffected(
        IReadOnlySet<ObjectId> changedIds,
        TimeTableSetCache cache)
    {
        var visited = new HashSet<ObjectId>(changedIds);
        var queue = new Queue<ObjectId>(changedIds);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var dependent in ResolveDirectDependents(current, cache))
            {
                if (visited.Add(dependent))
                {
                    queue.Enqueue(dependent);
                }
            }
        }

        return visited;
    }

    /// <summary>
    /// 単一オブジェクトの直接の依存先（1ホップ分）を返す。依存関係ルールテーブルの実体。 <br/>
    /// 新しいObjectId派生型を追加した場合、CS8509(error)によりここでのケース追加漏れがビルドエラーとなる <br/>
    /// （.editorconfigでdotnet_diagnostic.CS8509.severity=errorが設定済みのため）。
    /// </summary>
    private static IEnumerable<ObjectId> ResolveDirectDependents(ObjectId current, TimeTableSetCache cache) =>
        current switch
        {
            // Station → StationConnection
            StationObjectId s =>
                cache.StationConnectionIndex.TryGetValue(s.Id, out var scByStation)
                    ? scByStation.Select(id => (ObjectId)new StationConnectionObjectId(id))
                    : [],

            // EntryPoint → StationConnection
            EntryPointObjectId e =>
                cache.EntryPointConnectionIndex.TryGetValue(e.Id, out var scByEntry)
                    ? scByEntry.Select(id => (ObjectId)new StationConnectionObjectId(id))
                    : [],

            // StationConnectionSegment → StationConnection（逆引き。共有時は複数SC）
            //                        → TemporaryRestriction
            StationConnectionSegmentObjectId scs =>
                (cache.ScsUsedByIndex.TryGetValue(scs.Id, out var scBySegment)
                    ? scBySegment.Select(id => (ObjectId)new StationConnectionObjectId(id))
                    : Enumerable.Empty<ObjectId>())
                .Concat(
                    cache.TemporaryRestrictionBySegmentIndex.TryGetValue(scs.Id, out var trBySegment)
                        ? trBySegment.Select(id => (ObjectId)new TemporaryRestrictionObjectId(id))
                        : []),

            // MainRoute → StationConnection
            MainRouteObjectId m =>
                cache.MainRouteConnectionIndex.TryGetValue(m.Id, out var scByRoute)
                    ? scByRoute.Select(id => (ObjectId)new StationConnectionObjectId(id))
                    : [],

            // 以下は現時点でこのグラフの終端（他オブジェクトを波及させるルールが未定義）
            StationConnectionObjectId => [],
            TemporaryRestrictionObjectId => [],
            BoundaryPointObjectId => [],
            BufferStopObjectId => [],
            SwitcherObjectId => [],
            RailObjectId => [],
            VirtualConflictObjectIdObject => [],
            TrainObjectId => [],

            // CS8509（error化済み）は参照型switchでnullケースも網羅対象とするため明示。
            // ResolveAffected()側はchangedIds/queueにnullを積まない前提だが、
            // 将来の呼び出し元の実装ミス（null混入）を早期に検知するために例外化する。
            null => throw new ArgumentNullException(nameof(current)),

            not null => throw new NotSupportedException(
                $"DependencyResolver: 未対応のObjectId種別です: {current.GetType().Name}")
        };
}
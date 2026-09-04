namespace DiaEditCore.Algorithm.Dependency;

using DiaEditCore.Model;

/// <summary>
/// 変更対象オブジェクト群から、依存関係グラフ（TimeTableSetCacheの逆引きインデックス）を辿って影響を受ける全オブジェクトIDを算出する。 <br/>
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
    /// （.editorconfigでdotnet_diagnostic.CS8509.severity=errorが設定済みのため）。 <br/>
    /// public化：ResolveAffected内部での波及探索用途に加え、削除系コマンドが
    /// 「直接の参照元が残っている場合はexecute時点で拒否する」判定に使う1ホップ専用の
    /// 問い合わせとしても利用する。 <br/>
    ///
    /// 【v12.31 §9.1項目20対応】従来は末尾が`null => throw` ／ `not null => throw`の2ケースで
    /// 構成されており、`null ∪ not null`が参照型に対し数学的に完全網羅となるため、
    /// CS8509によるコンパイル時網羅性チェックが実質無効化されていた。
    /// `ObjectId.cs`の24種類の派生型全てを明示的にケース化することで是正する。
    /// 依存関係ルールが未設計の型は`=> []`（終端）と明示し、「未実装」と「意図的に終端」を
    /// コード上で区別できない状態を解消する（コメントで区別を残す）。
    /// </summary>
    public static IEnumerable<ObjectId> ResolveDirectDependents(ObjectId current, TimeTableSetCache cache) =>
        current switch
        {
            // Station → StationConnection（間接、SC経由）
            //        → MainRoute（StationOrder直接参照）
            //        → StationConnectionSegment（From/ToStationId直接参照。
            //           SC未所属の孤立Segmentも含めて捕捉する）
            StationObjectId s =>
                (cache.StationConnectionIndex.TryGetValue(s.Id, out var scByStation)
                    ? scByStation.Select(id => (ObjectId)new StationConnectionObjectId(id))
                    : Enumerable.Empty<ObjectId>())
                .Concat(
                    cache.StationUsedByMainRouteIndex.TryGetValue(s.Id, out var mrByStation)
                        ? mrByStation.Select(id => (ObjectId)new MainRouteObjectId(id))
                        : [])
                .Concat(
                    cache.StationUsedBySegmentIndex.TryGetValue(s.Id, out var segByStation)
                        ? segByStation.Select(id => (ObjectId)new StationConnectionSegmentObjectId(id))
                        : []),

            // EntryPoint → StationConnection（既存）
            //           → StationConnectionSegment（孤立Segmentからの直接参照を捕捉）
            EntryPointObjectId e =>
                (cache.EntryPointConnectionIndex.TryGetValue(e.Id, out var scByEntry)
                    ? scByEntry.Select(id => (ObjectId)new StationConnectionObjectId(id))
                    : Enumerable.Empty<ObjectId>())
                .Concat(
                    cache.EntryPointUsedBySegmentIndex.TryGetValue(e.Id, out var segByEntry)
                        ? segByEntry.Select(id => (ObjectId)new StationConnectionSegmentObjectId(id))
                        : []),

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

            // MainRoute → StationConnection（既存）
            //          → StationConnectionSegment（孤立Segmentからの直接参照を捕捉）
            // 【未対応】ServiceRouteSegment（MainRouteId／PairedMainRouteId）経由・DisplayContext
            // （MainRouteRanges）経由の直接参照は§5.14.4棚卸しの対象のまま未実装（§9.1項目3残課題）。
            MainRouteObjectId m =>
                (cache.MainRouteConnectionIndex.TryGetValue(m.Id, out var scByRoute)
                    ? scByRoute.Select(id => (ObjectId)new StationConnectionObjectId(id))
                    : Enumerable.Empty<ObjectId>())
                .Concat(
                    cache.MainRouteUsedBySegmentIndex.TryGetValue(m.Id, out var segByRoute)
                        ? segByRoute.Select(id => (ObjectId)new StationConnectionSegmentObjectId(id))
                        : []),

            // StationConnection → ServiceRoute（ServiceRouteSegment.SelectedStationConnectionId／
            //                     PairedSelectedStationConnectionId経由。§9.1項目3・v12.31対応）
            StationConnectionObjectId sc =>
                cache.StationConnectionUsedByServiceRouteIndex.TryGetValue(sc.Id, out var routesByConnection)
                    ? routesByConnection.Select(id => (ObjectId)new ServiceRouteObjectId(id))
                    : [],

            // FloorUnit → 配下オブジェクト（BoundaryPoint/EntryPoint/BufferStop/Switcher/Platform/StationPath）
            FloorUnitObjectId f =>
                cache.FloorUnitDependentIndex.TryGetValue(f.Id, out var floorUnitDependents)
                    ? floorUnitDependents
                    : [],

            // 以下は現時点でこのグラフの終端（他オブジェクトを波及させるルールが未定義）。
            // 【意図的に終端（実装済み・正しい）】
            TemporaryRestrictionObjectId => [],
            PlatformObjectId => [],
            TrainObjectId => [], // 将来的にTimeTableSet.TrainIds等の逆参照要検討（5.13.3節）

            // 【未実装・§9.1項目3残課題】BoundaryPoint／EntryPoint／StationPath（Waypoints経由）は
            // §9.2項目10（StationWork CRUD横展開）と一体で実装する方針のため、
            // 現行実装スコープ（駅作業なし・最小構成）では意図的に終端のまま据え置く。
            // FloorUnitDependentIndex経由でFloorUnitObjectIdから辿られる側であり、
            // NoneEndpoint自体が他オブジェクトへ波及させるルールは未定義（BoundaryPoint/BufferStop/Switcherと同様）。
            NoneEndpointObjectId => [],
            BoundaryPointObjectId => [],
            BufferStopObjectId => [],
            SwitcherObjectId => [],
            StationPathObjectId => [], // StationWork.StationPathId経由の被参照も同様に未実装

            // RailObjectIdは専用インデックス化を見送り、DeleteRailCommand内の直接線形走査で
            // 対応する方針が確定済み（5.13.4節）。ObjectIdグラフ自体は終端のまま。
            RailObjectId => [],

            VirtualConflictObjectIdObject => [],

            // 【v12.31新設・24種明示化】ここから9種。いずれも被参照ルールが未設計のため終端扱いとするが、
            // 「意図的な終端」ではなく「未検討」である点を区別してコメントに残す（§9.1項目20クローズ時の申し送り）。

            // Train → ServiceRoute（Train.ServiceRouteId）を持つが、逆方向
            // （ServiceRoute削除時にTrainを検知する索引）は§5.14.4棚卸し「Train関連4種」の1つとして未実装。
            ServiceRouteObjectId => [],

            // CarConsist.Cars（CarRef.CarId）からの参照あり（5.13.2節Cars層）。
            // Car←CarConsist逆引きは§5.14.4「CarConsist・CarComposition関連」棚卸し未着手。
            CarObjectId => [],

            // CarComposition.CarConsistIdからの参照あり。CarConsist←CarComposition逆引きは同上、未着手。
            CarConsistObjectId => [],

            // StartOpCarSlot／CutGroupEntry.CarCompositionIdからの参照あり（StationWorkスコープのため
            // 現行実装スコープ外）。逆引きは未着手。
            CarCompositionObjectId => [],

            // CarConsist.VehicleTypeId／Train.DefaultVehicleTypeIdからの参照あり。
            // VehicleType←CarConsist・Train逆引きは§5.14.4「Train関連4種」の1つとして未実装。
            VehicleTypeObjectId => [],

            // Train.TrainTypeIdからの参照あり。TrainType←Train逆引きは§5.14.4「Train関連4種」の1つ
            // として未実装（現行実装スコープの中核エンティティだが優先度は§9.1項目3等より低いと判断）。
            TrainTypeObjectId => [],

            // DiagramRevision.TimeTableSetIds／BaseTimeTableSetId、Train.TimeTableSetIdからの参照あり。
            // TimeTableSet←DiagramRevision・Train逆引きは§5.14.4「Train関連4種」の1つとして未実装。
            TimeTableSetObjectId => [],

            // DiagramRevision.BaseRevisionId（自己参照）の削除時参照元チェック要否自体が
            // §9.2項目20で未確定のため、ルール設計を保留したまま終端とする。
            DiagramRevisionObjectId => [],

            // DisplayContextは被参照ゼロの独立ノード（MainRouteRanges等でMainRouteを参照する側であり、
            // 逆に他から参照される側ではない）。将来的にも終端である可能性が高いが、
            // 「意図的に確認済み終端」と判定するのは他8種と合わせて次回精査時とする。
            DisplayContextObjectId => [],

            // CS8509（error化済み）は参照型switchでnullケースも網羅対象とするため明示。
            // ResolveAffected()側はchangedIds/queueにnullを積まない前提だが、
            // 将来の呼び出し元の実装ミス（null混入）を早期に検知するために例外化する。
            null => throw new ArgumentNullException(nameof(current)),
            not null => throw new NotSupportedException(
                $"DependencyResolver: 未対応のObjectId種別です: {current.GetType().Name}")
        };
}
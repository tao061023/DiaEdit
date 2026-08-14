namespace DiaEditCore.Serialization.Validation.Routes;

using DiaEditCore.Model;
using DiaEditCore.Model.Routes;

/// <summary>
/// 複線区間（同一MainRoute・同一Direction）内で、同一StationConnectionSegmentが
/// 2つ以上のStationConnectionから参照されていないかを検証する。
///
/// v12.19設計セッションで確定した前提：SCは「複々線・双単線区間における経路をユーザーが
/// 分かりやすいようにグルーピングするための用途」であり、複線区間にまで2つ以上のSCを
/// 持たせることは許可しない。この1本のルールだけで以下3ケースが正しく分類される：
///
///   - 複々線区間：緩行線・急行線は別の物理Rail（別EntryPoint経由）を通るため、区間ごとに
///     別のSCSを参照する。同一SCSを共有することはない → 検知されない（正しく許可）。
///   - 単純な複線区間：物理的な経路が1本のため、その区間のSCSは1個のみ存在しうる。それを
///     2つのSCが同一方向で参照しようとすれば必ず同一SCSを共有する → 検知される（正しく禁止）。
///   - 双単線区間：同一SCSを上り方向SCと下り方向SCの両方が参照するが、Directionが異なるため
///     本ルールの対象外 → 検知されない（正しく許可）。
///
/// 実装はScsUsedByIndexBuilderと同じ「StationConnection.Segmentsを1回走査するだけ」の
/// ロジックを踏襲するが、TimeTableSetCacheには依存せずValidationContextの生データから
/// 都度算出する。
///
/// TrainOperationCrossValidator／TrainOperationUniquenessValidatorと同じ「単一オブジェクト
/// Validatorの契約（IValidator&lt;T&gt;）に収まらない検証」向けの静的Runパターンを踏襲する
/// （ICrossValidatorのようなインターフェースは存在しないため、これらと同じ静的メソッド呼び出し規約に揃える）。
/// ProjectSettingsへの依存が無いため、他の2つと異なりRunはValidationContextのみを引数に取る。
/// </summary>
public static class StationConnectionSegmentOverlapCrossValidator
{
    public static IReadOnlyList<IValidationIssue> Run(ValidationContext context)
    {
        var issues = new List<IValidationIssue>();

        // (MainRouteId, Direction, SegmentId) → 参照元StationConnectionId一覧
        var groups = new Dictionary<
            (MainRouteId MainRouteId, StationConnectionDirection Direction, StationConnectionSegmentId SegmentId),
            List<StationConnectionId>>();

        foreach (var sc in context.StationConnections)
        {
            foreach (var segId in sc.Segments)
            {
                var key = (sc.MainRouteId, sc.Direction, segId);
                if (!groups.TryGetValue(key, out var list))
                {
                    list = new List<StationConnectionId>();
                    groups[key] = list;
                }
                list.Add(sc.Id);
            }
        }

        foreach (var (key, scIds) in groups)
        {
            if (scIds.Count < 2) continue;

            issues.Add(new ValidationIssue(
                $"StationConnectionSegment({key.SegmentId.Value})がMainRoute({key.MainRouteId.Value})の" +
                $"同一方向（{key.Direction}）内で{scIds.Count}件のStationConnection" +
                $"（{string.Join(",", scIds.Select(id => id.Value))}）から重複参照されています。" +
                $"複線区間は1本の物理経路のみを表すため、同一方向で2つ以上のStationConnectionに" +
                $"分割することはできません（複々線として別経路にする場合は別のSCSを参照してください）。"));
        }

        return issues;
    }
}
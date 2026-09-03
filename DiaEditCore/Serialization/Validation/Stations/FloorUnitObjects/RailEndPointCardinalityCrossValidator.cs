namespace DiaEditCore.Serialization.Validation.Stations.FloorUnitObjects;

using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;

/// <summary>
/// Railの端点オブジェクト（BufferStop/EntryPoint/BoundaryPoint/Switcherの各ポート）が、
/// 物理的に許容される本数を超えて複数のRailから参照されていないかを検証する。
///
/// 許容数：
///   BufferStop：1（行き止まり）
///   EntryPoint：1（駅への入口）
///   BoundaryPoint：2（線路の連続）
///   Switcher：ポート単位(SwitcherId, PortIndex)で1（ポート数自体の妥当性はSwitcherValidatorの担当）
///
/// RailValidator（単一Rail向け）では他Railとの重複を検知できないため、
/// StationConnectionSegmentOverlapCrossValidator等と同じ「単一オブジェクトValidatorの契約に
/// 収まらない検証」向けの静的Runパターンを踏襲する（SaveValidationRunner.ValidateAllから呼ぶ）。
///
/// NoneEndpointRef（CreateRailCommand直後の未接続状態）は検証対象外。
/// </summary>
public static class RailEndpointCardinalityCrossValidator
{
    public static IReadOnlyList<IValidationIssue> Run(ValidationContext context)
    {
        var issues = new List<IValidationIssue>();
        var usage = new Dictionary<RailEndpointRef, List<RailId>>();

        void Register(RailEndpointRef endpoint, RailId railId)
        {
            if (endpoint is NoneEndpointRef) return;

            if (!usage.TryGetValue(endpoint, out var list))
            {
                list = new List<RailId>();
                usage[endpoint] = list;
            }
            list.Add(railId);
        }

        foreach (var rail in context.Rails)
        {
            Register(rail.EndpointA, rail.Id);
            Register(rail.EndpointB, rail.Id);
        }

        foreach (var (endpoint, railIds) in usage)
        {
            var limit = MaxCardinality(endpoint);
            if (railIds.Count > limit)
            {
                issues.Add(new ValidationIssue(
                    $"{DescribeEndpoint(endpoint)} に許容数({limit})を超えるRailが接続されています：" +
                    string.Join(", ", railIds.Select(id => $"Rail({id.Value})"))));
            }
        }

        return issues;
    }

    private static int MaxCardinality(RailEndpointRef endpoint) => endpoint switch
    {
        BufferStopEndpointRef => 1,
        EntryPointEndpointRef => 1,
        BoundaryPointEndpointRef => 2,
        SwitcherEndpointRef => 1,
        NoneEndpointRef => int.MaxValue, // Register側で除外済みのため到達しない
        _ => throw new NotSupportedException($"未知のRailEndpointRef型: {endpoint.GetType().Name}")
    };

    private static string DescribeEndpoint(RailEndpointRef endpoint) => endpoint switch
    {
        BufferStopEndpointRef b => $"BufferStop({b.Id.Value})",
        EntryPointEndpointRef e => $"EntryPoint({e.Id.Value})",
        BoundaryPointEndpointRef bp => $"BoundaryPoint({bp.Id.Value})",
        SwitcherEndpointRef s => $"Switcher({s.Id.Value}) Port{s.PortIndex}",
        _ => endpoint.GetType().Name
    };
}
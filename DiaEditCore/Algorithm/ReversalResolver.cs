using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.Stations;

namespace DiaEditCore.Algorithm;

/// <summary>
/// 6.10節：編成前後反転の自動導出。単一MainRoute内のスイッチバック判定（ResolveDirectionReversalStations）と、
/// 境界駅（MainRoute間）での折り返し判定（ResolveReversesAtBoundary）を、同一の判定基準
/// （EntryPoint.Type＋接続トポロジー。座標・表示用回転角は一切使わない）で扱う。
///
/// 判定基準：ある駅において、進入側で使用するEntryPointIdと進出側で使用するEntryPointIdが
/// 同一であれば、その駅は進入・進出を単一の物理的な出入口で行っている（＝デッドエンド構造であり、
/// 折り返しが必須）と判定する。異なるEntryPointIdであれば別々の出入口を持つ通過構造であり、
/// 折り返しは不要と判定する。
///
/// 両メソッドとも、EP引き当ての下請けとしてBoundaryEntryPointResolver（6.1節）を共有する。
/// 出力（directionReversalStations／ServiceRouteSegment.reversesAtBoundary）は
/// あくまで保存時のデフォルト値提示の候補であり、確定はユーザーが行う。
/// </summary>
public static class ReversalResolver
{
    /// <summary>
    /// mainRoute内の各中間駅（先頭・末尾を除く）について、スイッチバック判定を行う。
    /// 戻り値はStationId→判定結果（true=反転が必要と推定／false=不要と推定）。
    /// 前後いずれかの区間に対応するStationConnectionが存在しない駅は結果に含めない
    /// （判定不能。呼び出し側でdirectionReversalStationsへの自動登録候補から除外する）。
    /// </summary>
    public static Dictionary<StationId, bool> ResolveDirectionReversalStations(
        MainRoute mainRoute,
        IReadOnlyList<MainRoute> allMainRoutes,
        IReadOnlyList<StationConnection> allStationConnections,
        IReadOnlyList<StationConnectionSegment> allSegments)
    {
        var result = new Dictionary<StationId, bool>();
        var stationOrder = mainRoute.StationOrder;

        for (var i = 1; i < stationOrder.Count - 1; i++)
        {
            // 進入側：i-1 → i 方向で駅iに進入する際に使用するEP
            var arrivingEps = BoundaryEntryPointResolver.ResolveBoundaryEntryPoint(
                mainRoute.Id, i - 1, i, allMainRoutes, allStationConnections, allSegments);

            // 進出側：i+1 → i 方向（＝Up方向）で駅iに進入する際に使用するEP。
            // 単一線路のSCSはUp/Down双方のSCで共有されるため、i→i+1方向で駅iから進出する際に
            // 使用する物理的なEPと同一のもの（またはその判定に必要な同値性）が得られる。
            var departingEps = BoundaryEntryPointResolver.ResolveBoundaryEntryPoint(
                mainRoute.Id, i + 1, i, allMainRoutes, allStationConnections, allSegments);

            var stationId = stationOrder[i];
            var reversal = JudgeReversal(arrivingEps, departingEps);
            if (reversal is not null)
            {
                result[stationId] = reversal.Value;
            }
        }

        return result;
    }

    /// <summary>
    /// 境界駅（ServiceRouteSegmentの境界）における折り返し要否を判定する。
    /// prevSegmentの終端駅とnextSegmentの起点駅が同一駅であることを前提とする
    /// （異なる場合はnullを返し、判定不能として扱う）。
    /// </summary>
    public static bool? ResolveReversesAtBoundary(
        ServiceRouteSegment prevSegment,
        ServiceRouteSegment nextSegment,
        IReadOnlyList<MainRoute> allMainRoutes,
        IReadOnlyList<StationConnection> allStationConnections,
        IReadOnlyList<StationConnectionSegment> allSegments)
    {
        var prevMainRoute = allMainRoutes.FirstOrDefault(mr => mr.Id == prevSegment.MainRouteId);
        var nextMainRoute = allMainRoutes.FirstOrDefault(mr => mr.Id == nextSegment.MainRouteId);
        if (prevMainRoute is null || nextMainRoute is null) return null;

        var boundaryStationId = SafeStationAt(prevMainRoute, prevSegment.ToStationIndex);
        var nextStartStationId = SafeStationAt(nextMainRoute, nextSegment.FromStationIndex);
        if (boundaryStationId is null || nextStartStationId is null || boundaryStationId != nextStartStationId)
        {
            return null;
        }

        // prevSegment内で境界駅の1つ手前の駅（進入方向）
        var prevStep = Math.Sign(prevSegment.ToStationIndex - prevSegment.FromStationIndex);
        if (prevStep == 0) return null;
        var prevPenultimateIndex = prevSegment.ToStationIndex - prevStep;

        // nextSegment内で境界駅の1つ先の駅（進出方向）
        var nextStep = Math.Sign(nextSegment.ToStationIndex - nextSegment.FromStationIndex);
        if (nextStep == 0) return null;
        var nextFollowingIndex = nextSegment.FromStationIndex + nextStep;

        var arrivingEps = BoundaryEntryPointResolver.ResolveBoundaryEntryPoint(
            prevSegment.MainRouteId, prevPenultimateIndex, prevSegment.ToStationIndex,
            allMainRoutes, allStationConnections, allSegments);

        var departingEps = BoundaryEntryPointResolver.ResolveBoundaryEntryPoint(
            nextSegment.MainRouteId, nextFollowingIndex, nextSegment.FromStationIndex,
            allMainRoutes, allStationConnections, allSegments);

        return JudgeReversal(arrivingEps, departingEps);
    }

    /// <summary>
    /// 進入側候補群・進出側候補群のいずれかの組み合わせで同一EntryPointIdが使われていれば
    /// 折り返し必須（true）、いずれの組み合わせも一致しなければ不要（false）と判定する。
    /// 複々線等でどちらかの候補群が空の場合は判定不能（null）。
    /// </summary>
    private static bool? JudgeReversal(
        IReadOnlyList<EntryPointSequenceElement> arrivingEps,
        IReadOnlyList<EntryPointSequenceElement> departingEps)
    {
        if (arrivingEps.Count == 0 || departingEps.Count == 0) return null;

        foreach (var arriving in arrivingEps)
        {
            foreach (var departing in departingEps)
            {
                if (arriving.ToEntryPointId == departing.ToEntryPointId)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static StationId? SafeStationAt(MainRoute mainRoute, int index)
        => index >= 0 && index < mainRoute.StationOrder.Count ? mainRoute.StationOrder[index] : null;
}

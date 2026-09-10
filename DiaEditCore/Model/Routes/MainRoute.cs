namespace DiaEditCore.Model.Routes;

/// <summary>
/// 分岐を持たない1本の駅順経路
/// </summary>
/// <remarks>
/// 現実での、「路線」が該当する。（UI上では路線と表示する）
/// </remarks>
public sealed class MainRoute
{
    /// <summary>
    /// 路線識別子
    /// </summary>
    public required MainRouteId Id { get; set; }
    /// <summary>
    /// 路線名称
    /// </summary>
    /// <remarks>
    /// 全MainRouteで一意
    /// </remarks>
    public required DisplayName Name { get; set; }
    /// <summary>
    /// 駅の順序付き配列（分岐なし）
    /// </summary>
    /// <remarks>
    /// StationOrder[0]とStationOrder[last]はStationType != Halt
    /// </remarks>
    public required List<StationId> StationOrder { get; set; }
    /// <summary>
    /// 環状線フラグ
    /// </summary>
    public bool IsLoop { get; set; } = false;
    /// <summary>
    /// 進行方向が変わる（スイッチバックが発生する）駅リスト
    /// </summary>
    public List<StationId> DirectionReversalStations { get; set; } = new();
    /// <summary>
    /// 路線固有の駅名を持つ場合の駅名保持フィールド
    /// </summary>
    /// <remarks>
    /// 未設定ならStation.DisplayNameにフォールバック
    /// </remarks>
    public Dictionary<StationId, DisplayName> StationDisplayNameOverrides { get; set; } = new();
}
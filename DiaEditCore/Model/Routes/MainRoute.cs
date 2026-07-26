namespace DiaEditCore.Model.Routes;

public sealed class MainRoute
{
    public required MainRouteId Id { get; set; }
    public required DisplayName Name { get; set; }
    public required List<StationId> StationOrder { get; set; } // 駅の順序付き配列（分岐なし）
    public bool IsLoop { get; set; } = false;
    public List<StationId> DirectionReversalStations { get; set; } = new(); // スイッチバック駅（6.10節ReversalResolver参照）
    public Dictionary<StationId, DisplayName> StationDisplayNameOverrides { get; set; } = new();
    // 未設定ならStation.DisplayNameにフォールバック
}
namespace DiaEditCore.Model.Routes;

public enum StationConnectionDirection { Up, Down } // Both廃止（5.7節）

public sealed class StationConnection
{
    public required StationConnectionId Id { get; set; }
    public required string Name { get; set; } = ""; // 例: "上り本線"
    public required MainRouteId MainRouteId { get; set; }
    public required StationConnectionDirection Direction { get; set; }
    public required List<StationConnectionSegmentId> Segments { get; set; } // 実体参照。同一SCSIdを複数SCが共有しうる
}
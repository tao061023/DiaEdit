namespace DiaEditCore.Model;

public sealed class FloorUnit
{
    public required FloorUnitId Id { get; set; }
    public required StationId StationId { get; set; }
    public string Name { get; set; } = ""; // 空文字列許容。自動採番はしない
    public required int DisplayOrder { get; set; } // 同一StationId内で一意（保存時検証）
}
namespace DiaEditCore.Model;

public sealed class Platform
{
    public required PlatformId Id { get; set; }
    public required FloorUnitObjectBase Base { get; set; }
    public string Name { get; set; } = "";
    public required List<RailId> FacingRailIds { get; set; } = new(); // 正のデータ。多対多（櫛型ホーム対応）
    public double? EffectiveLength { get; set; } // 未設定の場合、FacingRailIdsのLengthMにフォールバック
}
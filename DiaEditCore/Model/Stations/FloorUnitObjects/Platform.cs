namespace DiaEditCore.Model.Stations.FloorUnitObjects;

/// <summary>
/// 駅のプラットホームを表す
/// </summary>
public sealed class Platform
{
    /// <summary>
    /// プラットホーム識別子
    /// </summary>
    public required PlatformId Id { get; set; }
    /// <summary>
    /// 駅階層識別子と座標情報を保持する複合フィールド。ドラッグ始端。
    /// </summary>
    public required FloorUnitObjectBase Base { get; set; }
    /// <summary>
    /// ドラッグ終端。
    /// </summary>
    /// <remarks>
    /// BaseのPositionとSecondaryPositionを対角線とする矩形でホームを表現する。
    /// </remarks>
    public required Point SecondaryPosition { get; set; }
    /// <summary>
    /// ホームの名称
    /// </summary>
    public string Name { get; set; } = "";
    /// <summary>
    /// 当ホームで客扱いを行う番線リスト
    /// </summary>
    /// <remarks>
    /// 多対多（櫛型ホーム対応のため）
    /// </remarks>
    public required List<RailId> FacingRailIds { get; set; } = new();
    /// <summary>
    /// ホーム有効長
    /// </summary>
    /// <remarks>
    /// 未設定の場合、FacingRailIdsのLengthMの最小値にフォールバックする
    /// </remarks>
    public double? EffectiveLength { get; set; }
}
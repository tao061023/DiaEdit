namespace DiaEditCore.Model.Cars;

/// <summary>
/// 組成単位を表現する
/// </summary>
public sealed class Car
{
    /// <summary>
    /// 組成単位識別子
    /// </summary>
    public required CarId Id { get; set; }
    /// <summary>
    /// 車両種別
    /// </summary>
    /// <remarks>
    /// "クハE234" など。表記のマスタ管理はUI側の責務。
    /// </remarks>
    public required string CarType { get; set; }
    /// <summary>
    /// 編成番号で置換する数字。Placeholder+編成番号で表現。
    /// </summary>
    /// <remarks>
    /// 1000番台などの1000を設定すればよい。
    /// </remarks>
    public int Placeholder { get; set; } = 0;
    /// <summary>
    /// 動力車フラグ
    /// </summary>
    public required bool IsPower { get; set; }
    /// <summary>
    /// 組成単位ごとの実長
    /// </summary>
    public required double LengthM { get; set; }
}
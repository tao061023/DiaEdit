namespace DiaEditCore.Model.Cars;

/// <summary>
/// 編成を表現する
/// </summary>
public sealed class CarComposition
{
    /// <summary>
    /// 編成識別子
    /// </summary>
    public required CarCompositionId Id { get; set; }
    /// <summary>
    /// 編成名
    /// </summary>
    /// <remarks>
    /// 例: "トウ01"
    /// </remarks>
    public required string Name { get; set; }
    /// <summary>
    /// 編成番号
    /// </summary>
    /// <remarks>
    /// 車番表記に利用
    /// </remarks>
    public required int Identifier { get; set; }
    /// <summary>
    /// 利用する組成済み編成の識別子
    /// </summary>
    public required CarConsistId CarConsistId { get; set; }
}
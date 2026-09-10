namespace DiaEditCore.Model.Cars;

/// <summary>
/// 組成タイプ
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description><c>Basic</c>；基本編成</description></item>
/// <item><description><c>Attached</c>：付属編成</description></item>
/// </list>
/// </remarks>
public enum CarConsistType { Basic, Attached }

/// <summary>
/// 組成単位参照型
/// </summary>
public sealed class CarRef
{
    /// <summary>
    /// 組成単位識別子
    /// </summary>
    public required CarId CarId { get; set; }
    /// <summary>
    /// 組成位置
    /// </summary>
    public required int Position { get; set; }
}

/// <summary>
/// 組成済み編成を表現する
/// </summary>
public sealed class CarConsist
{
    /// <summary>
    /// 組成済み編成識別子
    /// </summary>
    public required CarConsistId Id { get; set; }
    /// <summary>
    /// 車両形式の識別子
    /// </summary>
    public required VehicleTypeId VehicleTypeId { get; set; }
    /// <summary>
    /// 組成タイプ
    /// </summary>
    public required CarConsistType Type { get; set; }
    /// <summary>
    /// 編成の組成配列
    /// </summary>
    public required List<CarRef> Cars { get; set; }
}
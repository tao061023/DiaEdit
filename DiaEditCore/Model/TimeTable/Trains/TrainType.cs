namespace DiaEditCore.Model.TimeTable.Trains;

/// <summary>
/// ダイヤグラム線の種類
/// </summary>
/// <remarks>
/// 具体的な値はUI（ダイヤグラム）実装時に確定
/// <list type="bullet">
/// <item><description><c>Solid</c>：実線</description></item>
/// <item><description><c>Dashed</c>：破線</description></item>
/// <item><description><c>Dotted</c>：点線</description></item>
/// </list>
/// </remarks>
public enum LineStyle { Solid, Dashed, Dotted }

/// <summary>
/// 列車種別を表現する
/// </summary>
public sealed class TrainType
{
    /// <summary>
    /// 列車種別識別子
    /// </summary>
    public required TrainTypeId Id { get; set; }
    /// <summary>
    /// 列車種別名
    /// </summary>
    public required DisplayName Name { get; set; }
    /// <summary>
    /// ダイヤグラム線色
    /// </summary>
    public required string DiagramColor { get; set; }
    /// <summary>
    /// ダイヤグラム線種
    /// </summary>
    public required LineStyle DiagramLineStyle { get; set; }
    /// <summary>
    /// 同じ種別がServiceRouteをまたいで複数存在しても色・線種・並び順を統一
    /// </summary>
    /// <remarks>
    /// 親種別にした方がいい可能性。
    /// TrainTypeId? BaseTrainTypeId { get; set; }
    /// </remarks>
    public required int SortOrder { get; set; }
}
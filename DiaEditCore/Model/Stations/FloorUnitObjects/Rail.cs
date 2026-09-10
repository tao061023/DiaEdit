namespace DiaEditCore.Model.Stations.FloorUnitObjects;

/// <summary>
/// レール種別
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description><c>Normal</c>：本線など</description></item>
/// <item><description><c>Track</c>：番線。客扱いの有無にかかわらず、（オブジェクトとしての）駅を走行する中継地点として登録しなければならない。</description></item>
/// <item><description><c>Shunting</c>：引き上げ線、留置線。入替作業を行うための場所として登録する。</description></item>
/// </list>
/// </remarks>
public enum RailRole { Normal, Track, Shunting };

/// <summary>
/// Rail端点を参照するための抽象基底型
/// </summary>
/// <remarks>
/// 非 sealed のため派生型が増える可能性がある。
/// 利用側では型の網羅性を保証できない点に注意すること。
/// </remarks>
public abstract record RailEndpointRef;

/// <summary>
/// レール端点の「仮種別」を参照する型
/// </summary>
/// <param name="Id">仮種別識別子</param>
public sealed record NoneEndpointRef(NoneEndpointId Id) : RailEndpointRef;
/// <summary>
/// レール端点の「閉塞境界点」を参照する型
/// </summary>
/// <param name="Id">閉塞境界点識別子</param>
public sealed record BoundaryPointEndpointRef(BoundaryPointId Id) : RailEndpointRef;
/// <summary>
/// レール端点の「駅境界点」を参照する型
/// </summary>
/// <param name="Id">駅境界点識別子</param>
public sealed record EntryPointEndpointRef(EntryPointId Id) : RailEndpointRef;
/// <summary>
/// レール端点の「車止め」を参照する型
/// </summary>
/// <param name="Id">車止め識別子</param>
public sealed record BufferStopEndpointRef(BufferStopId Id) : RailEndpointRef;
/// <summary>
/// レール端点の「分岐器」を参照する型
/// </summary>
/// <remarks>
/// PortIndexはSwitcher接続時のみ意味を持つため、Switcher用派生型にのみ持たせる（構造的防止）
/// </remarks>
/// <param name="Id">分岐器識別子</param>
/// <param name="PortIndex">接続ポート位置</param>
public sealed record SwitcherEndpointRef(SwitcherId Id, int PortIndex) : RailEndpointRef;
/// <summary>
/// Rail描画用の中間制御点。
/// </summary>
/// <remarks>
/// 現時点では意味のないフィールド。将来的にRail端点に依存せずに折れ線や曲線を表現するために仮導入している。
/// </remarks>
public sealed class RailControlPoint
{
    /// <summary>
    /// 制御点座標
    /// </summary>
    public required Point Point { get; set; }
}

/// <summary>
/// レールを表現する
/// </summary>
public sealed class Rail
{
    /// <summary>
    /// レール識別子
    /// </summary>
    public required RailId Id { get; set; }
    /// <summary>
    /// レール名称
    /// </summary>
    /// <remarks>
    /// RailRole==Trackの場合、非空文字列かつ一意（保存時検証）
    /// </remarks>
    public string Name { get; set; } = "";
    /// <summary>
    /// レール長
    /// </summary>
    public required double LengthM { get; set; }
    /// <summary>
    /// 制限速度
    /// </summary>
    public required double SpeedLimitKph { get; set; }
    /// <summary>
    /// レール種別
    /// </summary>
    public required RailRole Role { get; set; }

    /// <summary>
    /// レール端点の参照先A
    /// </summary>
    public required RailEndpointRef EndpointA { get; set; }
    /// <summary>
    /// レール端点の参照先B
    /// </summary>
    public required RailEndpointRef EndpointB { get; set; }

    /// <summary>
    /// Rail描画用の中間制御点
    /// </summary>
    public List<RailControlPoint> ControlPoints { get; set; } = new();
}
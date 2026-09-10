namespace DiaEditCore.Model.Stations.FloorUnitObjects;

/// <summary>
/// 片開き、両開き分岐器の分岐構造を表現する
/// </summary>
public sealed class SwitchMechanism
{
    /// <summary>
    /// 基底側。構成可能な2つの進路の配列で共通のRailが該当する。
    /// </summary>
    public required int RootPortIndex { get; set; }
    /// <summary>
    /// 正位側
    /// </summary>
    public required int NormalPortIndex { get; set; }
    /// <summary>
    /// 反位側
    /// </summary>
    public required int ReversePortIndex { get; set; }
}

/// <summary>
/// 構成可能な進路をPort対で表現する
/// </summary>
/// <param name="PortA"></param>
/// <param name="PortB"></param>
public readonly record struct PortPair(int PortA, int PortB);

/// <summary>
/// 分岐器を表す
/// </summary>
public sealed class Switcher
{
    /// <summary>
    /// 分岐器識別子
    /// </summary>
    public required SwitcherId Id { get; set; }
    /// <summary>
    /// 駅階層識別子と座標情報を保持する複合フィールド
    /// </summary>
    public required FloorUnitObjectBase Base { get; set; }
    /// <summary>
    /// 収束するRailの数。
    /// </summary>
    /// <remarks>
    /// PortCount == 3 || 4 のみ整合。
    /// </remarks>
    public required int PortCount { get; set; }
    /// <summary>
    /// PortCount == 3 の場合に利用。
    /// 片開き、両開き分岐器の分岐構造を表現する。
    /// </summary>
    public SwitchMechanism? Mechanism { get; set; }
    /// <summary>
    /// PortCount == 4 の場合に利用。
    /// 要素数は 2 &lt;= かつ &lt;= 4
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><c>ValidRoutes=2</c>：シザースクロッシング</description></item>
    /// <item><description><c>ValidRoutes=3</c>：シングルスリップスイッチ</description></item>
    /// <item><description><c>ValidRoutes=4</c>：ダブルスリップスイッチ</description></item>
    /// </list>
    /// </remarks>
    public List<PortPair> ValidRoutes { get; set; } = new();
}

public static class SwitcherRoutingExtensions
{
    /// <summary>
    /// Switcherの構造（mechanism / validRoutes）から、通行可能なPortペアの集合を都度計算する。
    /// </summary>
    /// <remarks>
    /// 永続化はしない派生値。N=3はroot-normal・root-reverseの2組、N=4はvalidRoutesそのもの。<br/>
    /// PortIndexの割り当て順序に業務的意味を持たせないため、各ペアはPortA&lt;=PortBに正規化する。
    /// </remarks>
    public static IReadOnlySet<PortPair> GetTraversablePairs(this Switcher switcher)
    {
        if (switcher.Mechanism is { } m)
        {
            return new HashSet<PortPair>
            {
                Normalize(m.RootPortIndex, m.NormalPortIndex),
                Normalize(m.RootPortIndex, m.ReversePortIndex),
            };
        }

        return switcher.ValidRoutes.Select(p => Normalize(p.PortA, p.PortB)).ToHashSet();
    }
    /// <summary>
    /// PortPairのPortA&lt;=PortB正規化。
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static PortPair Normalize(int a, int b) => a <= b ? new PortPair(a, b) : new PortPair(b, a);
}
namespace DiaEditCore.Model.Stations.FloorUnitObjects;

public sealed class SwitchMechanism
{
    public required int RootPortIndex { get; set; }
    public required int NormalPortIndex { get; set; }
    public required int ReversePortIndex { get; set; }
}

public readonly record struct PortPair(int PortA, int PortB);

public sealed class Switcher
{
    public required SwitcherId Id { get; set; }
    public required FloorUnitObjectBase Base { get; set; }
    public required int PortCount { get; set; }
    public SwitchMechanism? Mechanism { get; set; }
    public List<PortPair> ValidRoutes { get; set; } = new();
}

public static class SwitcherRoutingExtensions
{
    /// <summary>
    /// Switcherの構造（mechanism / validRoutes）から、通行可能なPortペアの集合を都度計算する。
    /// 永続化はしない派生値。N=3はroot-normal・root-reverseの2組、N=4はvalidRoutesそのもの。
    /// PortIndexの割り当て順序に業務的意味を持たせないため、各ペアはPortA&lt;=PortBに正規化する。
    /// </summary>
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

    public static PortPair Normalize(int a, int b) => a <= b ? new PortPair(a, b) : new PortPair(b, a);
}
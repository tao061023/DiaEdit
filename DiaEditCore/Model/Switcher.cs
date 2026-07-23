namespace DiaEditCore.Model;

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
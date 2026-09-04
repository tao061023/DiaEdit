namespace DiaEditCore.Tests.Algorithm;

using DiaEditCore.Algorithm;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;

using Xunit;

public class RailMergerTests
{
    private static Rail MakeRail(
        int id,
        RailEndpointRef endpointA,
        RailEndpointRef endpointB,
        double lengthM = 10.0,
        double speedLimitKph = 25.0,
        string name = "",
        RailRole role = RailRole.Normal) => new()
    {
        Id = new RailId(id),
        Name = name,
        LengthM = lengthM,
        SpeedLimitKph = speedLimitKph,
        Role = role,
        EndpointA = endpointA,
        EndpointB = endpointB,
        ControlPoints = new List<RailControlPoint> { new() { Point = new Point(1, 1) } },
    };

    // ================================
    // MergeAtConvergence：端点保持の判定
    // ================================

    [Fact]
    public void 収束側がRailA_B_RailB_Aの場合_RailA_AとRailB_Bが保持される()
    {
        var epA_A = new BoundaryPointEndpointRef(new BoundaryPointId(1));
        var epA_B = new EntryPointEndpointRef(new EntryPointId(1)); // 収束点（破棄される）
        var epB_A = new EntryPointEndpointRef(new EntryPointId(1)); // 収束点（破棄される）
        var epB_B = new BufferStopEndpointRef(new BufferStopId(1));

        var railA = MakeRail(1, epA_A, epA_B);
        var railB = MakeRail(2, epB_A, epB_B);

        var merged = RailMerger.MergeAtConvergence(
            railA, RailEnd.B,
            railB, RailEnd.A,
            new RailId(99), resolvedName: "");

        Assert.Equal(epA_A, merged.EndpointA);
        Assert.Equal(epB_B, merged.EndpointB);
    }

    [Fact]
    public void 収束側がRailA_A_RailB_Bの場合_RailA_BとRailB_Aが保持される()
    {
        var epA_A = new EntryPointEndpointRef(new EntryPointId(1)); // 収束点（破棄される）
        var epA_B = new BoundaryPointEndpointRef(new BoundaryPointId(1));
        var epB_A = new BufferStopEndpointRef(new BufferStopId(1));
        var epB_B = new EntryPointEndpointRef(new EntryPointId(1)); // 収束点（破棄される）

        var railA = MakeRail(1, epA_A, epA_B);
        var railB = MakeRail(2, epB_A, epB_B);

        var merged = RailMerger.MergeAtConvergence(
            railA, RailEnd.A,
            railB, RailEnd.B,
            new RailId(99), resolvedName: "");

        Assert.Equal(epA_B, merged.EndpointA);
        Assert.Equal(epB_A, merged.EndpointB);
    }

    [Fact]
    public void 収束側がRailA_A_RailB_Aの場合_RailA_BとRailB_Bが保持される()
    {
        var epA_A = new EntryPointEndpointRef(new EntryPointId(1)); // 収束点（破棄される）
        var epA_B = new BoundaryPointEndpointRef(new BoundaryPointId(1));
        var epB_A = new EntryPointEndpointRef(new EntryPointId(1)); // 収束点（破棄される）
        var epB_B = new BufferStopEndpointRef(new BufferStopId(1));

        var railA = MakeRail(1, epA_A, epA_B);
        var railB = MakeRail(2, epB_A, epB_B);

        var merged = RailMerger.MergeAtConvergence(
            railA, RailEnd.A,
            railB, RailEnd.A,
            new RailId(99), resolvedName: "");

        Assert.Equal(epA_B, merged.EndpointA);
        Assert.Equal(epB_B, merged.EndpointB);
    }

    [Fact]
    public void 収束側がRailA_B_RailB_Bの場合_RailA_AとRailB_Aが保持される()
    {
        var epA_A = new BoundaryPointEndpointRef(new BoundaryPointId(1));
        var epA_B = new EntryPointEndpointRef(new EntryPointId(1)); // 収束点（破棄される）
        var epB_A = new BufferStopEndpointRef(new BufferStopId(1));
        var epB_B = new EntryPointEndpointRef(new EntryPointId(1)); // 収束点（破棄される）

        var railA = MakeRail(1, epA_A, epA_B);
        var railB = MakeRail(2, epB_A, epB_B);

        var merged = RailMerger.MergeAtConvergence(
            railA, RailEnd.B,
            railB, RailEnd.B,
            new RailId(99), resolvedName: "");

        Assert.Equal(epA_A, merged.EndpointA);
        Assert.Equal(epB_A, merged.EndpointB);
    }

    [Fact]
    public void SwitcherEndpointRefのPortIndexは保持側であればそのまま引き継がれる()
    {
        var convergingEp = new EntryPointEndpointRef(new EntryPointId(1)); // 収束点（破棄される）
        var switcherEp = new SwitcherEndpointRef(new SwitcherId(5), PortIndex: 2); // 保持される側

        var railA = MakeRail(1, convergingEp, switcherEp);
        var railB = MakeRail(2, convergingEp, new BufferStopEndpointRef(new BufferStopId(1)));

        var merged = RailMerger.MergeAtConvergence(
            railA, RailEnd.A,
            railB, RailEnd.A,
            new RailId(99), resolvedName: "");

        var resultSwitcherRef = Assert.IsType<SwitcherEndpointRef>(merged.EndpointA);
        Assert.Equal(new SwitcherId(5), resultSwitcherRef.Id);
        Assert.Equal(2, resultSwitcherRef.PortIndex);
    }

    // ================================
    // MergeAtConvergence：数値・その他フィールドの統合
    // ================================

    [Fact]
    public void LengthMは合算される()
    {
        var railA = MakeRail(1, new NoneEndpointRef(new NoneEndpointId(1)), new NoneEndpointRef(new NoneEndpointId(2)), lengthM: 12.5);
        var railB = MakeRail(2, new NoneEndpointRef(new NoneEndpointId(3)), new NoneEndpointRef(new NoneEndpointId(4)), lengthM: 7.3);

        var merged = RailMerger.MergeAtConvergence(
            railA, RailEnd.B, railB, RailEnd.A, new RailId(99), resolvedName: "");

        Assert.Equal(19.8, merged.LengthM, 3);
    }

    [Fact]
    public void SpeedLimitKphは小さい方が採用される()
    {
        var railA = MakeRail(1, new NoneEndpointRef(new NoneEndpointId(1)), new NoneEndpointRef(new NoneEndpointId(2)), speedLimitKph: 45);
        var railB = MakeRail(2, new NoneEndpointRef(new NoneEndpointId(3)), new NoneEndpointRef(new NoneEndpointId(4)), speedLimitKph: 25);

        var merged1 = RailMerger.MergeAtConvergence(
            railA, RailEnd.B, railB, RailEnd.A, new RailId(99), resolvedName: "");
        Assert.Equal(25, merged1.SpeedLimitKph);

        // 順序を入れ替えても結果が変わらないことも確認
        var merged2 = RailMerger.MergeAtConvergence(
            railB, RailEnd.A, railA, RailEnd.B, new RailId(100), resolvedName: "");
        Assert.Equal(25, merged2.SpeedLimitKph);
    }

    [Fact]
    public void ControlPointsは破棄される()
    {
        var railA = MakeRail(1, new NoneEndpointRef(new NoneEndpointId(1)), new NoneEndpointRef(new NoneEndpointId(2)));
        var railB = MakeRail(2, new NoneEndpointRef(new NoneEndpointId(3)), new NoneEndpointRef(new NoneEndpointId(4)));
        Assert.NotEmpty(railA.ControlPoints); // 前提確認

        var merged = RailMerger.MergeAtConvergence(
            railA, RailEnd.B, railB, RailEnd.A, new RailId(99), resolvedName: "");

        Assert.Empty(merged.ControlPoints);
    }

    [Fact]
    public void 新しいRailIdが発行される()
    {
        var railA = MakeRail(1, new NoneEndpointRef(new NoneEndpointId(1)), new NoneEndpointRef(new NoneEndpointId(2)));
        var railB = MakeRail(2, new NoneEndpointRef(new NoneEndpointId(3)), new NoneEndpointRef(new NoneEndpointId(4)));
        var newId = new RailId(99);

        var merged = RailMerger.MergeAtConvergence(
            railA, RailEnd.B, railB, RailEnd.A, newId, resolvedName: "");

        Assert.Equal(newId, merged.Id);
    }

    [Fact]
    public void RoleはrailA側が採用される()
    {
        var railA = MakeRail(1, new NoneEndpointRef(new NoneEndpointId(1)), new NoneEndpointRef(new NoneEndpointId(2)), role: RailRole.Normal);
        var railB = MakeRail(2, new NoneEndpointRef(new NoneEndpointId(3)), new NoneEndpointRef(new NoneEndpointId(4)), role: RailRole.Normal);

        var merged = RailMerger.MergeAtConvergence(
            railA, RailEnd.B, railB, RailEnd.A, new RailId(99), resolvedName: "");

        Assert.Equal(RailRole.Normal, merged.Role);
    }

    [Fact]
    public void resolvedNameがそのままNameに反映される()
    {
        var railA = MakeRail(1, new NoneEndpointRef(new NoneEndpointId(1)), new NoneEndpointRef(new NoneEndpointId(2)));
        var railB = MakeRail(2, new NoneEndpointRef(new NoneEndpointId(3)), new NoneEndpointRef(new NoneEndpointId(4)));

        var merged = RailMerger.MergeAtConvergence(
            railA, RailEnd.B, railB, RailEnd.A, new RailId(99), resolvedName: "統合後の線路");

        Assert.Equal("統合後の線路", merged.Name);
    }

    // ================================
    // ResolveName
    // ================================

    [Fact]
    public void 両方空文字列なら空文字列を返す()
    {
        var result = RailMerger.ResolveName("", "");
        var resolved = Assert.IsType<MergeNameResolved>(result);
        Assert.Equal("", resolved.Name);
    }

    [Fact]
    public void Aのみ空ならBの名前が採用される()
    {
        var result = RailMerger.ResolveName("", "下り本線");
        var resolved = Assert.IsType<MergeNameResolved>(result);
        Assert.Equal("下り本線", resolved.Name);
    }

    [Fact]
    public void Bのみ空ならAの名前が採用される()
    {
        var result = RailMerger.ResolveName("上り本線", "");
        var resolved = Assert.IsType<MergeNameResolved>(result);
        Assert.Equal("上り本線", resolved.Name);
    }

    [Fact]
    public void 両方同名なら同名を返す()
    {
        var result = RailMerger.ResolveName("待避線", "待避線");
        var resolved = Assert.IsType<MergeNameResolved>(result);
        Assert.Equal("待避線", resolved.Name);
    }

    [Fact]
    public void 両方非空かつ異なる場合はConflictを返す()
    {
        var result = RailMerger.ResolveName("上り本線", "下り本線");
        var conflict = Assert.IsType<MergeNameConflict>(result);
        Assert.Equal("上り本線", conflict.NameA);
        Assert.Equal("下り本線", conflict.NameB);
    }
}
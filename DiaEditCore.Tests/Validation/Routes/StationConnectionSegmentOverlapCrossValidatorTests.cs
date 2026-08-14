namespace DiaEditCore.Tests.Serialization.Validation.Routes;

using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using DiaEditCore.Serialization.Validation;
using DiaEditCore.Serialization.Validation.Routes;
using Xunit;

public class StationConnectionSegmentOverlapCrossValidatorTests
{
    private static StationConnection MakeSc(
        int id, int mainRouteId, StationConnectionDirection direction, params int[] segIds) => new()
    {
        Id = new StationConnectionId(id),
        Name = $"SC{id}",
        MainRouteId = new MainRouteId(mainRouteId),
        Direction = direction,
        Segments = segIds.Select(s => new StationConnectionSegmentId(s)).ToList(),
    };

    private static ValidationContext MakeContext(params StationConnection[] scs) => new()
    {
        StationConnections = scs,
    };

    [Fact]
    public void Run_単純な複線区間で同一方向のSC2つが同一SCSを参照する場合_違反を検出する()
    {
        // 単純な複線区間の想定：物理的な経路は1本しかないはずなのに、
        // ユーザーが誤って同一区間を2つのSCに分割し、同じSCS(100)を両方が参照してしまったケース
        var scs = new[]
        {
            MakeSc(1, mainRouteId: 1, StationConnectionDirection.Down, 100),
            MakeSc(2, mainRouteId: 1, StationConnectionDirection.Down, 100),
        };

        var issues = StationConnectionSegmentOverlapCrossValidator.Run(MakeContext(scs));

        Assert.Single(issues);
        Assert.Contains("100", issues[0].Message);
        Assert.Contains("Down", issues[0].Message);
    }

    [Fact]
    public void Run_複々線区間で緩行急行が別SCSを参照する場合_違反を検出しない()
    {
        // 複々線の想定：緩行線(SCS=100)・急行線(SCS=200)は物理的に別のRailを通るため、
        // 別々のSCSを参照する（同一SCSを共有しない）
        var scs = new[]
        {
            MakeSc(1, mainRouteId: 1, StationConnectionDirection.Down, 100), // 緩行線
            MakeSc(2, mainRouteId: 1, StationConnectionDirection.Down, 200), // 急行線
        };

        var issues = StationConnectionSegmentOverlapCrossValidator.Run(MakeContext(scs));

        Assert.Empty(issues);
    }

    [Fact]
    public void Run_双単線区間で上りと下りが同一SCSを参照する場合_違反を検出しない()
    {
        // 双単線の想定：同一SCS(100)を上り方向SCと下り方向SCの双方が参照するが、
        // Directionが異なるため本ルールの対象外
        var scs = new[]
        {
            MakeSc(1, mainRouteId: 1, StationConnectionDirection.Down, 100),
            MakeSc(2, mainRouteId: 1, StationConnectionDirection.Up, 100),
        };

        var issues = StationConnectionSegmentOverlapCrossValidator.Run(MakeContext(scs));

        Assert.Empty(issues);
    }

    [Fact]
    public void Run_異なるMainRouteで同一SegmentIdの値が使われても別空間として扱い違反にしない()
    {
        // SegmentIdの値がたまたま同じでも、MainRouteIdが異なれば別のグルーピング対象
        // （このテストは値の偶然一致に対する型安全性の確認というより、キーの複合性を担保する回帰テスト）
        var scs = new[]
        {
            MakeSc(1, mainRouteId: 1, StationConnectionDirection.Down, 100),
            MakeSc(2, mainRouteId: 2, StationConnectionDirection.Down, 100),
        };

        var issues = StationConnectionSegmentOverlapCrossValidator.Run(MakeContext(scs));

        Assert.Empty(issues);
    }

    [Fact]
    public void Run_単一SCのみで参照が重複しない基本ケースは違反なし()
    {
        var scs = new[] { MakeSc(1, mainRouteId: 1, StationConnectionDirection.Down, 100, 101) };

        var issues = StationConnectionSegmentOverlapCrossValidator.Run(MakeContext(scs));

        Assert.Empty(issues);
    }

    [Fact]
    public void Run_同一方向で3つ以上のSCが同一SCSを参照する場合_該当ID全てをメッセージに含める()
    {
        var scs = new[]
        {
            MakeSc(1, mainRouteId: 1, StationConnectionDirection.Down, 100),
            MakeSc(2, mainRouteId: 1, StationConnectionDirection.Down, 100),
            MakeSc(3, mainRouteId: 1, StationConnectionDirection.Down, 100),
        };

        var issues = StationConnectionSegmentOverlapCrossValidator.Run(MakeContext(scs));

        Assert.Single(issues);
        Assert.Contains("1", issues[0].Message);
        Assert.Contains("2", issues[0].Message);
        Assert.Contains("3", issues[0].Message);
    }

    [Fact]
    public void Run_StationConnectionsが空なら違反なし()
    {
        var issues = StationConnectionSegmentOverlapCrossValidator.Run(MakeContext());

        Assert.Empty(issues);
    }

    [Fact]
    public void Run_検出したissueのSeverityはWarning既定値である()
    {
        var scs = new[]
        {
            MakeSc(1, mainRouteId: 1, StationConnectionDirection.Down, 100),
            MakeSc(2, mainRouteId: 1, StationConnectionDirection.Down, 100),
        };

        var issues = StationConnectionSegmentOverlapCrossValidator.Run(MakeContext(scs));

        Assert.Equal(ValidationSeverity.Warning, issues[0].Severity);
    }
}
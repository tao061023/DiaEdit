namespace DiaEditCore.Tests.Algorithm.Conflicts;

using DiaEditCore.Algorithm.Conflicts;
using DiaEditCore.Model;

using Xunit;

public class ConflictCheckerTests
{
    private static ConflictChecker.Occupancy Occ(int trainId, int start, int end) =>
        new(new TrainId(trainId), start, end);

    private static ObjectId AnyTarget() => new RailObjectId(new RailId(1));

    [Fact]
    public void CheckOverlap_空のoccupancyRanges_空を返す()
    {
        var sut = new ConflictChecker(AnyTarget(), Array.Empty<ConflictChecker.Occupancy>());

        var result = sut.CheckOverlap();

        Assert.Empty(result);
    }

    [Fact]
    public void CheckOverlap_単一要素_空を返す()
    {
        var sut = new ConflictChecker(AnyTarget(), new[] { Occ(1, 0, 100) });

        var result = sut.CheckOverlap();

        Assert.Empty(result);
    }

    [Fact]
    public void CheckOverlap_完全に分離した2区間_重複なし()
    {
        var ranges = new[] { Occ(1, 0, 100), Occ(2, 200, 300) };
        var sut = new ConflictChecker(AnyTarget(), ranges);

        var result = sut.CheckOverlap();

        Assert.Empty(result);
    }

    [Fact]
    public void CheckOverlap_境界が一致するだけの2区間_重複とみなさない()
    {
        // 設計書の擬似コード：ヒープ先頭のendSecondsが現区間のstartSeconds"以下"ならpop
        // → end==startは重複判定前にpopされるため、境界一致は非重複
        var ranges = new[] { Occ(1, 0, 100), Occ(2, 100, 200) };
        var sut = new ConflictChecker(AnyTarget(), ranges);

        var result = sut.CheckOverlap();

        Assert.Empty(result);
    }

    [Fact]
    public void CheckOverlap_単純な2区間重複_1ペア検出()
    {
        var ranges = new[] { Occ(1, 0, 100), Occ(2, 50, 150) };
        var sut = new ConflictChecker(AnyTarget(), ranges);

        var result = sut.CheckOverlap();

        var pair = Assert.Single(result);
        var ids = new[] { pair.A.Value, pair.B.Value };
        Assert.Contains(1, ids);
        Assert.Contains(2, ids);
    }

    [Fact]
    public void CheckOverlap_3区間が同時に重複_3ペア全て検出()
    {
        // train1: [0,100) train2:[10,90) train3:[20,80) 全て互いに重複
        var ranges = new[] { Occ(1, 0, 100), Occ(2, 10, 90), Occ(3, 20, 80) };
        var sut = new ConflictChecker(AnyTarget(), ranges);

        var result = sut.CheckOverlap();

        Assert.Equal(3, result.Count);
        bool HasPair(int a, int b) => result.Any(p =>
            (p.A.Value == a && p.B.Value == b) || (p.A.Value == b && p.B.Value == a));
        Assert.True(HasPair(1, 2));
        Assert.True(HasPair(1, 3));
        Assert.True(HasPair(2, 3));
    }

    [Fact]
    public void CheckOverlap_開始時刻が同値の複数区間_検出ペア集合は入力順序に依存しない()
    {
        var rangesA = new[] { Occ(1, 0, 50), Occ(2, 0, 50), Occ(3, 0, 50) };
        var rangesB = new[] { Occ(3, 0, 50), Occ(1, 0, 50), Occ(2, 0, 50) };

        var resultA = new ConflictChecker(AnyTarget(), rangesA).CheckOverlap();
        var resultB = new ConflictChecker(AnyTarget(), rangesB).CheckOverlap();

        HashSet<(int, int)> Normalize(IReadOnlyList<(TrainId A, TrainId B)> r) =>
            r.Select(p => p.A.Value < p.B.Value ? (p.A.Value, p.B.Value) : (p.B.Value, p.A.Value))
             .ToHashSet();

        Assert.Equal(Normalize(resultA), Normalize(resultB));
        Assert.Equal(3, resultA.Count); // (1,2)(1,3)(2,3)
    }

    [Fact]
    public void CheckOverlap_一方の区間がもう一方を完全に包含する_重複として検出()
    {
        var ranges = new[] { Occ(1, 0, 1000), Occ(2, 100, 200) };
        var sut = new ConflictChecker(AnyTarget(), ranges);

        var result = sut.CheckOverlap();

        Assert.Single(result);
    }

    [Fact]
    public void TargetObjectId_コンストラクタで渡した値を保持する()
    {
        var target = new SwitcherObjectId(new SwitcherId(42));
        var sut = new ConflictChecker(target, Array.Empty<ConflictChecker.Occupancy>());

        Assert.Equal(target, sut.TargetObjectId);
    }
}

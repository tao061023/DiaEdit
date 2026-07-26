using DiaEditCore.Algorithm;
using DiaEditCore.Model;
using DiaEditCore.Model.Routes;

using Xunit;

namespace DiaEditCore.Tests.Algorithm;

public class EntryPointSequenceResolverTests
{
    private static StationConnectionSegment MakeSegment(
        StationConnectionSegmentId id, StationId from, StationId to, MainRouteId mainRouteId)
        => new()
        {
            Id = id,
            FromStationId = from,
            ToStationId = to,
            FromEntryPointId = new EntryPointId(id.Value * 10 + 1),
            ToEntryPointId = new EntryPointId(id.Value * 10 + 2),
            MainRouteId = mainRouteId,
            BaseRunTimeSec = 60,
        };

    private static StationConnection MakeConnection(
        StationConnectionId id, MainRouteId mainRouteId, StationConnectionDirection direction,
        params StationConnectionSegmentId[] segmentIds)
        => new()
        {
            Id = id,
            Name = "test-sc",
            MainRouteId = mainRouteId,
            Direction = direction,
            Segments = segmentIds.ToList(),
        };

    // ---------------------------------------------------------
    // ① 正常系：単一 Segment を正しく射影する
    // ---------------------------------------------------------
    [Fact]
    public void 正常系_単一Segmentを正しく射影する()
    {
        var mainRouteId = new MainRouteId(1);
        var stA = new StationId(1);
        var stB = new StationId(2);

        var seg = MakeSegment(new StationConnectionSegmentId(1), stA, stB, mainRouteId);
        var sc = MakeConnection(new StationConnectionId(1), mainRouteId, StationConnectionDirection.Down, seg.Id);

        var result = EntryPointSequenceResolver.Resolve(sc, new List<StationConnectionSegment> { seg });

        Assert.Single(result);
        Assert.Equal(stA, result[0].FromStationId);
        Assert.Equal(stB, result[0].ToStationId);
        Assert.Equal(new EntryPointId(11), result[0].FromEntryPointId);
        Assert.Equal(new EntryPointId(12), result[0].ToEntryPointId);
    }

    // ---------------------------------------------------------
    // ② 正常系：複数 Segment を順序通りに射影する
    // ---------------------------------------------------------
    [Fact]
    public void 正常系_複数Segmentを順序通りに射影する()
    {
        var mainRouteId = new MainRouteId(1);
        var stA = new StationId(1);
        var stB = new StationId(2);
        var stC = new StationId(3);

        var seg1 = MakeSegment(new StationConnectionSegmentId(1), stA, stB, mainRouteId);
        var seg2 = MakeSegment(new StationConnectionSegmentId(2), stB, stC, mainRouteId);

        var sc = MakeConnection(new StationConnectionId(1), mainRouteId, StationConnectionDirection.Down, seg1.Id, seg2.Id);

        var result = EntryPointSequenceResolver.Resolve(sc, new List<StationConnectionSegment> { seg1, seg2 });

        Assert.Equal(2, result.Count);

        Assert.Equal(stA, result[0].FromStationId);
        Assert.Equal(stB, result[0].ToStationId);

        Assert.Equal(stB, result[1].FromStationId);
        Assert.Equal(stC, result[1].ToStationId);
    }

    // ---------------------------------------------------------
    // ③ 異常系：allSegments に存在しない SegmentId はスキップされる
    // ---------------------------------------------------------
    [Fact]
    public void 異常系_SegmentIdがallSegmentsに存在しない場合はスキップされる()
    {
        var mainRouteId = new MainRouteId(1);
        var stA = new StationId(1);
        var stB = new StationId(2);

        var seg1 = MakeSegment(new StationConnectionSegmentId(1), stA, stB, mainRouteId);
        var missingId = new StationConnectionSegmentId(99);

        var sc = MakeConnection(new StationConnectionId(1), mainRouteId, StationConnectionDirection.Down, seg1.Id, missingId);

        var result = EntryPointSequenceResolver.Resolve(sc, new List<StationConnectionSegment> { seg1 });

        Assert.Single(result);
        Assert.Equal(stA, result[0].FromStationId);
        Assert.Equal(stB, result[0].ToStationId);
    }

    // ---------------------------------------------------------
    // ④ 異常系：Segment が 0 件なら空リスト
    // ---------------------------------------------------------
    [Fact]
    public void 異常系_Segmentが0件なら空リスト()
    {
        var mainRouteId = new MainRouteId(1);

        var sc = MakeConnection(new StationConnectionId(1), mainRouteId, StationConnectionDirection.Down);

        var result = EntryPointSequenceResolver.Resolve(sc, new List<StationConnectionSegment>());

        Assert.Empty(result);
    }

    // ---------------------------------------------------------
    // ⑤ 異常系：allSegments が空なら空リスト
    // ---------------------------------------------------------
    [Fact]
    public void 異常系_allSegmentsが空なら空リスト()
    {
        var mainRouteId = new MainRouteId(1);
        var stA = new StationId(1);
        var stB = new StationId(2);

        var seg1 = MakeSegment(new StationConnectionSegmentId(1), stA, stB, mainRouteId);
        var sc = MakeConnection(new StationConnectionId(1), mainRouteId, StationConnectionDirection.Down, seg1.Id);

        var result = EntryPointSequenceResolver.Resolve(sc, new List<StationConnectionSegment>());

        Assert.Empty(result);
    }

    // ---------------------------------------------------------
    // ⑥ 正常系：SegmentId が重複している場合はそのまま複数件返る
    // ---------------------------------------------------------
    [Fact]
    public void 正常系_SegmentIdが重複している場合は複数件返る()
    {
        var mainRouteId = new MainRouteId(1);
        var stA = new StationId(1);
        var stB = new StationId(2);

        var seg1 = MakeSegment(new StationConnectionSegmentId(1), stA, stB, mainRouteId);

        var sc = MakeConnection(new StationConnectionId(1), mainRouteId, StationConnectionDirection.Down, seg1.Id, seg1.Id);

        var result = EntryPointSequenceResolver.Resolve(sc, new List<StationConnectionSegment> { seg1 });

        Assert.Equal(2, result.Count);

        Assert.Equal(stA, result[0].FromStationId);
        Assert.Equal(stB, result[0].ToStationId);

        Assert.Equal(stA, result[1].FromStationId);
        Assert.Equal(stB, result[1].ToStationId);
    }

    // ---------------------------------------------------------
    // ⑦ 正常系：EP が正しく射影されることを確認する
    // ---------------------------------------------------------
    [Fact]
    public void 正常系_EntryPointが正しく射影される()
    {
        var mainRouteId = new MainRouteId(1);
        var stA = new StationId(1);
        var stB = new StationId(2);

        var seg = MakeSegment(new StationConnectionSegmentId(3), stA, stB, mainRouteId);
        // EP は 31, 32 のはず

        var sc = MakeConnection(new StationConnectionId(1), mainRouteId, StationConnectionDirection.Down, seg.Id);

        var result = EntryPointSequenceResolver.Resolve(sc, new List<StationConnectionSegment> { seg });

        Assert.Single(result);
        Assert.Equal(new EntryPointId(31), result[0].FromEntryPointId);
        Assert.Equal(new EntryPointId(32), result[0].ToEntryPointId);
    }
}

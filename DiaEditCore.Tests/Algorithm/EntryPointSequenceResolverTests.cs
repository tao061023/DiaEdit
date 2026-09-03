namespace DiaEditCore.Tests.Algorithm;

using DiaEditCore.Algorithm;
using DiaEditCore.Model;
using DiaEditCore.Model.Routes;

using Xunit;

public class EntryPointSequenceResolverTests
{
    private static StationConnectionSegment MakeSegment(
        StationConnectionSegmentId id, StationId from, StationId to, MainRouteId mainRouteId)
        => new()
        {
            Id = id,
            StationIdA = from,
            StationIdB = to,
            EntryPointIdA = new EntryPointId(id.Value * 10 + 1),
            EntryPointIdB = new EntryPointId(id.Value * 10 + 2),
            MainRouteId = mainRouteId,
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

    // v12.29：EntryPointSequenceResolver.Resolve（系統(ii)）がMainRoute.StationOrder経由の
    // 向き解決を行うようになったため、各テストに対応するMainRouteフィクスチャが必要になった。
    // 既存テストは全てDirection=Down・StationIdA→StationIdBの並びがそのままStationOrderの
    // 並びと一致する構成のため、期待値（FromStationId等）自体に変更は生じない。
    private static MainRoute MakeMainRoute(MainRouteId id, params StationId[] stationOrder)
        => new()
        {
            Id = id,
            Name = new DisplayName { Name = "test-main-route" },
            StationOrder = stationOrder.ToList(),
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
        var mainRoute = MakeMainRoute(mainRouteId, stA, stB);

        var result = EntryPointSequenceResolver.Resolve(sc, new List<StationConnectionSegment> { seg }, new List<MainRoute> { mainRoute });

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
        var mainRoute = MakeMainRoute(mainRouteId, stA, stB, stC);

        var result = EntryPointSequenceResolver.Resolve(
            sc, new List<StationConnectionSegment> { seg1, seg2 }, new List<MainRoute> { mainRoute });

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
        var mainRoute = MakeMainRoute(mainRouteId, stA, stB);

        var result = EntryPointSequenceResolver.Resolve(
            sc, new List<StationConnectionSegment> { seg1 }, new List<MainRoute> { mainRoute });

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

        var result = EntryPointSequenceResolver.Resolve(
            sc, new List<StationConnectionSegment>(), new List<MainRoute>());

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
        var mainRoute = MakeMainRoute(mainRouteId, stA, stB);

        var result = EntryPointSequenceResolver.Resolve(
            sc, new List<StationConnectionSegment>(), new List<MainRoute> { mainRoute });

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
        var mainRoute = MakeMainRoute(mainRouteId, stA, stB);

        var result = EntryPointSequenceResolver.Resolve(
            sc, new List<StationConnectionSegment> { seg1 }, new List<MainRoute> { mainRoute });

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
        var mainRoute = MakeMainRoute(mainRouteId, stA, stB);

        var result = EntryPointSequenceResolver.Resolve(
            sc, new List<StationConnectionSegment> { seg }, new List<MainRoute> { mainRoute });

        Assert.Single(result);
        Assert.Equal(new EntryPointId(31), result[0].FromEntryPointId);
        Assert.Equal(new EntryPointId(32), result[0].ToEntryPointId);
    }

    // ---------------------------------------------------------
    // ⑧ 新規：双単線区間 - 同一SCSを上り方向SCが共有した場合、向きが正しく反転する
    // （本セッションの主目的：v12.29 SCS direction-agnostic renameの検証）
    // ---------------------------------------------------------
    [Fact]
    public void 新規_双単線_同一SegmentをUp方向SCが参照すると向きが反転して解決される()
    {
        var mainRouteId = new MainRouteId(1);
        var stA = new StationId(1);
        var stB = new StationId(2);

        // Segment自体はA=stA, B=stBのまま（Down方向で登録された想定）
        var seg = MakeSegment(new StationConnectionSegmentId(1), stA, stB, mainRouteId);
        // 同一Segmentを、Up方向のSCが共有する
        var scUp = MakeConnection(new StationConnectionId(2), mainRouteId, StationConnectionDirection.Up, seg.Id);
        var mainRoute = MakeMainRoute(mainRouteId, stA, stB);

        var result = EntryPointSequenceResolver.Resolve(
            scUp, new List<StationConnectionSegment> { seg }, new List<MainRoute> { mainRoute });

        Assert.Single(result);
        // Up方向なので、発着がstB→stAへ反転して解決されるはず
        Assert.Equal(stB, result[0].FromStationId);
        Assert.Equal(stA, result[0].ToStationId);
        Assert.Equal(new EntryPointId(12), result[0].FromEntryPointId); // EntryPointIdBが発側
        Assert.Equal(new EntryPointId(11), result[0].ToEntryPointId);  // EntryPointIdAが着側
    }

    // ---------------------------------------------------------
    // ⑨ 新規：MainRouteが見つからない場合は当該Segmentがスキップされる
    // ---------------------------------------------------------
    [Fact]
    public void 新規_MainRouteが見つからない場合はSegmentがスキップされる()
    {
        var mainRouteId = new MainRouteId(1);
        var stA = new StationId(1);
        var stB = new StationId(2);

        var seg = MakeSegment(new StationConnectionSegmentId(1), stA, stB, mainRouteId);
        var sc = MakeConnection(new StationConnectionId(1), mainRouteId, StationConnectionDirection.Down, seg.Id);

        // allMainRoutesが空＝MainRoute未検出
        var result = EntryPointSequenceResolver.Resolve(
            sc, new List<StationConnectionSegment> { seg }, new List<MainRoute>());

        Assert.Empty(result);
    }

    // ---------------------------------------------------------
    // ⑩ 新規：StationOrder上で隣接していないSegmentはスキップされる（データ不整合）
    // ---------------------------------------------------------
    [Fact]
    public void 新規_StationOrder上で隣接しないSegmentはスキップされる()
    {
        var mainRouteId = new MainRouteId(1);
        var stA = new StationId(1);
        var stB = new StationId(2);
        var stC = new StationId(3);

        // SegmentはstA-stCを繋ぐが、StationOrderではstA-stBが隣接・stCは離れている（不整合データ）
        var seg = MakeSegment(new StationConnectionSegmentId(1), stA, stC, mainRouteId);
        var sc = MakeConnection(new StationConnectionId(1), mainRouteId, StationConnectionDirection.Down, seg.Id);
        var mainRoute = MakeMainRoute(mainRouteId, stA, stB, stC);

        var result = EntryPointSequenceResolver.Resolve(
            sc, new List<StationConnectionSegment> { seg }, new List<MainRoute> { mainRoute });

        Assert.Empty(result);
    }

    // ---------------------------------------------------------
    // ⑪ 新規：ループ路線で境界をまたぐSegmentも正しく解決される
    // ---------------------------------------------------------
    [Fact]
    public void 新規_ループ路線で末尾から先頭への境界Segmentも解決される()
    {
        var mainRouteId = new MainRouteId(1);
        var stA = new StationId(1);
        var stB = new StationId(2);
        var stC = new StationId(3);

        // StationOrder=[stA, stB, stC]（環状。末尾stCの次はstAに戻る）
        var mainRoute = MakeMainRoute(mainRouteId, stA, stB, stC);
        mainRoute.IsLoop = true;

        // 境界Segment：stC→stA（Down方向で末尾から先頭へ戻る）
        var seg = MakeSegment(new StationConnectionSegmentId(1), stC, stA, mainRouteId);
        var sc = MakeConnection(new StationConnectionId(1), mainRouteId, StationConnectionDirection.Down, seg.Id);

        var result = EntryPointSequenceResolver.Resolve(
            sc, new List<StationConnectionSegment> { seg }, new List<MainRoute> { mainRoute });

        Assert.Single(result);
        Assert.Equal(stC, result[0].FromStationId);
        Assert.Equal(stA, result[0].ToStationId);
    }

    // ---------------------------------------------------------
    // ⑫ 新規：ResolveOriented（系統(i)）の無向マッチング検証
    // ---------------------------------------------------------
    [Fact]
    public void 新規_ResolveOriented_順方向は正しく解決される()
    {
        var stA = new StationId(1);
        var stB = new StationId(2);
        var seg = MakeSegment(new StationConnectionSegmentId(1), stA, stB, new MainRouteId(1));

        var result = EntryPointSequenceResolver.ResolveOriented(seg, stA, stB);

        Assert.NotNull(result);
        Assert.Equal(stA, result!.FromStationId);
        Assert.Equal(stB, result.ToStationId);
        Assert.Equal(new EntryPointId(11), result.FromEntryPointId);
        Assert.Equal(new EntryPointId(12), result.ToEntryPointId);
    }

    [Fact]
    public void 新規_ResolveOriented_逆方向指定でも向きが反転して解決される()
    {
        var stA = new StationId(1);
        var stB = new StationId(2);
        var seg = MakeSegment(new StationConnectionSegmentId(1), stA, stB, new MainRouteId(1));

        var result = EntryPointSequenceResolver.ResolveOriented(seg, stB, stA);

        Assert.NotNull(result);
        Assert.Equal(stB, result!.FromStationId);
        Assert.Equal(stA, result.ToStationId);
        Assert.Equal(new EntryPointId(12), result.FromEntryPointId);
        Assert.Equal(new EntryPointId(11), result.ToEntryPointId);
    }

    [Fact]
    public void 新規_ResolveOriented_一致しない駅ペアはnullを返す()
    {
        var stA = new StationId(1);
        var stB = new StationId(2);
        var stC = new StationId(3);
        var seg = MakeSegment(new StationConnectionSegmentId(1), stA, stB, new MainRouteId(1));

        var result = EntryPointSequenceResolver.ResolveOriented(seg, stA, stC);

        Assert.Null(result);
    }
}
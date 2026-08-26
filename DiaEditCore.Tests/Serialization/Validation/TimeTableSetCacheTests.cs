namespace DiaEditCore.Tests.Serialization.Validation;

using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using Xunit;

/// <summary>
/// TimeTableSetCache.RebuildAllが構築する軽量インデックス群の検証。
/// 既存のProjectSessionTests（Load経由の統合的な検証）とは別に、RebuildAll単体を
/// 直接呼び出す形で各Builderの配線漏れを検出できるようにする（v12.24 StationUsedBy系、
/// グラフ完成セッションのEntryPointUsedBySegmentIndex／MainRouteUsedBySegmentIndexが
/// 未作成だったため新設）。
/// </summary>
public class TimeTableSetCacheTests
{
    private static StationConnectionSegment MakeSeg(int id, int fromStation, int toStation, int fromEp, int toEp, int mainRouteId) => new()
    {
        Id = new StationConnectionSegmentId(id),
        StationIdA = new StationId(fromStation),
        StationIdB = new StationId(toStation),
        EntryPointIdA = new EntryPointId(fromEp),
        EntryPointIdB = new EntryPointId(toEp),
        MainRouteId = new MainRouteId(mainRouteId),
    };

    [Fact]
    public void RebuildAll_孤立SegmentからEntryPointUsedBySegmentIndexが構築される()
    {
        // どのStationConnectionにも属さない（Segmentsに含まれない）孤立したSegmentを渡す。
        var seg = MakeSeg(id: 50, fromStation: 1, toStation: 2, fromEp: 10, toEp: 20, mainRouteId: 100);
        var cache = new TimeTableSetCache();

        cache.RebuildAll(
            trains: [],
            stationConnections: [], // 孤立＝どのSCからも参照されない
            segments: [seg],
            restrictions: [],
            mainRoutes: [],
            serviceRoutes: []);

        Assert.True(cache.EntryPointUsedBySegmentIndex.TryGetValue(new EntryPointId(10), out var fromList));
        Assert.Contains(new StationConnectionSegmentId(50), fromList!);

        Assert.True(cache.EntryPointUsedBySegmentIndex.TryGetValue(new EntryPointId(20), out var toList));
        Assert.Contains(new StationConnectionSegmentId(50), toList!);
    }

    [Fact]
    public void RebuildAll_孤立SegmentからMainRouteUsedBySegmentIndexが構築される()
    {
        var seg = MakeSeg(id: 50, fromStation: 1, toStation: 2, fromEp: 10, toEp: 20, mainRouteId: 100);
        var cache = new TimeTableSetCache();

        cache.RebuildAll(
            trains: [],
            stationConnections: [],
            segments: [seg],
            restrictions: [],
            mainRoutes: [],
            serviceRoutes: []);

        Assert.True(cache.MainRouteUsedBySegmentIndex.TryGetValue(new MainRouteId(100), out var list));
        Assert.Contains(new StationConnectionSegmentId(50), list!);
    }

    [Fact]
    public void RebuildAllを複数回呼んでも古いEntryPointUsedBySegmentIndexの内容が残留しない()
    {
        // Clear()漏れの回帰防止（v12.20のServiceRoutesByMainRouteIndex Clear漏れと同種の不具合を
        // 別インデックスでも作り込んでいないことを確認する）。
        var segA = MakeSeg(id: 50, fromStation: 1, toStation: 2, fromEp: 10, toEp: 20, mainRouteId: 100);
        var segB = MakeSeg(id: 51, fromStation: 3, toStation: 4, fromEp: 30, toEp: 40, mainRouteId: 200);
        var cache = new TimeTableSetCache();

        cache.RebuildAll(trains: [], stationConnections: [], segments: [segA], restrictions: [], mainRoutes: [], serviceRoutes: []);
        Assert.True(cache.EntryPointUsedBySegmentIndex.ContainsKey(new EntryPointId(10)));

        // 2回目：segAを含まない新しい入力で再構築
        cache.RebuildAll(trains: [], stationConnections: [], segments: [segB], restrictions: [], mainRoutes: [], serviceRoutes: []);

        Assert.False(cache.EntryPointUsedBySegmentIndex.ContainsKey(new EntryPointId(10)));
        Assert.True(cache.EntryPointUsedBySegmentIndex.ContainsKey(new EntryPointId(30)));
    }
}
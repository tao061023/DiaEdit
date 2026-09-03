using DiaEditCore.Model;
using DiaEditCore.Model.TimeTable.Trains;

using Xunit;

namespace DiaEditCore.Tests.Model.TimeTable.Trains;

public sealed class StopKeySequenceBuilderTests
{
    private static Train CreateTrain(List<TrainRunSegment> runSegments) => new()
    {
        Id = new TrainId(1),
        TimeTableSetId = new TimeTableSetId(1),
        TrainNumber = "1234M",
        ServiceRouteId = new ServiceRouteId(1),
        TrainTypeId = new TrainTypeId(1),
        TrainTypeName = new DisplayName { Name = "普通" },
        Nickname = new DisplayName { Name = "" },
        RunSegments = runSegments
    };

    private static TrainRunSegment Segment(StationId from, StationId to) => new()
    {
        FromStationId = from,
        ToStationId = to,
        StationConnectionId = new StationConnectionId(1)
    };

    // ---------------------------
    // 1. RunSegmentsが空 → 空リスト
    // ---------------------------
    [Fact]
    public void BuildVisitedStopKeys_EmptyRunSegments_ReturnsEmpty()
    {
        var train = CreateTrain(new List<TrainRunSegment>());

        var keys = StopKeySequenceBuilder.BuildVisitedStopKeys(train);

        Assert.Empty(keys);
    }

    // ---------------------------
    // 2. 単純経路（全駅が異なる）→ 全てVisitCount=0、訪問順に並ぶ
    // ---------------------------
    [Fact]
    public void BuildVisitedStopKeys_AllDistinctStations_AllVisitCountsAreZero()
    {
        var stationA = new StationId(1);
        var stationB = new StationId(2);
        var stationC = new StationId(3);

        var train = CreateTrain(new List<TrainRunSegment>
        {
            Segment(stationA, stationB),
            Segment(stationB, stationC)
        });

        var keys = StopKeySequenceBuilder.BuildVisitedStopKeys(train);

        Assert.Equal(new[]
        {
            new StopKey(stationA, 0),
            new StopKey(stationB, 0),
            new StopKey(stationC, 0)
        }, keys);
    }

    // ---------------------------
    // 3. 同一駅への複数回訪問（環状線・デルタ線折返し等）
    //    → 駅ごとにローカルなVisitCountが0-indexedで増加する
    // ---------------------------
    [Fact]
    public void BuildVisitedStopKeys_RevisitsSameStation_LocalVisitCountIncrements()
    {
        var stationA = new StationId(1);
        var stationB = new StationId(2);

        // A -> B -> A（B始発の折返し等を想定した2ホップ経路）
        var train = CreateTrain(new List<TrainRunSegment>
        {
            Segment(stationA, stationB),
            Segment(stationB, stationA)
        });

        var keys = StopKeySequenceBuilder.BuildVisitedStopKeys(train);

        Assert.Equal(new[]
        {
            new StopKey(stationA, 0),
            new StopKey(stationB, 0),
            new StopKey(stationA, 1)
        }, keys);
    }

    // ---------------------------
    // 4. 同一駅への3回以上の訪問
    //    → VisitCountは他駅への訪問回数と無関係に、当該駅だけのローカルカウンタとして増加する
    // ---------------------------
    [Fact]
    public void BuildVisitedStopKeys_MultipleRevisits_LocalVisitCountIsIndependentPerStation()
    {
        var stationA = new StationId(1);
        var stationB = new StationId(2);
        var stationC = new StationId(3);

        // A -> B -> A -> C -> A（Aに計3回訪問、Bには1回のみ訪問）
        var train = CreateTrain(new List<TrainRunSegment>
        {
            Segment(stationA, stationB),
            Segment(stationB, stationA),
            Segment(stationA, stationC),
            Segment(stationC, stationA)
        });

        var keys = StopKeySequenceBuilder.BuildVisitedStopKeys(train);

        Assert.Equal(new[]
        {
            new StopKey(stationA, 0),
            new StopKey(stationB, 0),
            new StopKey(stationA, 1),
            new StopKey(stationC, 0),
            new StopKey(stationA, 2)
        }, keys);
    }

    // ---------------------------
    // 5. 単一RunSegment（最短経路）→ 始発・終着の2キーのみ
    // ---------------------------
    [Fact]
    public void BuildVisitedStopKeys_SingleRunSegment_ReturnsTwoKeys()
    {
        var stationA = new StationId(1);
        var stationB = new StationId(2);

        var train = CreateTrain(new List<TrainRunSegment>
        {
            Segment(stationA, stationB)
        });

        var keys = StopKeySequenceBuilder.BuildVisitedStopKeys(train);

        Assert.Equal(new[]
        {
            new StopKey(stationA, 0),
            new StopKey(stationB, 0)
        }, keys);
    }

    // ---------------------------
    // 6. 返却順序が訪問順（RunSegments順）と一致することの明示的検証
    //    （TrainConnectionResolver.StopKeyAt等がインデックスアクセスに依存しているため重要）
    // ---------------------------
    [Fact]
    public void BuildVisitedStopKeys_ReturnsKeysInVisitOrder_NotInsertionOrderOfDictionary()
    {
        var stationA = new StationId(1);
        var stationB = new StationId(2);
        var stationC = new StationId(3);

        var train = CreateTrain(new List<TrainRunSegment>
        {
            Segment(stationC, stationA),
            Segment(stationA, stationB)
        });

        var keys = StopKeySequenceBuilder.BuildVisitedStopKeys(train);

        Assert.Equal(3, keys.Count);
        Assert.Equal(stationC, keys[0].StationId);
        Assert.Equal(stationA, keys[1].StationId);
        Assert.Equal(stationB, keys[2].StationId);
    }
}
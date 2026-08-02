using DiaEditCore.Model;
using DiaEditCore.Model.Cars;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.TimeTable;

using DiaEditCore.Algorithm;

using Xunit;

namespace DiaEditCore.Tests.Algorithm;

public class CarConsistResolverTests
{
    // -----------------------------
    // ヘルパー
    // -----------------------------

    private static Train NewTrain(int id, string trainNumber = "1000M") => new()
    {
        Id = new TrainId(id),
        TrainNumber = trainNumber,
        ServiceRouteId = new ServiceRouteId(1),
        TrainTypeId = new TrainTypeId(1),
        TrainTypeName = new DisplayName { Name = "普通" },
        Nickname = new DisplayName { Name = "" },
        DefaultVehicleTypeId = new VehicleTypeId(1),
    };

    private static void AddRunSegment(Train train, int fromStationId, int toStationId)
        => train.RunSegments.Add(new TrainRunSegment
        {
            FromStationId = new StationId(fromStationId),
            ToStationId = new StationId(toStationId),
            StationConnectionId = new StationConnectionId(1),
        });

    // CarConsist：編成の"型"（ひな形）。Name/Identifierは持たない（v11.33でCarCompositionへ移動）。
    private static CarConsist MakeConsist(int id, params int[] carIds)
    {
        var cars = carIds.Select((carId, i) => new CarRef { CarId = new CarId(carId), Position = i }).ToList();
        return new CarConsist
        {
            Id = new CarConsistId(id),
            VehicleTypeId = new VehicleTypeId(1),
            Type = CarConsistType.Basic,
            Cars = cars,
        };
    }

    // CarComposition：実運用編成の実体。ここではテストの見通しを保つため、
    // compositionIdとconsistIdを同一の数値で対応付ける（1:1マッピング）。
    private static CarComposition MakeComposition(int id, int carConsistId)
        => new()
        {
            Id = new CarCompositionId(id),
            Name = $"composition{id}",
            Identifier = id,
            CarConsistId = new CarConsistId(carConsistId),
        };

    private static Dictionary<CarConsistId, CarConsist> MakeConsistDict(params CarConsist[] consists)
        => consists.ToDictionary(c => c.Id);

    private static Dictionary<CarCompositionId, CarComposition> MakeCompositionDict(params CarComposition[] compositions)
        => compositions.ToDictionary(c => c.Id);

    private static StartOpCarSlot Slot(int position, int carCompositionId)
        => new() { Position = position, CarCompositionId = new CarCompositionId(carCompositionId) };

    private static TrainCutPoint CutPoint(int trainId, int position, int carCompositionId)
        => new() { TrainId = new TrainId(trainId), Position = position, CarCompositionId = new CarCompositionId(carCompositionId) };

    // -----------------------------
    // テスト
    // -----------------------------

    [Fact]
    public void StartOpのみならStartOpConsistがそのまま返る()
    {
        var train = NewTrain(1);
        AddRunSegment(train, 1, 2);
        train.StopTimes[new StopKey(new StationId(1), 0)] = new StopTime
        {
            Works = [new StationWork
            {
                Type = StationWorkType.StartOp,
                StartOpConsist = [Slot(0, 10), Slot(1, 20)],
            }],
        };
        var consists = MakeConsistDict(MakeConsist(10, 1, 2), MakeConsist(20, 3));
        var compositions = MakeCompositionDict(MakeComposition(10, 10), MakeComposition(20, 20));

        var result = CarConsistResolver.ResolveConsistAt(train, new StopKey(new StationId(2), 0), consists, compositions);

        Assert.Equal(new[] { new CarCompositionId(10), new CarCompositionId(20) }, result.ConsistBlocks);
        Assert.Equal(3, result.Cars.Count);
    }

    [Fact]
    public void StartOpが無ければ空を返す()
    {
        var train = NewTrain(1);
        AddRunSegment(train, 1, 2);
        var consists = MakeConsistDict();
        var compositions = MakeCompositionDict();

        var result = CarConsistResolver.ResolveConsistAt(train, new StopKey(new StationId(2), 0), consists, compositions);

        Assert.Empty(result.ConsistBlocks);
        Assert.Empty(result.Cars);
    }

    [Fact]
    public void 対象StopKeyがStartOpより前なら空を返す()
    {
        var train = NewTrain(1);
        AddRunSegment(train, 1, 2);
        AddRunSegment(train, 2, 3);
        train.StopTimes[new StopKey(new StationId(2), 0)] = new StopTime
        {
            Works = [new StationWork { Type = StationWorkType.StartOp, StartOpConsist = [Slot(0, 10)] }],
        };
        var consists = MakeConsistDict(MakeConsist(10, 1));
        var compositions = MakeCompositionDict(MakeComposition(10, 10));

        var result = CarConsistResolver.ResolveConsistAt(train, new StopKey(new StationId(1), 0), consists, compositions);

        Assert.Empty(result.ConsistBlocks);
    }

    [Fact]
    public void Decoupling後は自Trainに残る側のCutPointsのみに置き換わる()
    {
        var train = NewTrain(1);
        AddRunSegment(train, 1, 2);
        AddRunSegment(train, 2, 3);
        train.StopTimes[new StopKey(new StationId(1), 0)] = new StopTime
        {
            Works = [new StationWork { Type = StationWorkType.StartOp, StartOpConsist = [Slot(0, 10), Slot(1, 20)] }],
        };
        train.StopTimes[new StopKey(new StationId(2), 0)] = new StopTime
        {
            Works = [new StationWork
            {
                Type = StationWorkType.Decoupling,
                CutPoints = [CutPoint(1, 0, 10), CutPoint(2, 0, 20)], // TrainId=2側は別Trainへ切り出し
            }],
        };
        var consists = MakeConsistDict(MakeConsist(10, 1), MakeConsist(20, 2));
        var compositions = MakeCompositionDict(MakeComposition(10, 10), MakeComposition(20, 20));

        var beforeDecoupling = CarConsistResolver.ResolveConsistAt(train, new StopKey(new StationId(1), 0), consists, compositions);
        var afterDecoupling = CarConsistResolver.ResolveConsistAt(train, new StopKey(new StationId(3), 0), consists, compositions);

        Assert.Equal(new[] { new CarCompositionId(10), new CarCompositionId(20) }, beforeDecoupling.ConsistBlocks);
        Assert.Equal(new[] { new CarCompositionId(10) }, afterDecoupling.ConsistBlocks);
    }

    [Fact]
    public void Coupling後はTrainIdを問わずCutPoints全件がPosition順に連結される()
    {
        var train = NewTrain(1);
        AddRunSegment(train, 1, 2);
        AddRunSegment(train, 2, 3);
        train.StopTimes[new StopKey(new StationId(1), 0)] = new StopTime
        {
            Works = [new StationWork { Type = StationWorkType.StartOp, StartOpConsist = [Slot(0, 10)] }],
        };
        train.StopTimes[new StopKey(new StationId(2), 0)] = new StopTime
        {
            Works = [new StationWork
            {
                Type = StationWorkType.Coupling,
                CutPoints = [CutPoint(2, 0, 20), CutPoint(1, 1, 10)], // 他Train由来(20)が0番、自Train(10)が1番
            }],
        };
        var consists = MakeConsistDict(MakeConsist(10, 1), MakeConsist(20, 2));
        var compositions = MakeCompositionDict(MakeComposition(10, 10), MakeComposition(20, 20));

        var result = CarConsistResolver.ResolveConsistAt(train, new StopKey(new StationId(3), 0), consists, compositions);

        Assert.Equal(new[] { new CarCompositionId(20), new CarCompositionId(10) }, result.ConsistBlocks);
    }

    [Fact]
    public void 同一駅を複数回訪問するループ線でVisitSequenceにより区別される()
    {
        var train = NewTrain(1);
        AddRunSegment(train, 1, 2);
        AddRunSegment(train, 2, 1); // 駅1を再訪（ループ）
        AddRunSegment(train, 1, 3);

        train.StopTimes[new StopKey(new StationId(1), 0)] = new StopTime
        {
            Works = [new StationWork { Type = StationWorkType.StartOp, StartOpConsist = [Slot(0, 10)] }],
        };
        train.StopTimes[new StopKey(new StationId(1), 1)] = new StopTime // 2回目の駅1でDecoupling
        {
            Works = [new StationWork
            {
                Type = StationWorkType.Decoupling,
                CutPoints = [CutPoint(1, 0, 30)],
            }],
        };
        var consists = MakeConsistDict(MakeConsist(10, 1), MakeConsist(30, 2));
        var compositions = MakeCompositionDict(MakeComposition(10, 10), MakeComposition(30, 30));

        var atFirstVisit = CarConsistResolver.ResolveConsistAt(train, new StopKey(new StationId(2), 0), consists, compositions);
        var atThirdStation = CarConsistResolver.ResolveConsistAt(train, new StopKey(new StationId(3), 0), consists, compositions);

        Assert.Equal(new[] { new CarCompositionId(10) }, atFirstVisit.ConsistBlocks);
        Assert.Equal(new[] { new CarCompositionId(30) }, atThirdStation.ConsistBlocks);
    }

    [Fact]
    public void Carsは各ConsistBlockのCarsをブロック順に連結したものになる()
    {
        var train = NewTrain(1);
        AddRunSegment(train, 1, 2);
        train.StopTimes[new StopKey(new StationId(1), 0)] = new StopTime
        {
            Works = [new StationWork { Type = StationWorkType.StartOp, StartOpConsist = [Slot(0, 10), Slot(1, 20)] }],
        };
        var consists = MakeConsistDict(MakeConsist(10, 1, 2), MakeConsist(20, 3, 4));
        var compositions = MakeCompositionDict(MakeComposition(10, 10), MakeComposition(20, 20));

        var result = CarConsistResolver.ResolveConsistAt(train, new StopKey(new StationId(2), 0), consists, compositions);

        Assert.Equal(new[] { new CarId(1), new CarId(2), new CarId(3), new CarId(4) }, result.Cars.Select(c => c.CarId));
    }

    [Fact]
    public void 対象StopKeyがRunSegmentsに含まれる訪問駅列に存在しなければ空を返す()
    {
        var train = NewTrain(1);
        AddRunSegment(train, 1, 2);
        train.StopTimes[new StopKey(new StationId(1), 0)] = new StopTime
        {
            Works = [new StationWork { Type = StationWorkType.StartOp, StartOpConsist = [Slot(0, 10)] }],
        };
        var consists = MakeConsistDict(MakeConsist(10, 1));
        var compositions = MakeCompositionDict(MakeComposition(10, 10));

        var result = CarConsistResolver.ResolveConsistAt(train, new StopKey(new StationId(999), 0), consists, compositions);

        Assert.Empty(result.ConsistBlocks);
    }

    [Fact]
    public void 複数のCompositionが同じConsist_型_を共有していてもそれぞれ正しく展開される()
    {
        // CarComposition"トウ01"と"トウ02"が同じCarConsist(型)を共有するケース。
        // v11.33で追加された多対1関係（CarComposition N : CarConsist 1）を明示的に検証する。
        var train = NewTrain(1);
        AddRunSegment(train, 1, 2);
        train.StopTimes[new StopKey(new StationId(1), 0)] = new StopTime
        {
            Works = [new StationWork { Type = StationWorkType.StartOp, StartOpConsist = [Slot(0, 101)] }],
        };
        var sharedConsist = MakeConsist(10, 1, 2); // 型は1つ
        var consists = MakeConsistDict(sharedConsist);
        // composition101と102はどちらもconsist(10)を参照する（102は本テストでは未使用だが多対1を明示する目的で残す）
        var compositions = MakeCompositionDict(MakeComposition(101, 10), MakeComposition(102, 10));

        var result = CarConsistResolver.ResolveConsistAt(train, new StopKey(new StationId(2), 0), consists, compositions);

        Assert.Equal(new[] { new CarCompositionId(101) }, result.ConsistBlocks);
        Assert.Equal(new[] { new CarId(1), new CarId(2) }, result.Cars.Select(c => c.CarId));
    }
}
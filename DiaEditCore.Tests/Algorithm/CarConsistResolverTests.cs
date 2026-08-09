// CarConsistResolverTests.cs
using DiaEditCore.Model;
using DiaEditCore.Model.Cars;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.TimeTable.Trains;

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
        TimeTableSetId = new TimeTableSetId(1),
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

    // テストでは運用IDの実在チェックはCarConsistResolverの対象外（Validator層の責務）なので、
    // 適当なResolvedOperationRefを都度払い出すだけでよい。
    private static OperationRef Op(int id) => new ResolvedOperationRef(new TrainOperationId(id));

    private static StartOpCarSlot Slot(int position, int carCompositionId, OperationRef? operationId = null)
        => new() { Position = position, CarCompositionId = new CarCompositionId(carCompositionId), OperationId = operationId ?? Op(carCompositionId) };

    // vNEXT：GroupIndexを持たないCutGroupEntry
    private static CutGroupEntry Entry(int carCompositionId, OperationRef? operationId = null)
        => new() { CarCompositionId = new CarCompositionId(carCompositionId), OperationId = operationId ?? Op(carCompositionId) };

    private static ConsistResolutionContext SimpleContext(
        Dictionary<CarConsistId, CarConsist> consists,
        Dictionary<CarCompositionId, CarComposition> compositions)
        => ConsistResolutionContext.Empty(consists, compositions);

    // -----------------------------
    // テスト（StartOp単体、SplitOrigin/Decoupling/Couplingを使わないもの）
    // -----------------------------

    [Fact]
    public void StartOpのみならStartOpConsistがそのまま返る()
    {
        var train = NewTrain(1);
        AddRunSegment(train, 1, 2);
        train.StopTimesInternal[new StopKey(new StationId(1), 0)] = new StopTime
        {
            Works = [new StationWork
            {
                Type = StationWorkType.StartOp,
                StartOpConsist = [Slot(0, 10), Slot(1, 20)],
            }],
        };
        var consists = MakeConsistDict(MakeConsist(10, 1, 2), MakeConsist(20, 3));
        var compositions = MakeCompositionDict(MakeComposition(10, 10), MakeComposition(20, 20));

        var result = CarConsistResolver.ResolveConsistAt(train, new StopKey(new StationId(2), 0), SimpleContext(consists, compositions));

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

        var result = CarConsistResolver.ResolveConsistAt(train, new StopKey(new StationId(2), 0), SimpleContext(consists, compositions));

        Assert.Empty(result.ConsistBlocks);
        Assert.Empty(result.Cars);
    }

    [Fact]
    public void 対象StopKeyがStartOpより前なら空を返す()
    {
        var train = NewTrain(1);
        AddRunSegment(train, 1, 2);
        AddRunSegment(train, 2, 3);
        train.StopTimesInternal[new StopKey(new StationId(2), 0)] = new StopTime
        {
            Works = [new StationWork { Type = StationWorkType.StartOp, StartOpConsist = [Slot(0, 10)] }],
        };
        var consists = MakeConsistDict(MakeConsist(10, 1));
        var compositions = MakeCompositionDict(MakeComposition(10, 10));

        var result = CarConsistResolver.ResolveConsistAt(train, new StopKey(new StationId(1), 0), SimpleContext(consists, compositions));

        Assert.Empty(result.ConsistBlocks);
    }

    [Fact]
    public void Carsは各ConsistBlockのCarsをブロック順に連結したものになる()
    {
        var train = NewTrain(1);
        AddRunSegment(train, 1, 2);
        train.StopTimesInternal[new StopKey(new StationId(1), 0)] = new StopTime
        {
            Works = [new StationWork { Type = StationWorkType.StartOp, StartOpConsist = [Slot(0, 10), Slot(1, 20)] }],
        };
        var consists = MakeConsistDict(MakeConsist(10, 1, 2), MakeConsist(20, 3, 4));
        var compositions = MakeCompositionDict(MakeComposition(10, 10), MakeComposition(20, 20));

        var result = CarConsistResolver.ResolveConsistAt(train, new StopKey(new StationId(2), 0), SimpleContext(consists, compositions));

        Assert.Equal(new[] { new CarId(1), new CarId(2), new CarId(3), new CarId(4) }, result.Cars.Select(c => c.CarId));
    }

    [Fact]
    public void 対象StopKeyがRunSegmentsに含まれる訪問駅列に存在しなければ空を返す()
    {
        var train = NewTrain(1);
        AddRunSegment(train, 1, 2);
        train.StopTimesInternal[new StopKey(new StationId(1), 0)] = new StopTime
        {
            Works = [new StationWork { Type = StationWorkType.StartOp, StartOpConsist = [Slot(0, 10)] }],
        };
        var consists = MakeConsistDict(MakeConsist(10, 1));
        var compositions = MakeCompositionDict(MakeComposition(10, 10));

        var result = CarConsistResolver.ResolveConsistAt(train, new StopKey(new StationId(999), 0), SimpleContext(consists, compositions));

        Assert.Empty(result.ConsistBlocks);
    }

    [Fact]
    public void 複数のCompositionが同じConsist_型_を共有していてもそれぞれ正しく展開される()
    {
        var train = NewTrain(1);
        AddRunSegment(train, 1, 2);
        train.StopTimesInternal[new StopKey(new StationId(1), 0)] = new StopTime
        {
            Works = [new StationWork { Type = StationWorkType.StartOp, StartOpConsist = [Slot(0, 101)] }],
        };
        var sharedConsist = MakeConsist(10, 1, 2);
        var consists = MakeConsistDict(sharedConsist);
        var compositions = MakeCompositionDict(MakeComposition(101, 10), MakeComposition(102, 10));

        var result = CarConsistResolver.ResolveConsistAt(train, new StopKey(new StationId(2), 0), SimpleContext(consists, compositions));

        Assert.Equal(new[] { new CarCompositionId(101) }, result.ConsistBlocks);
        Assert.Equal(new[] { new CarId(1), new CarId(2) }, result.Cars.Select(c => c.CarId));
    }

    [Fact]
    public void 同一駅を複数回訪問するループ線でVisitSequenceにより区別される()
    {
        // vNEXT：Coupling は CutGroups ではなく PartnerTrainId 参照方式のため、
        // 相手Train(partner)を別途用意し、Coupling時点でその中身を再帰的に解決させる。
        var train = NewTrain(1);
        AddRunSegment(train, 1, 2);
        AddRunSegment(train, 2, 1); // 駅1を再訪（ループ）
        AddRunSegment(train, 1, 3);

        train.StopTimesInternal[new StopKey(new StationId(1), 0)] = new StopTime
        {
            Works = [new StationWork { Type = StationWorkType.StartOp, StartOpConsist = [Slot(0, 10)] }],
        };

        var partner = NewTrain(2, "9000M");
        AddRunSegment(partner, 5, 1);
        var partnerStopKey = new StopKey(new StationId(1), 0);
        partner.StopTimesInternal[new StopKey(new StationId(5), 0)] = new StopTime
        {
            Works = [new StationWork { Type = StationWorkType.StartOp, StartOpConsist = [Slot(0, 30)] }],
        };
        partner.StopTimesInternal[partnerStopKey] = new StopTime();

        // 2回目の駅1でCoupling（相手Train全体＝Composition30を自編成の後ろに連結）
        train.StopTimesInternal[new StopKey(new StationId(1), 1)] = new StopTime
        {
            Works = [new StationWork
            {
                Type = StationWorkType.Coupling,
                CouplingDetail = new CouplingWork { PartnerTrainId = partner.Id, PartnerStopKey = partnerStopKey, AttachToFront = false },
            }],
        };
        var consists = MakeConsistDict(MakeConsist(10, 1), MakeConsist(30, 2));
        var compositions = MakeCompositionDict(MakeComposition(10, 10), MakeComposition(30, 30));
        var allTrainsById = new Dictionary<TrainId, Train> { [train.Id] = train, [partner.Id] = partner };
        var context = new ConsistResolutionContext(consists, compositions, allTrainsById);

        var atFirstVisit = CarConsistResolver.ResolveConsistAt(train, new StopKey(new StationId(2), 0), context);
        var atThirdStation = CarConsistResolver.ResolveConsistAt(train, new StopKey(new StationId(3), 0), context);

        Assert.Equal(new[] { new CarCompositionId(10) }, atFirstVisit.ConsistBlocks);
        Assert.Equal(new[] { new CarCompositionId(10), new CarCompositionId(30) }, atThirdStation.ConsistBlocks);
    }

    // -----------------------------
    // テスト（Decoupling/Coupling：vNEXT、front/rear + IsRearBase方式）
    // -----------------------------

    [Fact]
    public void Decoupling後は自Trainの継続側グループのみに置き換わる_front継続()
    {
        // IsRearBase=false（front側=自Train自身がそのまま継続）。
        // train自身のStopTimesにDecouplingが記録されている以上、trainは常に継続側なので、
        // SplitGroupAssignments相当の全Train横断計算は不要になった。
        var train = NewTrain(1);
        AddRunSegment(train, 1, 2);
        AddRunSegment(train, 2, 3);
        train.StopTimesInternal[new StopKey(new StationId(1), 0)] = new StopTime
        {
            Works = [new StationWork { Type = StationWorkType.StartOp, StartOpConsist = [Slot(0, 10), Slot(1, 20)] }],
        };
        train.StopTimesInternal[new StopKey(new StationId(2), 0)] = new StopTime
        {
            Works = [new StationWork
            {
                Type = StationWorkType.Decoupling,
                DecouplingDetail = new DecouplingWork
                {
                    FrontGroup = [Entry(10)],
                    RearGroup = [Entry(20)],
                    IsRearBase = false,
                },
            }],
        };
        var consists = MakeConsistDict(MakeConsist(10, 1), MakeConsist(20, 2));
        var compositions = MakeCompositionDict(MakeComposition(10, 10), MakeComposition(20, 20));
        var context = ConsistResolutionContext.Empty(consists, compositions);

        var beforeDecoupling = CarConsistResolver.ResolveConsistAt(train, new StopKey(new StationId(1), 0), context);
        var afterDecoupling = CarConsistResolver.ResolveConsistAt(train, new StopKey(new StationId(3), 0), context);

        Assert.Equal(new[] { new CarCompositionId(10), new CarCompositionId(20) }, beforeDecoupling.ConsistBlocks);
        Assert.Equal(new[] { new CarCompositionId(10) }, afterDecoupling.ConsistBlocks);
    }

    [Fact]
    public void Decoupling後は自Trainの継続側グループのみに置き換わる_rear継続()
    {
        // IsRearBase=true（rear側が継続）のパターンも取り違えがないことを確認する。
        var train = NewTrain(1);
        AddRunSegment(train, 1, 2);
        AddRunSegment(train, 2, 3);
        train.StopTimesInternal[new StopKey(new StationId(1), 0)] = new StopTime
        {
            Works = [new StationWork { Type = StationWorkType.StartOp, StartOpConsist = [Slot(0, 10), Slot(1, 20)] }],
        };
        train.StopTimesInternal[new StopKey(new StationId(2), 0)] = new StopTime
        {
            Works = [new StationWork
            {
                Type = StationWorkType.Decoupling,
                DecouplingDetail = new DecouplingWork
                {
                    FrontGroup = [Entry(10)],
                    RearGroup = [Entry(20)],
                    IsRearBase = true,
                },
            }],
        };
        var consists = MakeConsistDict(MakeConsist(10, 1), MakeConsist(20, 2));
        var compositions = MakeCompositionDict(MakeComposition(10, 10), MakeComposition(20, 20));
        var context = ConsistResolutionContext.Empty(consists, compositions);

        var afterDecoupling = CarConsistResolver.ResolveConsistAt(train, new StopKey(new StationId(3), 0), context);

        Assert.Equal(new[] { new CarCompositionId(20) }, afterDecoupling.ConsistBlocks);
    }

    [Fact]
    public void Decoupling_DecouplingDetailが未設定なら分割前の編成のまま変化しない()
    {
        // データ不整合ケース（本来Validatorで弾かれるべきだが、Resolver単体としては
        // 例外を投げず「直前の状態を維持する」という構造的に安全な挙動を固定しておく）。
        var train = NewTrain(1);
        AddRunSegment(train, 1, 2);
        AddRunSegment(train, 2, 3);
        train.StopTimesInternal[new StopKey(new StationId(1), 0)] = new StopTime
        {
            Works = [new StationWork { Type = StationWorkType.StartOp, StartOpConsist = [Slot(0, 10)] }],
        };
        train.StopTimesInternal[new StopKey(new StationId(2), 0)] = new StopTime
        {
            Works = [new StationWork { Type = StationWorkType.Decoupling, DecouplingDetail = null }],
        };
        var consists = MakeConsistDict(MakeConsist(10, 1));
        var compositions = MakeCompositionDict(MakeComposition(10, 10));
        var context = ConsistResolutionContext.Empty(consists, compositions);

        var afterDecoupling = CarConsistResolver.ResolveConsistAt(train, new StopKey(new StationId(3), 0), context);

        Assert.Equal(new[] { new CarCompositionId(10) }, afterDecoupling.ConsistBlocks);
    }

    [Fact]
    public void Coupling後はPartnerTrainの編成がAttachToFront_falseなら自編成の後ろに連結される()
    {
        var train = NewTrain(1);
        AddRunSegment(train, 1, 2);
        AddRunSegment(train, 2, 3);
        train.StopTimesInternal[new StopKey(new StationId(1), 0)] = new StopTime
        {
            Works = [new StationWork { Type = StationWorkType.StartOp, StartOpConsist = [Slot(0, 10)] }],
        };

        var partner = NewTrain(2, "9000M");
        AddRunSegment(partner, 5, 2);
        var partnerStopKey = new StopKey(new StationId(2), 0);
        partner.StopTimesInternal[new StopKey(new StationId(5), 0)] = new StopTime
        {
            Works = [new StationWork { Type = StationWorkType.StartOp, StartOpConsist = [Slot(0, 20)] }],
        };
        partner.StopTimesInternal[partnerStopKey] = new StopTime();

        train.StopTimesInternal[new StopKey(new StationId(2), 0)] = new StopTime
        {
            Works = [new StationWork
            {
                Type = StationWorkType.Coupling,
                CouplingDetail = new CouplingWork { PartnerTrainId = partner.Id, PartnerStopKey = partnerStopKey, AttachToFront = false },
            }],
        };

        var consists = MakeConsistDict(MakeConsist(10, 1), MakeConsist(20, 2));
        var compositions = MakeCompositionDict(MakeComposition(10, 10), MakeComposition(20, 20));
        var allTrainsById = new Dictionary<TrainId, Train> { [train.Id] = train, [partner.Id] = partner };
        var context = new ConsistResolutionContext(consists, compositions, allTrainsById);

        var result = CarConsistResolver.ResolveConsistAt(train, new StopKey(new StationId(3), 0), context);

        Assert.Equal(new[] { new CarCompositionId(10), new CarCompositionId(20) }, result.ConsistBlocks);
    }

    [Fact]
    public void Coupling後はPartnerTrainの編成がAttachToFront_trueなら自編成の前に連結される()
    {
        var train = NewTrain(1);
        AddRunSegment(train, 1, 2);
        AddRunSegment(train, 2, 3);
        train.StopTimesInternal[new StopKey(new StationId(1), 0)] = new StopTime
        {
            Works = [new StationWork { Type = StationWorkType.StartOp, StartOpConsist = [Slot(0, 10)] }],
        };

        var partner = NewTrain(2, "9000M");
        AddRunSegment(partner, 5, 2);
        var partnerStopKey = new StopKey(new StationId(2), 0);
        partner.StopTimesInternal[new StopKey(new StationId(5), 0)] = new StopTime
        {
            Works = [new StationWork { Type = StationWorkType.StartOp, StartOpConsist = [Slot(0, 20)] }],
        };
        partner.StopTimesInternal[partnerStopKey] = new StopTime();

        train.StopTimesInternal[new StopKey(new StationId(2), 0)] = new StopTime
        {
            Works = [new StationWork
            {
                Type = StationWorkType.Coupling,
                CouplingDetail = new CouplingWork { PartnerTrainId = partner.Id, PartnerStopKey = partnerStopKey, AttachToFront = true },
            }],
        };

        var consists = MakeConsistDict(MakeConsist(10, 1), MakeConsist(20, 2));
        var compositions = MakeCompositionDict(MakeComposition(10, 10), MakeComposition(20, 20));
        var allTrainsById = new Dictionary<TrainId, Train> { [train.Id] = train, [partner.Id] = partner };
        var context = new ConsistResolutionContext(consists, compositions, allTrainsById);

        var result = CarConsistResolver.ResolveConsistAt(train, new StopKey(new StationId(3), 0), context);

        Assert.Equal(new[] { new CarCompositionId(20), new CarCompositionId(10) }, result.ConsistBlocks);
    }

    [Fact]
    public void SplitOriginRef経由の新Trainは分割元TrainのDecouplingから継続側と逆のグループを引き継ぐ()
    {
        // origin: Train(1)がStation2でDecoupling（front=継続, rear=新Train行き）
        var origin = NewTrain(1);
        AddRunSegment(origin, 1, 2);
        AddRunSegment(origin, 2, 3);
        origin.StopTimesInternal[new StopKey(new StationId(1), 0)] = new StopTime
        {
            Works = [new StationWork { Type = StationWorkType.StartOp, StartOpConsist = [Slot(0, 10), Slot(1, 20)] }],
        };
        var originDecouplingStopKey = new StopKey(new StationId(2), 0);
        origin.StopTimesInternal[originDecouplingStopKey] = new StopTime
        {
            Works = [new StationWork
            {
                Type = StationWorkType.Decoupling,
                DecouplingDetail = new DecouplingWork
                {
                    FrontGroup = [Entry(10)],
                    RearGroup = [Entry(20)],
                    IsRearBase = false, // front(10)が継続。rear(20)が新Train行き
                },
            }],
        };

        // 新Train(2)：SplitOriginRef経由でrear（Composition=20）を引き継ぐ
        var newTrain = NewTrain(2);
        AddRunSegment(newTrain, 2, 4);
        newTrain.StopTimesInternal[new StopKey(new StationId(2), 0)] = new StopTime
        {
            Works = [new StationWork
            {
                Type = StationWorkType.PrevTrain,
                SplitOrigin = new SplitOriginRef { OriginTrainId = origin.Id, OriginStopKey = originDecouplingStopKey },
            }],
        };

        var consists = MakeConsistDict(MakeConsist(10, 1), MakeConsist(20, 2));
        var compositions = MakeCompositionDict(MakeComposition(10, 10), MakeComposition(20, 20));
        var allTrainsById = new Dictionary<TrainId, Train> { [origin.Id] = origin, [newTrain.Id] = newTrain };
        var context = new ConsistResolutionContext(consists, compositions, allTrainsById);

        var result = CarConsistResolver.ResolveConsistAt(newTrain, new StopKey(new StationId(4), 0), context);

        Assert.Equal(new[] { new CarCompositionId(20) }, result.ConsistBlocks);
    }

    [Fact]
    public void SplitOriginRef経由の新TrainはIsRearBase_trueならFrontGroupを引き継ぐ()
    {
        var origin = NewTrain(1);
        AddRunSegment(origin, 1, 2);
        AddRunSegment(origin, 2, 3);
        origin.StopTimesInternal[new StopKey(new StationId(1), 0)] = new StopTime
        {
            Works = [new StationWork { Type = StationWorkType.StartOp, StartOpConsist = [Slot(0, 10), Slot(1, 20)] }],
        };
        var originDecouplingStopKey = new StopKey(new StationId(2), 0);
        origin.StopTimesInternal[originDecouplingStopKey] = new StopTime
        {
            Works = [new StationWork
            {
                Type = StationWorkType.Decoupling,
                DecouplingDetail = new DecouplingWork
                {
                    FrontGroup = [Entry(10)],
                    RearGroup = [Entry(20)],
                    IsRearBase = true, // rear(20)が継続。front(10)が新Train行き
                },
            }],
        };

        var newTrain = NewTrain(2);
        AddRunSegment(newTrain, 2, 4);
        newTrain.StopTimesInternal[new StopKey(new StationId(2), 0)] = new StopTime
        {
            Works = [new StationWork
            {
                Type = StationWorkType.PrevTrain,
                SplitOrigin = new SplitOriginRef { OriginTrainId = origin.Id, OriginStopKey = originDecouplingStopKey },
            }],
        };

        var consists = MakeConsistDict(MakeConsist(10, 1), MakeConsist(20, 2));
        var compositions = MakeCompositionDict(MakeComposition(10, 10), MakeComposition(20, 20));
        var allTrainsById = new Dictionary<TrainId, Train> { [origin.Id] = origin, [newTrain.Id] = newTrain };
        var context = new ConsistResolutionContext(consists, compositions, allTrainsById);

        var result = CarConsistResolver.ResolveConsistAt(newTrain, new StopKey(new StationId(4), 0), context);

        Assert.Equal(new[] { new CarCompositionId(10) }, result.ConsistBlocks);
    }
}
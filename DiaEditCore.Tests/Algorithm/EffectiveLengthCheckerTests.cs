using DiaEditCore.Algorithm;
using DiaEditCore.Model;
using DiaEditCore.Model.Cars;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.TimeTable.Trains;

using Xunit;

namespace DiaEditCore.Tests.Algorithm;

public class EffectiveLengthCheckerTests
{
    private static readonly RailId TrackRail = new(1);
    private static readonly VehicleTypeId VtId = new(1);
    private static readonly CarConsistId ConsistId = new(1);
    private static readonly CarCompositionId CompositionId = new(1);
    private static readonly StopKey Key = new(new StationId(1), 0);

    private static VehicleType MakeVehicleType() => new()
    {
        Id = VtId,
        Name = "E235系",
    };

    private static Car MakeCar(int id, double lengthM) => new()
    {
        Id = new CarId(id),
        CarType = "テスト車両",
        IsPower = true,
        LengthM = lengthM,
    };

    private static ConsistResolutionContext Ctx(
        Dictionary<CarConsistId, CarConsist> consists,
        Dictionary<CarCompositionId, CarComposition> compositions)
        => ConsistResolutionContext.Empty(consists, compositions);

    /// <summary>StartOpから始まり、carCount両×lengthM(各車両共通長)の1ブロック編成のTrainを作る。
    /// CarConsist（型）とCarComposition（実体、"編成A"）を1:1で対応付ける。</summary>
    private static (Train Train, Dictionary<CarConsistId, CarConsist> Consists,
        Dictionary<CarCompositionId, CarComposition> Compositions, Dictionary<CarId, Car> Cars)
        MakeTrainWithConsist(int carCount, double lengthM = 20.0)
    {
        var cars = Enumerable.Range(1, carCount).Select(i => MakeCar(i, lengthM)).ToList();
        var consist = new CarConsist
        {
            Id = ConsistId,
            VehicleTypeId = VtId,
            Type = CarConsistType.Basic,
            Cars = cars.Select((c, i) => new CarRef { CarId = c.Id, Position = i }).ToList(),
        };
        var composition = new CarComposition
        {
            Id = CompositionId,
            Name = "編成A",
            Identifier = 1,
            CarConsistId = ConsistId,
        };

        var train = new Train
        {
            Id = new TrainId(1),
            TimeTableSetId = new TimeTableSetId(1),
            TrainNumber = "1000M",
            ServiceRouteId = new ServiceRouteId(1),
            TrainTypeId = new TrainTypeId(1),
            TrainTypeName = new DisplayName { Name = "普通" },
            Nickname = new DisplayName { Name = "" },
            DefaultVehicleTypeId = VtId,
            RunSegments =
            {
                new TrainRunSegment
                {
                    FromStationId = new StationId(1),
                    ToStationId = new StationId(2),
                    StationConnectionId = new StationConnectionId(1),
                },
            },
        };
        train.StopTimesInternal[Key] = new StopTime
        {
            IsStop = true,
            ArrivalSeconds = -1,
            DepartureSeconds = 1000,
            TrackRailId = TrackRail,
            Works =
            {
                new StationWork
                {
                    Type = StationWorkType.StartOp,
                    StartOpConsist = { new StartOpCarSlot { Position = 0, CarCompositionId = CompositionId, OperationNumber = "1" } },
                },
            },
        };

        return (train, new Dictionary<CarConsistId, CarConsist> { [ConsistId] = consist },
                    new Dictionary<CarCompositionId, CarComposition> { [CompositionId] = composition },
                    cars.ToDictionary(c => c.Id));
    }
    private static Rail MakeRail(double lengthM) => new()
    {
        Id = TrackRail,
        LengthM = lengthM,
        SpeedLimitKph = 25,
        Role = RailRole.Track,
        EndpointA = new NoneEndpointRef(),
        EndpointB = new NoneEndpointRef(),
    };

    private static readonly Dictionary<VehicleTypeId, VehicleType> VehicleTypes =
        new() { [VtId] = MakeVehicleType() };

    [Fact]
    public void 編成長がRailの有効長以下ならOK()
    {
        var (train, consists, compositions, cars) = MakeTrainWithConsist(carCount: 2, lengthM: 20); // 20m×2=40m
        var rails = new Dictionary<RailId, Rail> { [TrackRail] = MakeRail(lengthM: 100) };

        var result = EffectiveLengthChecker.CheckEffectiveLength(
            train, Key, rails, new Dictionary<PlatformId, Platform>(), train.StopTimes, cars, Ctx(consists, compositions));

        Assert.IsType<LengthCheckOk>(result);
    }

    [Fact]
    public void 編成長がRailの有効長を超えるとOverflowと超過量を返す()
    {
        var (train, consists, compositions, cars) = MakeTrainWithConsist(carCount: 6, lengthM: 20); // 20m×6=120m
        var rails = new Dictionary<RailId, Rail> { [TrackRail] = MakeRail(lengthM: 100) };

        var result = EffectiveLengthChecker.CheckEffectiveLength(
            train, Key, rails, new Dictionary<PlatformId, Platform>(), train.StopTimes, cars, Ctx(consists, compositions));

        var overflow = Assert.IsType<LengthCheckOverflow>(result);
        Assert.Equal(20, overflow.OverflowMeters);
    }

    [Fact]
    public void PlatformのEffectiveLengthが設定されていればRailのLengthMより優先される()
    {
        var (train, consists, compositions, cars) = MakeTrainWithConsist(carCount: 3, lengthM: 20); // 60m
        var rails = new Dictionary<RailId, Rail> { [TrackRail] = MakeRail(lengthM: 200) }; // Rail側は十分長い
        var platform = new Platform
        {
            Id = new PlatformId(1),
            Base = new FloorUnitObjectBase { FloorUnitId = new FloorUnitId(1), Position = new Point(0, 0) },
            FacingRailIds = new List<RailId> { TrackRail },
            EffectiveLength = 50, // Platform側は不足
        };

        var result = EffectiveLengthChecker.CheckEffectiveLength(
            train, Key, rails, new Dictionary<PlatformId, Platform> { [platform.Id] = platform },
            train.StopTimes, cars, Ctx(consists, compositions));

        var overflow = Assert.IsType<LengthCheckOverflow>(result);
        Assert.Equal(10, overflow.OverflowMeters);
    }

    [Fact]
    public void PlatformのEffectiveLengthが未設定ならRailのLengthMにフォールバックする()
    {
        var (train, consists, compositions, cars) = MakeTrainWithConsist(carCount: 3, lengthM: 20); // 60m
        var rails = new Dictionary<RailId, Rail> { [TrackRail] = MakeRail(lengthM: 100) };
        var platform = new Platform
        {
            Id = new PlatformId(1),
            Base = new FloorUnitObjectBase { FloorUnitId = new FloorUnitId(1), Position = new Point(0, 0) },
            FacingRailIds = new List<RailId> { TrackRail },
            EffectiveLength = null,
        };

        var result = EffectiveLengthChecker.CheckEffectiveLength(
            train, Key, rails, new Dictionary<PlatformId, Platform> { [platform.Id] = platform },
            train.StopTimes, cars, Ctx(consists, compositions));

        Assert.IsType<LengthCheckOk>(result); // 60m <= 100m(Railフォールバック)
    }

    [Fact]
    public void TrackRailIdが未設定StopTimeはNotApplicable()
    {
        var (train, consists, compositions, cars) = MakeTrainWithConsist(carCount: 2, lengthM: 20);
        train.StopTimesInternal[Key].TrackRailId = null;
        var rails = new Dictionary<RailId, Rail> { [TrackRail] = MakeRail(lengthM: 100) };

        var result = EffectiveLengthChecker.CheckEffectiveLength(
            train, Key, rails, new Dictionary<PlatformId, Platform>(), train.StopTimes, cars, Ctx(consists, compositions));

        Assert.IsType<LengthCheckNotApplicable>(result);
    }

    [Fact]
    public void 対象StopKeyがStopTimesに存在しない場合はNotApplicable()
    {
        var (train, consists, compositions, cars) = MakeTrainWithConsist(carCount: 2, lengthM: 20);
        var rails = new Dictionary<RailId, Rail> { [TrackRail] = MakeRail(lengthM: 100) };
        var missingKey = new StopKey(new StationId(999), 0);

        var result = EffectiveLengthChecker.CheckEffectiveLength(
            train, missingKey, rails, new Dictionary<PlatformId, Platform>(), train.StopTimes, cars, Ctx(consists, compositions));

        Assert.IsType<LengthCheckNotApplicable>(result);
    }

    [Fact]
    public void 編成長がRailの有効長とちょうど一致する場合はOK()
    {
        var (train, consists, compositions, cars) = MakeTrainWithConsist(carCount: 5, lengthM: 20); // 100m
        var rails = new Dictionary<RailId, Rail> { [TrackRail] = MakeRail(lengthM: 100) };

        var result = EffectiveLengthChecker.CheckEffectiveLength(
            train, Key, rails, new Dictionary<PlatformId, Platform>(), train.StopTimes, cars, Ctx(consists, compositions));

        Assert.IsType<LengthCheckOk>(result); // 境界値：超過ではなく一致はOK扱い
    }

    [Fact]
    public void 複数PlatformのうちFacingRailIdsに対象Railを含むものだけが採用される()
    {
        var (train, consists, compositions, cars) = MakeTrainWithConsist(carCount: 3, lengthM: 20); // 60m
        var rails = new Dictionary<RailId, Rail> { [TrackRail] = MakeRail(lengthM: 200) };
        var unrelatedPlatform = new Platform
        {
            Id = new PlatformId(1),
            Base = new FloorUnitObjectBase { FloorUnitId = new FloorUnitId(1), Position = new Point(0, 0) },
            FacingRailIds = new List<RailId> { new RailId(999) }, // 対象Railを含まない
            EffectiveLength = 10, // これが誤って採用されるとOverflowになってしまう
        };
        var targetPlatform = new Platform
        {
            Id = new PlatformId(2),
            Base = new FloorUnitObjectBase { FloorUnitId = new FloorUnitId(1), Position = new Point(0, 0) },
            FacingRailIds = new List<RailId> { TrackRail },
            EffectiveLength = 100,
        };
        var platforms = new Dictionary<PlatformId, Platform>
        {
            [unrelatedPlatform.Id] = unrelatedPlatform,
            [targetPlatform.Id] = targetPlatform,
        };

        var result = EffectiveLengthChecker.CheckEffectiveLength(
            train, Key, rails, platforms, train.StopTimes, cars, Ctx(consists, compositions));

        Assert.IsType<LengthCheckOk>(result);
    }

    [Fact]
    public void 車両ごとにLengthMが異なっても合計値で正しく判定される()
    {
        // 先頭2両を21m、中間1両を19.5mとし、単純な「N両×固定長」では出ない合計値を検証する
        var cars = new List<Car>
        {
            MakeCar(1, 21.0),
            MakeCar(2, 19.5),
            MakeCar(3, 21.0),
        };
        var consist = new CarConsist
        {
            Id = ConsistId,
            VehicleTypeId = VtId,
            Type = CarConsistType.Basic,
            Cars = cars.Select((c, i) => new CarRef { CarId = c.Id, Position = i }).ToList(),
        };
        var composition = new CarComposition
        {
            Id = CompositionId,
            Name = "編成B",
            Identifier = 2,
            CarConsistId = ConsistId,
        };
        var train = new Train
        {
            Id = new TrainId(2),
            TimeTableSetId = new TimeTableSetId(1),
            TrainNumber = "2000M",
            ServiceRouteId = new ServiceRouteId(1),
            TrainTypeId = new TrainTypeId(1),
            TrainTypeName = new DisplayName { Name = "普通" },
            Nickname = new DisplayName { Name = "" },
            DefaultVehicleTypeId = VtId,
            RunSegments =
            {
                new TrainRunSegment
                {
                    FromStationId = new StationId(1),
                    ToStationId = new StationId(2),
                    StationConnectionId = new StationConnectionId(1),
                },
            },
        };
        train.StopTimesInternal[Key] = new StopTime
        {
            IsStop = true,
            ArrivalSeconds = -1,
            DepartureSeconds = 1000,
            TrackRailId = TrackRail,
            Works =
            {
                new StationWork
                {
                    Type = StationWorkType.StartOp,
                    StartOpConsist = { new StartOpCarSlot { Position = 0, CarCompositionId = CompositionId, OperationNumber = "1" } },
                },
            },
        };
        var consists = new Dictionary<CarConsistId, CarConsist> { [ConsistId] = consist };
        var compositions = new Dictionary<CarCompositionId, CarComposition> { [CompositionId] = composition };
        var carsDict = cars.ToDictionary(c => c.Id);
        var rails = new Dictionary<RailId, Rail> { [TrackRail] = MakeRail(lengthM: 61) }; // 合計61.5m > 61m

        var result = EffectiveLengthChecker.CheckEffectiveLength(
            train, Key, rails, new Dictionary<PlatformId, Platform>(), train.StopTimes, carsDict, Ctx(consists, compositions));

        var overflow = Assert.IsType<LengthCheckOverflow>(result);
        Assert.Equal(0.5, overflow.OverflowMeters, 3);
    }
}
using DiaEditCore.Algorithm;
using DiaEditCore.Model;
using DiaEditCore.Model.Cars;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.TimeTable;

using Xunit;

namespace DiaEditCore.Tests.Algorithm;

public class EffectiveLengthCheckerTests
{
    private static readonly RailId TrackRail = new(1);
    private static readonly VehicleTypeId VtId = new(1);
    private static readonly CarConsistId ConsistId = new(1);
    private static readonly StopKey Key = new(new StationId(1), 0);

    private static VehicleType MakeVehicleType(double lengthM) => new()
    {
        Id = VtId,
        Name = "E235系",
        LengthM = lengthM,
        BaseCarTemplate = new(),
    };

    private static Car MakeCar(int id) => new()
    {
        Id = new CarId(id),
        VehicleTypeId = VtId,
        Number = $"{id}",
    };

    /// <summary>StartOpから始まり、carCountsの各要素をそのまま両数として持つ1ブロック編成のTrainを作る。</summary>
    private static (Train Train, Dictionary<CarConsistId, CarConsist> Consists, Dictionary<CarId, Car> Cars) MakeTrainWithConsist(int carCount)
    {
        var cars = Enumerable.Range(1, carCount).Select(MakeCar).ToList();
        var consist = new CarConsist
        {
            Id = ConsistId,
            Name = "編成A",
            VehicleTypeId = VtId,
            SourceTemplate = new BaseTemplateSource(),
            Identifier = "A",
            Cars = cars.Select((c, i) => new CarRef { CarId = c.Id, Position = i }).ToList(),
        };

        var train = new Train
        {
            Id = new TrainId(1),
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
        train.StopTimes[Key] = new StopTime
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
                    StartOpConsist = { new StartOpCarSlot { Position = 0, CarConsistId = ConsistId } },
                },
            },
        };

        return (train, new Dictionary<CarConsistId, CarConsist> { [ConsistId] = consist },
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

    [Fact]
    public void 編成長がRailの有効長以下ならOK()
    {
        var (train, consists, cars) = MakeTrainWithConsist(carCount: 2); // 20m×2=40m
        var vehicleTypes = new Dictionary<VehicleTypeId, VehicleType> { [VtId] = MakeVehicleType(20) };
        var rails = new Dictionary<RailId, Rail> { [TrackRail] = MakeRail(lengthM: 100) };

        var result = EffectiveLengthChecker.CheckEffectiveLength(
            train, Key, rails, new Dictionary<PlatformId, Platform>(), train.StopTimes, cars, vehicleTypes, consists);

        Assert.IsType<LengthCheckOk>(result);
    }

    [Fact]
    public void 編成長がRailの有効長を超えるとOverflowと超過量を返す()
    {
        var (train, consists, cars) = MakeTrainWithConsist(carCount: 6); // 20m×6=120m
        var vehicleTypes = new Dictionary<VehicleTypeId, VehicleType> { [VtId] = MakeVehicleType(20) };
        var rails = new Dictionary<RailId, Rail> { [TrackRail] = MakeRail(lengthM: 100) };

        var result = EffectiveLengthChecker.CheckEffectiveLength(
            train, Key, rails, new Dictionary<PlatformId, Platform>(), train.StopTimes, cars, vehicleTypes, consists);

        var overflow = Assert.IsType<LengthCheckOverflow>(result);
        Assert.Equal(20, overflow.OverflowMeters);
    }

    [Fact]
    public void PlatformのEffectiveLengthが設定されていればRailのLengthMより優先される()
    {
        var (train, consists, cars) = MakeTrainWithConsist(carCount: 3); // 60m
        var vehicleTypes = new Dictionary<VehicleTypeId, VehicleType> { [VtId] = MakeVehicleType(20) };
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
            train.StopTimes, cars, vehicleTypes, consists);

        var overflow = Assert.IsType<LengthCheckOverflow>(result);
        Assert.Equal(10, overflow.OverflowMeters);
    }

    [Fact]
    public void PlatformのEffectiveLengthが未設定ならRailのLengthMにフォールバックする()
    {
        var (train, consists, cars) = MakeTrainWithConsist(carCount: 3); // 60m
        var vehicleTypes = new Dictionary<VehicleTypeId, VehicleType> { [VtId] = MakeVehicleType(20) };
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
            train.StopTimes, cars, vehicleTypes, consists);

        Assert.IsType<LengthCheckOk>(result); // 60m <= 100m(Railフォールバック)
    }

    [Fact]
    public void TrackRailIdが未設定StopTimeはNotApplicable()
    {
        var (train, consists, cars) = MakeTrainWithConsist(carCount: 2);
        train.StopTimes[Key].TrackRailId = null;
        var vehicleTypes = new Dictionary<VehicleTypeId, VehicleType> { [VtId] = MakeVehicleType(20) };
        var rails = new Dictionary<RailId, Rail> { [TrackRail] = MakeRail(lengthM: 100) };

        var result = EffectiveLengthChecker.CheckEffectiveLength(
            train, Key, rails, new Dictionary<PlatformId, Platform>(), train.StopTimes, cars, vehicleTypes, consists);

        Assert.IsType<LengthCheckNotApplicable>(result);
    }

    [Fact]
    public void 対象StopKeyがStopTimesに存在しない場合はNotApplicable()
    {
        var (train, consists, cars) = MakeTrainWithConsist(carCount: 2);
        var vehicleTypes = new Dictionary<VehicleTypeId, VehicleType> { [VtId] = MakeVehicleType(20) };
        var rails = new Dictionary<RailId, Rail> { [TrackRail] = MakeRail(lengthM: 100) };
        var missingKey = new StopKey(new StationId(999), 0);

        var result = EffectiveLengthChecker.CheckEffectiveLength(
            train, missingKey, rails, new Dictionary<PlatformId, Platform>(), train.StopTimes, cars, vehicleTypes, consists);

        Assert.IsType<LengthCheckNotApplicable>(result);
    }

    [Fact]
    public void 編成長がRailの有効長とちょうど一致する場合はOK()
    {
        var (train, consists, cars) = MakeTrainWithConsist(carCount: 5); // 100m
        var vehicleTypes = new Dictionary<VehicleTypeId, VehicleType> { [VtId] = MakeVehicleType(20) };
        var rails = new Dictionary<RailId, Rail> { [TrackRail] = MakeRail(lengthM: 100) };

        var result = EffectiveLengthChecker.CheckEffectiveLength(
            train, Key, rails, new Dictionary<PlatformId, Platform>(), train.StopTimes, cars, vehicleTypes, consists);

        Assert.IsType<LengthCheckOk>(result); // 境界値：超過ではなく一致はOK扱い
    }

    [Fact]
    public void 複数PlatformのうちFacingRailIdsに対象Railを含むものだけが採用される()
    {
        var (train, consists, cars) = MakeTrainWithConsist(carCount: 3); // 60m
        var vehicleTypes = new Dictionary<VehicleTypeId, VehicleType> { [VtId] = MakeVehicleType(20) };
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
            train, Key, rails, platforms, train.StopTimes, cars, vehicleTypes, consists);

        Assert.IsType<LengthCheckOk>(result);
    }
}

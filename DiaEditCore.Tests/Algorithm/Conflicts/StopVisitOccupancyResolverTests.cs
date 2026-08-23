// using DiaEditCore.Algorithm;
// using DiaEditCore.Algorithm.Conflicts;
// using DiaEditCore.Model;
// using DiaEditCore.Model.Routes;
// using DiaEditCore.Model.Stations;
// using DiaEditCore.Model.TimeTable.Trains;

// using Xunit;

// namespace DiaEditCore.Tests.Algorithm.Conflicts;

// public class StopVisitOccupancyResolverTests
// {
//     // -----------------------------
//     // 共通フィクスチャ：A - B - C の3駅、B駅に1本のTrack(trackB)を持つ簡易路線
//     // -----------------------------

//     private static readonly StationId StA = new(1);
//     private static readonly StationId StB = new(2);
//     private static readonly StationId StC = new(3);

//     private static readonly EntryPointId EpB = new(20);
//     private static readonly BoundaryPointId BpB = new(21);
//     private static readonly RailId TrackB = new(30);

//     private static readonly StationConnectionId ScAB = new(1);
//     private static readonly StationConnectionId ScBC = new(2);
//     private static readonly StationConnectionSegmentId ScsAB = new(1);
//     private static readonly StationConnectionSegmentId ScsBC = new(2);

//     private static readonly StationPathId ArrivalPathId = new(101);
//     private static readonly StationPathId DeparturePathId = new(102);

//     private static StationPath MakeArrivalPath(int adjustmentSec) => new()
//     {
//         Id = ArrivalPathId,
//         FloorUnitId = new FloorUnitId(1),
//         Name = "B到着",
//         Direction = StationPathDirection.Arrival,
//         Waypoints = [new EntryPointWaypoint(EpB), new BoundaryPointWaypoint(BpB)],
//         AdjustmentSec = adjustmentSec,
//     };

//     private static StationPath MakeDeparturePath(int adjustmentSec) => new()
//     {
//         Id = DeparturePathId,
//         FloorUnitId = new FloorUnitId(1),
//         Name = "B出発",
//         Direction = StationPathDirection.Departure,
//         Waypoints = [new BoundaryPointWaypoint(BpB), new EntryPointWaypoint(EpB)],
//         AdjustmentSec = adjustmentSec,
//     };

//     private static Train NewTrain(int id, string trainNumber) => new()
//     {
//         Id = new TrainId(id),
//         TimeTableSetId = new TimeTableSetId(1),
//         TrainNumber = trainNumber,
//         ServiceRouteId = new ServiceRouteId(1),
//         TrainTypeId = new TrainTypeId(1),
//         TrainTypeName = new DisplayName { Name = "普通" },
//         Nickname = new DisplayName { Name = "" },
//         DefaultVehicleTypeId = new VehicleTypeId(1),
//     };

//     private static (
//         Func<StationConnectionId, IReadOnlyList<EntryPointSequenceElement>> ResolveEp,
//         IReadOnlyDictionary<StationPathId, StationPath> PathsById,
//         IReadOnlyDictionary<(EntryPointId, RailId), StationPathId> ArrivalIndex,
//         IReadOnlyDictionary<(RailId, EntryPointId), StationPathId> DepartureIndex
//     ) BuildTopology(int arrivalAdjustmentSec = 30, int departureAdjustmentSec = 20)
//     {
        
//         var segs = new List<StationConnectionSegment>
//         {
//             new() { Id = ScsAB, FromStationId = StA, ToStationId = StB, FromEntryPointId = new EntryPointId(10), ToEntryPointId = EpB, MainRouteId = new MainRouteId(1), },
//             new() { Id = ScsBC, FromStationId = StB, ToStationId = StC, FromEntryPointId = EpB, ToEntryPointId = new EntryPointId(40), MainRouteId = new MainRouteId(1), },
//         };
//         var scs = new List<StationConnection>
//         {
//             new() { Id = ScAB, Name = "AB", MainRouteId = new MainRouteId(1), Direction = StationConnectionDirection.Down, Segments = [ScsAB] },
//             new() { Id = ScBC, Name = "BC", MainRouteId = new MainRouteId(1), Direction = StationConnectionDirection.Down, Segments = [ScsBC] },
//         };

//         var arrivalPath = MakeArrivalPath(arrivalAdjustmentSec);
//         var departurePath = MakeDeparturePath(departureAdjustmentSec);
//         var pathsById = new Dictionary<StationPathId, StationPath>
//         {
//             [arrivalPath.Id] = arrivalPath,
//             [departurePath.Id] = departurePath,
//         };
//         var rails = new List<Rail>
//         {
//             new()
//             {
//                 Id = TrackB, LengthM = 200, SpeedLimitKph = 25, Role = RailRole.Track,
//                 EndpointA = new EntryPointEndpointRef(EpB), EndpointB = new BoundaryPointEndpointRef(BpB),
//             },
//         };

//         var (arrivalIndex, departureIndex) = StationPathTrackIndexBuilder.Build([arrivalPath, departurePath], rails);
//         var resolveEp = EntryPointSequenceCache.Build(scs, segs);

//         return (resolveEp, pathsById, arrivalIndex, departureIndex);
//     }

//     // -----------------------------
//     // テスト
//     // -----------------------------

//     [Fact]
//     public void 終着訪問は到着占有情報のみが埋まり出発側はnull()
//     {
//         var (resolveEp, pathsById, arrivalIndex, departureIndex) = BuildTopology(arrivalAdjustmentSec: 30);
//         var train = NewTrain(1, "arr");
//         train.RunSegments.Add(new TrainRunSegment { FromStationId = StA, ToStationId = StB, StationConnectionId = ScAB });
//         train.StopTimesInternal[new StopKey(StB, 0)] = new StopTime { IsStop = true, ArrivalSeconds = 1000, DepartureSeconds = -1, TrackRailId = TrackB };

//         var result = StopVisitOccupancyResolver.Resolve(train, visitSeq: 1, resolveEp, pathsById, arrivalIndex, departureIndex);

//         Assert.NotNull(result);
//         Assert.Equal(ArrivalPathId, result!.Value.ArrivalSpId);
//         Assert.Equal(970, result.Value.ArrivalStart);
//         Assert.Equal(1000, result.Value.ArrivalEnd);
//         Assert.Null(result.Value.DepartureSpId);
//         Assert.Null(result.Value.DepartureStart);
//         Assert.Null(result.Value.DepartureEnd);
//     }

//     [Fact]
//     public void 始発訪問は出発占有情報のみが埋まり到着側はnull()
//     {
//         var (resolveEp, pathsById, arrivalIndex, departureIndex) = BuildTopology(departureAdjustmentSec: 20);
//         var train = NewTrain(1, "dep");
//         train.RunSegments.Add(new TrainRunSegment { FromStationId = StB, ToStationId = StC, StationConnectionId = ScBC });
//         train.StopTimesInternal[new StopKey(StB, 0)] = new StopTime { IsStop = true, ArrivalSeconds = -1, DepartureSeconds = 1100, TrackRailId = TrackB };

//         var result = StopVisitOccupancyResolver.Resolve(train, visitSeq: 0, resolveEp, pathsById, arrivalIndex, departureIndex);

//         Assert.NotNull(result);
//         Assert.Null(result!.Value.ArrivalSpId);
//         Assert.Equal(DeparturePathId, result.Value.DepartureSpId);
//         Assert.Equal(1100, result.Value.DepartureStart);
//         Assert.Equal(1120, result.Value.DepartureEnd);
//     }

//     [Fact]
//     public void 中間駅停車訪問は到着出発両方の占有情報が埋まる()
//     {
//         var (resolveEp, pathsById, arrivalIndex, departureIndex) = BuildTopology(arrivalAdjustmentSec: 30, departureAdjustmentSec: 20);
//         var train = NewTrain(1, "through");
//         train.RunSegments.Add(new TrainRunSegment { FromStationId = StA, ToStationId = StB, StationConnectionId = ScAB });
//         train.RunSegments.Add(new TrainRunSegment { FromStationId = StB, ToStationId = StC, StationConnectionId = ScBC });
//         train.StopTimesInternal[new StopKey(StB, 0)] = new StopTime { IsStop = true, ArrivalSeconds = 1000, DepartureSeconds = 1100, TrackRailId = TrackB };

//         var result = StopVisitOccupancyResolver.Resolve(train, visitSeq: 1, resolveEp, pathsById, arrivalIndex, departureIndex);

//         Assert.NotNull(result);
//         Assert.Equal(970, result!.Value.ArrivalStart);
//         Assert.Equal(1000, result.Value.ArrivalEnd);
//         Assert.Equal(1100, result.Value.DepartureStart);
//         Assert.Equal(1120, result.Value.DepartureEnd);
//     }

//     [Fact]
//     public void 通過訪問はdepartureSecondsを通過基準時刻として到着出発両方に流用する()
//     {
//         var (resolveEp, pathsById, arrivalIndex, departureIndex) = BuildTopology(arrivalAdjustmentSec: 30, departureAdjustmentSec: 20);
//         var train = NewTrain(1, "pass");
//         train.RunSegments.Add(new TrainRunSegment { FromStationId = StA, ToStationId = StB, StationConnectionId = ScAB });
//         train.RunSegments.Add(new TrainRunSegment { FromStationId = StB, ToStationId = StC, StationConnectionId = ScBC });
//         train.StopTimesInternal[new StopKey(StB, 0)] = new StopTime { IsStop = false, ArrivalSeconds = -1, DepartureSeconds = 1050, TrackRailId = TrackB };

//         var result = StopVisitOccupancyResolver.Resolve(train, visitSeq: 1, resolveEp, pathsById, arrivalIndex, departureIndex);

//         Assert.NotNull(result);
//         Assert.Equal(1020, result!.Value.ArrivalStart);  // 通過基準1050 - adjustment30
//         Assert.Equal(1050, result.Value.ArrivalEnd);
//         Assert.Equal(1050, result.Value.DepartureStart);
//         Assert.Equal(1070, result.Value.DepartureEnd);   // 通過基準1050 + adjustment20
//     }

//     [Fact]
//     public void 通過でdepartureSecondsが未設定の場合は対象外としてnullを返す()
//     {
//         var (resolveEp, pathsById, arrivalIndex, departureIndex) = BuildTopology();
//         var train = NewTrain(1, "pass");
//         train.RunSegments.Add(new TrainRunSegment { FromStationId = StA, ToStationId = StB, StationConnectionId = ScAB });
//         train.RunSegments.Add(new TrainRunSegment { FromStationId = StB, ToStationId = StC, StationConnectionId = ScBC });
//         train.StopTimesInternal[new StopKey(StB, 0)] = new StopTime { IsStop = false, ArrivalSeconds = -1, DepartureSeconds = -1, TrackRailId = TrackB };

//         var result = StopVisitOccupancyResolver.Resolve(train, visitSeq: 1, resolveEp, pathsById, arrivalIndex, departureIndex);

//         Assert.NotNull(result); // StopTime自体は存在するのでVisitOccupancyは返るが、占有情報は全てnull
//         Assert.Null(result!.Value.ArrivalStart);
//         Assert.Null(result.Value.DepartureStart);
//     }

//     [Fact]
//     public void 対象StopKeyがStopTimesに存在しない場合はnullを返す()
//     {
//         var (resolveEp, pathsById, arrivalIndex, departureIndex) = BuildTopology();
//         var train = NewTrain(1, "empty");
//         train.RunSegments.Add(new TrainRunSegment { FromStationId = StA, ToStationId = StB, StationConnectionId = ScAB });
//         // StopTimeを一切設定しない

//         var result = StopVisitOccupancyResolver.Resolve(train, visitSeq: 1, resolveEp, pathsById, arrivalIndex, departureIndex);

//         Assert.Null(result);
//     }

//     [Fact]
//     public void TrackRailIdが未設定の場合はnullを返す()
//     {
//         var (resolveEp, pathsById, arrivalIndex, departureIndex) = BuildTopology();
//         var train = NewTrain(1, "arr");
//         train.RunSegments.Add(new TrainRunSegment { FromStationId = StA, ToStationId = StB, StationConnectionId = ScAB });
//         train.StopTimesInternal[new StopKey(StB, 0)] = new StopTime { IsStop = true, ArrivalSeconds = 1000, TrackRailId = null };

//         var result = StopVisitOccupancyResolver.Resolve(train, visitSeq: 1, resolveEp, pathsById, arrivalIndex, departureIndex);

//         Assert.Null(result);
//     }

//     [Fact]
//     public void 対応するStationPathTrackIndexが見つからない場合は当該側の占有情報のみnullになる()
//     {
//         // trackIndexが空＝(EntryPointId, RailId)の組がarrivalIndexに存在しないケース
//         var (resolveEp, pathsById, _, departureIndex) = BuildTopology();
//         var emptyArrivalIndex = new Dictionary<(EntryPointId, RailId), StationPathId>();
//         var train = NewTrain(1, "arr");
//         train.RunSegments.Add(new TrainRunSegment { FromStationId = StA, ToStationId = StB, StationConnectionId = ScAB });
//         train.StopTimesInternal[new StopKey(StB, 0)] = new StopTime { IsStop = true, ArrivalSeconds = 1000, TrackRailId = TrackB };

//         var result = StopVisitOccupancyResolver.Resolve(train, visitSeq: 1, resolveEp, pathsById, emptyArrivalIndex, departureIndex);

//         Assert.NotNull(result);
//         Assert.Null(result!.Value.ArrivalSpId);
//         Assert.Null(result.Value.ArrivalStart);
//     }
// }

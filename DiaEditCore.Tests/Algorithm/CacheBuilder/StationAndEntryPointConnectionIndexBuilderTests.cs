// namespace DiaEditCore.Tests.Algorithm.CacheBuilder;

// using DiaEditCore.Algorithm.CacheBuilder;
// using DiaEditCore.Model;
// using DiaEditCore.Model.Routes;
// using Xunit;

// public class StationAndEntryPointConnectionIndexBuilderTests
// {
//     private static StationConnectionSegment MakeSeg(
//         int id, int fromStation, int toStation, int fromEp, int toEp) => new()
//     {
//         Id = new StationConnectionSegmentId(id),
//         FromStationId = new StationId(fromStation),
//         ToStationId = new StationId(toStation),
//         FromEntryPointId = new EntryPointId(fromEp),
//         ToEntryPointId = new EntryPointId(toEp),
//         MainRouteId = new MainRouteId(1),
//     };

//     private static StationConnection MakeSc(int id, params int[] segIds) => new()
//     {
//         Id = new StationConnectionId(id),
//         Name = $"SC{id}",
//         MainRouteId = new MainRouteId(1),
//         Direction = StationConnectionDirection.Down,
//         Segments = segIds.Select(s => new StationConnectionSegmentId(s)).ToList(),
//     };

//     [Fact]
//     public void Build_単純な2駅間SC_StationとEntryPointの双方が登録される()
//     {
//         // 東京(駅1,EP10) → 品川(駅2,EP20) の1区間のみのStationConnection
//         var segs = new[] { MakeSeg(100, 1, 2, 10, 20) };
//         var scs = new[] { MakeSc(1, 100) };

//         var (stationIdx, epIdx) = StationAndEntryPointConnectionIndexBuilder.Build(scs, segs);

//         Assert.Equal(new[] { new StationConnectionId(1) }, stationIdx[new StationId(1)]);
//         Assert.Equal(new[] { new StationConnectionId(1) }, stationIdx[new StationId(2)]);
//         Assert.Equal(new[] { new StationConnectionId(1) }, epIdx[new EntryPointId(10)]);
//         Assert.Equal(new[] { new StationConnectionId(1) }, epIdx[new EntryPointId(20)]);
//     }

//     [Fact]
//     public void Build_複数区間を跨ぐ中間駅も途中駅として登録される()
//     {
//         // 東京(1)→品川(2)→横浜(3) の2区間からなるStationConnection
//         var segs = new[]
//         {
//             MakeSeg(100, 1, 2, 10, 21),
//             MakeSeg(101, 2, 3, 22, 30),
//         };
//         var scs = new[] { MakeSc(1, 100, 101) };

//         var (stationIdx, epIdx) = StationAndEntryPointConnectionIndexBuilder.Build(scs, segs);

//         // 中間駅（品川=2）も登録されること
//         Assert.True(stationIdx.ContainsKey(new StationId(2)));
//         Assert.Equal(3, stationIdx.Count); // 駅1,2,3すべて
//         // 中間駅の到着EP(21)・出発EP(22)は別モノとして両方登録される
//         Assert.Equal(4, epIdx.Count); // EP10,21,22,30
//     }

//     [Fact]
//     public void Build_同一StationConnection内で同一駅を複数回通っても重複登録しない()
//     {
//         // ループ線などで同一駅(1)を2区間から参照するケース
//         var segs = new[]
//         {
//             MakeSeg(100, 1, 2, 10, 20),
//             MakeSeg(101, 2, 1, 21, 11), // 駅1に戻ってくる
//         };
//         var scs = new[] { MakeSc(1, 100, 101) };

//         var (stationIdx, _) = StationAndEntryPointConnectionIndexBuilder.Build(scs, segs);

//         // 駅1へのStationConnectionId=1の登録は1件のみ（重複除去されている）
//         Assert.Single(stationIdx[new StationId(1)]);
//     }

//     [Fact]
//     public void Build_参照先SCSが存在しない場合はそのSegmentIdをスキップする()
//     {
//         // SC1がSegmentId=999を参照するが、allSegmentsに実体が無い（参照整合性エラーのケース）
//         var segs = Array.Empty<StationConnectionSegment>();
//         var scs = new[] { MakeSc(1, 999) };

//         var (stationIdx, epIdx) = StationAndEntryPointConnectionIndexBuilder.Build(scs, segs);

//         Assert.Empty(stationIdx);
//         Assert.Empty(epIdx);
//     }

//     [Fact]
//     public void Build_複数StationConnectionが同一駅を通る場合は両方登録される()
//     {
//         var segs = new[]
//         {
//             MakeSeg(100, 1, 2, 10, 20),
//             MakeSeg(101, 1, 3, 11, 30), // 別路線で同じ駅1発
//         };
//         var scs = new[] { MakeSc(1, 100), MakeSc(2, 101) };

//         var (stationIdx, _) = StationAndEntryPointConnectionIndexBuilder.Build(scs, segs);

//         Assert.Equal(
//             new[] { new StationConnectionId(1), new StationConnectionId(2) },
//             stationIdx[new StationId(1)]);
//     }
// }
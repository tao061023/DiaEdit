using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.Cars;
using DiaEditCore.Model.TimeTable;

using DiaEditCore.Algorithm;
using System.Transactions;
using System.Diagnostics.CodeAnalysis;

namespace DiaEditCore.Algorithm;

public abstract record LengthCheckResult;

public sealed record LengthCheckOk() : LengthCheckResult;
public sealed record LengthCheckNotApplicable() : LengthCheckResult;
public sealed record LengthCheckOverflow(double OverflowMeters) : LengthCheckResult;
public static class EffectiveLengthChecker
{
    public static LengthCheckResult CheckEffectiveLength(
        Train train,
        StopKey stopKey,
        IReadOnlyDictionary<RailId, Rail> rails,
        IReadOnlyDictionary<PlatformId, Platform> platforms,
        IReadOnlyDictionary<StopKey, StopTime> stopTimes,
        IReadOnlyDictionary<CarId, Car> cars,
        IReadOnlyDictionary<VehicleTypeId, VehicleType> vehicleTypes,
        IReadOnlyDictionary<CarConsistId, CarConsist> carConsists)
    {
        // --- StopTime の取得 ---
        if (!stopTimes.TryGetValue(stopKey, out var stopTime))
            return new LengthCheckNotApplicable();

        // trackRailId 未設定 → 通過扱い
        if (stopTime.TrackRailId is null)
            return new LengthCheckNotApplicable();

        var railId = stopTime.TrackRailId.Value;

        // --- 編成復元（CarConsistResolver を使用） ---
        var consist = CarConsistResolver.ResolveConsistAt(train, stopKey, carConsists);

        // --- 編成長の計算（VehicleType.lengthM の合計） ---
        double totalLength = consist.Cars
            .Sum(carRef =>
            {
                var car = cars[carRef.CarId];
                var vt = vehicleTypes[car.VehicleTypeId];
                return vt.LengthM;
            });

        // --- Platform の検索（FacingRailIds に railId を含むもの） ---
        var platform = platforms.Values
            .FirstOrDefault(p => p.FacingRailIds.Contains(railId));

        // --- 有効長の決定（Platform → fallback Rail） ---
        double effectiveLength =
            platform?.EffectiveLength ?? rails[railId].LengthM;

        // --- 判定 ---
        if (totalLength <= effectiveLength)
            return new LengthCheckOk();

        return new LengthCheckOverflow(totalLength - effectiveLength);
    }
}
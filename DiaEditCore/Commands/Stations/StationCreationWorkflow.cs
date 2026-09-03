namespace DiaEditCore.Commands.Stations;

using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Session;

/// <summary>
/// 4.2節「運用：n≥1制約を保つため、Station作成操作は空のFloorUnit同時生成までを1つの複合コマンド
/// （TransactionCommand）とする」の実装。
///
/// CreateStationCommand単体をUI層が直接呼び出すと、Station作成直後〜FloorUnit追加までの間
/// n≥1制約（保存時検証）に違反した状態が生じうる。本ワークフローはCreateStationCommandと
/// CreateFloorUnitCommandをTransactionCommandで束ね、常に両方が揃った状態のみをUndo単位とする。
///
/// FloorUnitのStationIdはCreateStationCommand.Execute()実行後でなければ確定しないため、
/// 2つ目のファクトリはTransactionCommand内部での遅延評価（Func&lt;IUndoableCommand&gt;）を利用し、
/// createStation.Createdへのクロージャ参照を通じて実行時に解決する。
/// </summary>
public static class StationCreationWorkflow
{
    public static TransactionCommand CreateStationWithDefaultFloorUnit(
        List<Station> stations,
        List<FloorUnit> floorUnits,
        IdAllocator<StationId> stationIds,
        IdAllocator<FloorUnitId> floorUnitIds,
        DisplayName displayName,
        StationType type,
        string operatingCode = "",
        string telegraphCode = "")
    {
        var createStation = new CreateStationCommand(stations, stationIds, displayName, type, operatingCode, telegraphCode);

        return new TransactionCommand(new List<Func<IUndoableCommand>>
        {
            () => createStation,
            () => new CreateFloorUnitCommand(floorUnits, floorUnitIds, createStation.Created!.Id)
        });
    }
}
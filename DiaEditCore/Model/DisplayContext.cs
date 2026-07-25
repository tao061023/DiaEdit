namespace DiaEditCore.Model;

/// <summary>
/// DisplayContextが対象とする区間。MainRouteから生成するのが基本だが、
/// ユーザーが任意の区間列を組んで独自のDisplayContextを作ることも可能。
/// </summary>
public record MainRouteRange(MainRouteId MainRouteId, int FromIndex, int ToIndex);

/// <summary>
/// ダイヤグラム・駅時刻表の表示対象を定義する（5.15節）。
/// 「路線系統」を基準に表示範囲を定義し、そこにServiceRouteに属するTrainを投影する。
/// stationOrderはこのMainRouteRangesから導出される表示用キャッシュであり、
/// ここには持たせない（Algorithm層のresolveDisplayContextStationOrder＋
/// TimeTableSetCache.stationOrderByDisplayContextIdで扱う。6章参照）。
/// </summary>
public record DisplayContext(
    DisplayContextId Id,
    DisplayName Name,
    IReadOnlyList<MainRouteRange> MainRouteRanges
);

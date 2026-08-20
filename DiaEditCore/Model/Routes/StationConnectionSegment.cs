namespace DiaEditCore.Model.Routes;

public sealed class StationConnectionSegment
{
    public required StationConnectionSegmentId Id { get; set; }
    public required StationId FromStationId { get; set; }
    public required StationId ToStationId { get; set; }
    public required EntryPointId FromEntryPointId { get; set; } // FromStationId側で使うEP（正データ）
    public required EntryPointId ToEntryPointId { get; set; }   // ToStationId側で使うEP（正データ）
    public required MainRouteId MainRouteId { get; set; }
    public double LengthM { get; set; }
    public double SpeedLimitKph { get; set; }
    // BaseRunTimeSec は v12.27で廃止（区間固定スカラー値では停車/通過4パターンによる
    // 実所要時分の差を表現できないため）。所要時分は RunTimeCalculator が
    // DiagramRevision.BaseTimeTableSetId 内のTrain実績から都度導出する（5.6節参照）。
}
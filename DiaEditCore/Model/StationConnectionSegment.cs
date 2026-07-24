namespace DiaEditCore.Model;

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
    public required int BaseRunTimeSec { get; set; } // 駅間の基準所要時分（ユーザー直接入力・正データ）
}
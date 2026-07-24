namespace DiaEditCore.Model;

public sealed class Train
{
    public required TrainId Id { get; set; }
    public required string TrainNumber { get; set; } // 一意（同一TimeTableSet内で重複禁止）
    public int? ServiceNumber { get; set; }            // 号数案内用
    public required ServiceRouteId ServiceRouteId { get; set; }

    public required TrainTypeId TrainTypeId { get; set; }
    public required DisplayName TrainTypeName { get; set; } // 基準列車からコピー、以後個別編集可
    public required DisplayName Nickname { get; set; }
    public required VehicleTypeId DefaultVehicleTypeId { get; set; } // 編成未確定時のフォールバック性能参照

    public TrainId? SourceTrainId { get; set; }         // 選択した基準列車。手動作成ならnull
    public int Revision { get; set; } = 0;
    public int? SourceRevisionAtCopy { get; set; }

    public List<TrainRunSegment> RunSegments { get; set; } = new();
    public Dictionary<StopKey, StopTime> StopTimes { get; set; } = new();
    public bool IsProvisional { get; set; } = true;
}
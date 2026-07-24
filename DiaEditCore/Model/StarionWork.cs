namespace DiaEditCore.Model;

public enum StationWorkType
{
    None, PrevTrain, StartOp, EndOp, Shunting, NextTrain, Coupling, Decoupling, OpNumberChange,
}

public enum NextTrainType { Other, TypeChange, InfoChange, SameTrain }

public sealed class TrainCutPoint
{
    public required TrainId TrainId { get; set; }
    public required int Position { get; set; } // consistSequence内のインデックス
    public required CarConsistId CarConsistId { get; set; }
}

public sealed class StationWork
{
    public required StationWorkType Type { get; set; }
    public List<TrainCutPoint> CutPoints { get; set; } = new();       // Coupling/Decoupling/StartOpで使用
    public NextTrainType? NextTrainType { get; set; }                  // NextTrainのみ使用
    public StationPathId? StationPathId { get; set; }                  // Shunting等
    public TrainOperationId? TrainOperationId { get; set; }            // StartOp/OpNumberChange
    public int StartOpSeconds { get; set; } = -1;
    public int EndOpSeconds { get; set; } = -1;
}
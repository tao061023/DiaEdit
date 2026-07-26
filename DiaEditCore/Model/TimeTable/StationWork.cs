namespace DiaEditCore.Model.TimeTable;

using DiaEditCore.Model;

public enum StationWorkType
{
    None, PrevTrain, StartOp, EndOp, Shunting, NextTrain, Coupling, Decoupling, OpNumberChange,
}

public enum NextTrainType { Other, TypeChange, InfoChange, SameTrain }

public sealed class StartOpCarSlot
{
    public required int Position { get; set; }
    public required CarConsistId CarConsistId { get; set; }
    public string OperationNumber { get; set; } = string.Empty; // 追加：空文字列ならStationWork.TrainOperationId（StartOp本体の運用）を継承
}

public sealed class TrainCutPoint
{
    public required TrainId TrainId { get; set; }
    public required int Position { get; set; }
    public required CarConsistId CarConsistId { get; set; }
    public string OperationNumber { get; set; } = string.Empty; // 追加：Decoupling=空文字列なら分割元Trainの運用番号を継承／Coupling=併合前の運用番号（自由記述の履歴）
}

public sealed class StationWork
{
    public required StationWorkType Type { get; set; }

    public List<StartOpCarSlot> StartOpConsist { get; set; } = new(); // StartOpでのみ使用
    public List<TrainCutPoint> CutPoints { get; set; } = new();       // Coupling/Decouplingでのみ使用

    public NextTrainType? NextTrainType { get; set; }
    public StationPathId? StationPathId { get; set; }
    public TrainOperationId? TrainOperationId { get; set; }
    public int StartOpSeconds { get; set; } = -1;
    public int EndOpSeconds { get; set; } = -1;
}
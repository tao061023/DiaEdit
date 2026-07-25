namespace DiaEditCore.Model;

public enum StationWorkType
{
    None, PrevTrain, StartOp, EndOp, Shunting, NextTrain, Coupling, Decoupling, OpNumberChange,
}

public enum NextTrainType { Other, TypeChange, InfoChange, SameTrain }

/// <summary>
/// StartOp専用：出区時点のconsistSequenceを表す1要素。
/// trainIdは意味を持たない（5.11.5節）ため、そもそもフィールドとして持たせない。
/// </summary>
public sealed class StartOpCarSlot
{
    public required int Position { get; set; }              // consistSequence内のインデックス
    public required CarConsistId CarConsistId { get; set; } // そのpositionの編成
}

/// <summary>
/// Coupling/Decoupling専用：分割・併合の相手Trainを特定する必要があるためtrainIdを保持する。
/// </summary>
public sealed class TrainCutPoint
{
    public required TrainId TrainId { get; set; }
    public required int Position { get; set; }               // consistSequence内のインデックス
    public required CarConsistId CarConsistId { get; set; }  // Decoupling: 分割後にこの列車が持つ編成
                                                               // Coupling:   併合前にこの列車が持っていた編成
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
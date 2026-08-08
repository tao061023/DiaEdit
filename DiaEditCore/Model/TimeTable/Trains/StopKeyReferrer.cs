namespace DiaEditCore.Model.TimeTable.Trains;

public enum StopKeyReferenceKind
{
    SplitOrigin,
    CouplingPartner
}

/// <summary>あるStopKeyを外部から参照しているTrainと、その参照種別。</summary>
public readonly record struct StopKeyReferrer(TrainId ReferrerTrainId, StopKeyReferenceKind Kind);
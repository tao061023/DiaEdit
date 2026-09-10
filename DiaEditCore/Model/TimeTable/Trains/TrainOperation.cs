namespace DiaEditCore.Model.TimeTable.Trains;

/// <summary>
/// 列車運用を表現する
/// </summary>
public sealed class TrainOperation
{
    /// <summary>
    /// 列車運用識別子
    /// </summary>
    public required TrainOperationId Id { get; set; }
    /// <summary>
    /// 列車運用番号
    /// </summary>
    public required string OperationNumber { get; set; }
}

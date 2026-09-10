namespace DiaEditCore.Model.TimeTable;

/// <summary>
/// 時刻表セットを表現する
/// </summary>
public sealed class TimeTableSet
{
    /// <summary>
    /// 時刻表セット識別子
    /// </summary>
    public required TimeTableSetId Id { get; set; }
    /// <summary>
    /// 時刻表名
    /// </summary>
    public required string Name { get; set; }
    /// <summary>
    /// 所属する列車リスト（表示順）
    /// </summary>
    /// <remarks>
    /// 意図的な空白欄・基準駅ソート等、ユーザーが指定する**管理上の並び順**を保持する単一の正データである。
    /// </remarks>
    public List<TrainId> TrainIds { get; set; } = new();
}
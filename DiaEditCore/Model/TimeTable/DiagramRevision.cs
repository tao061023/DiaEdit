namespace DiaEditCore.Model.TimeTable;

/// <summary>
/// ダイヤ改正1回分のまとまりを表す
/// </summary>
public sealed class DiagramRevision
{
    /// <summary>
    /// ダイヤ改正識別子
    /// </summary>
    public DiagramRevisionId Id { get; set; }
    /// <summary>
    /// ダイヤ改正名
    /// </summary>
    public required string Name;
    /// <summary>
    /// 複製元追跡タグ
    /// </summary>
    public DiagramRevisionId? BaseRevisionId { get; set;}

    /// <summary>
    /// 所属する時刻表セットの識別子
    /// </summary>
    /// <remarks>
    /// null許容。ただし、DiagramRevisionを作成した際に、空のTimeTableSetを作成するという方針もアリ。（Stationと似たような仕組み）（要検討。）
    /// </remarks>
    public List<TimeTableSetId> TimeTableSetIds { get; set; } = new();
    /// <summary>
    /// 基準とする時刻表セットの識別子
    /// </summary>
    public TimeTableSetId? BaseTimeTableSetId;
}
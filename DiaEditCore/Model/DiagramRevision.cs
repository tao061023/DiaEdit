namespace DiaEditCore.Model;

public sealed class DiagramRevision
{
    public DiagramRevisionId Id { get; set; }
    public required string Name;
    public DiagramRevisionId? BaseRevisionId { get; set;} // 複製元の追跡用タグ

    public List<TimeTableSetId> TimeTableSetIds { get; set; } = new(); // null許容。ただし、DiagramRevisionを作成した際に、空のTimeTableSetを作成するという方針もアリ。（Stationと似たような仕組み）（要検討。）
    public TimeTableSetId? baseTimeTableSetId; // 研究用に他社のダイヤを入力する際に基準運転時分が不要となる場合に備え、Optional化。
}
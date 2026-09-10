namespace DiaEditCore.Model.TimeTable.Trains;

/// <summary>
/// 駅の停車情報を表現する
/// </summary>
public sealed class StopTime
{
    /// <summary>
    /// 到着時刻
    /// </summary>
    public int ArrivalSeconds { get; set; } = -1;
    /// <summary>
    /// 発車時刻
    /// </summary>
    public int DepartureSeconds { get; set; } = -1;
    /// <summary>
    /// 停車フラグ
    /// </summary>
    public bool IsStop { get; set; } = false;
    /// <summary>
    /// 客扱い、使用する番線
    /// </summary>
    /// <remarks>
    /// バグの可能性あり。TrackRailIdは設定する必要がある。
    /// </remarks>
    public RailId? TrackRailId { get; set; }
    /// <summary>
    /// このStopTimeで発生する駅作業
    /// </summary>
    public List<StationWork> Works { get; set; } = new();
}
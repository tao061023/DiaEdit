namespace DiaEditCore.Model.Stations;

/// <summary>
/// 駅種別
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description><c>Standard</c>：停車場。在線検知の境界となる。</description></item>
/// <item><description><c>Halt</c>：停留場。在線検知の境界とならない。</description></item>
/// <item><description><c>SignalStation</c>：信号場。在線検知の境界となる。単なる路線分岐点やスイッチバック施設など、客扱いを行わない運行拠点が該当。</description></item>
/// <item><description><c>Depot</c>：車両基地。在線検知の境界となる。駅から車両基地までの間は一つの路線として登録する。</description></item>
/// </list>
/// </remarks>
public enum StationType { Standard, Halt, SignalStation, Depot }

/// <summary>
/// 駅や信号場、車両基地を表現する
/// </summary>
public sealed class Station
{
    /// <summary>
    /// 駅識別子
    /// </summary>
    public required StationId Id { get; set; }
    /// <summary>
    /// 駅名称
    /// </summary>
    public required DisplayName DisplayName { get; set; }
    /// <summary>
    /// 駅種別
    /// </summary>
    public required StationType Type { get; set; }
    /// <summary>
    /// 事業者管理用コード
    /// </summary>
    public string OperatingCode { get; set; } = "";
    /// <summary>
    /// 電報略号
    /// </summary>
    public string TelegraphCode { get; set; } = "";
    /// <summary>
    /// 駅時刻表の対象判別用フラグ
    /// </summary>
    public bool? ShowsInStationTimetableOverride { get; set; }

    /// <summary>
    /// 駅時刻表の対象判別用フラグをデフォルトに切り替えるメソッド
    /// </summary>
    /// <returns>Standard, HaltならTrue、SignalStation, DepotならFalse</returns>
    public bool ResolveShowsInStationTimetable()
    {
        if (ShowsInStationTimetableOverride.HasValue)
            return ShowsInStationTimetableOverride.Value;
        return Type != StationType.SignalStation && Type != StationType.Depot;
    }
}
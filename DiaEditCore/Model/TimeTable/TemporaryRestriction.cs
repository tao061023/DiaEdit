namespace DiaEditCore.Model.TimeTable;

/// <summary>
/// 規制期間を表現する
/// </summary>
/// <param name="Start">規制開始</param>
/// <param name="End">規制終了</param>
public record struct DateRange(DateTime Start, DateTime End);

/// <summary>
/// 規制対象を表現する
/// </summary>
public abstract record RestrictionTarget
{
    /// <summary>
    /// 規制対象を保持する
    /// </summary>
    private RestrictionTarget() { }

    /// <summary>
    /// 規制対象が駅間の場合
    /// </summary>
    /// <param name="StationConnectionSegmentId"></param>
    public record Segment(StationConnectionSegmentId StationConnectionSegmentId) : RestrictionTarget;

    /// <summary>
    /// 規制対象がレールの場合
    /// </summary>
    /// <param name="RailId"></param>
    public record Rail(RailId RailId) : RestrictionTarget;
}

/// <summary>
/// 工事や災害等による規制、時間帯・列車種別・車両形式などの制約を表現する
/// </summary>
/// <param name="Id">規制識別子</param>
/// <param name="Target">規制対象</param>
/// <param name="ExtraRunTimeSec">追加所要時間</param>
/// <param name="SpeedLimitKph">制限速度</param>
/// <param name="DateRange">期間</param>
/// <param name="Note">メモ</param>
/// <remarks>
/// 実装の優先度は低。
/// </remarks>
public record TemporaryRestriction(
    TemporaryRestrictionId Id,
    RestrictionTarget Target,               
    int? ExtraRunTimeSec,
    int? SpeedLimitKph,
    DateRange DateRange,
    string Note
);
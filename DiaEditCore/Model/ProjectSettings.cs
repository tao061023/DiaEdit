namespace DiaEditCore.Model;

/// <summary>
/// 保存時バリデーションの有効/無効・閾値を保持する（5.16節）。
/// プロジェクト単位（路線の特性によって適正値が変わるため）で設定可能とする。
/// ConflictChecker・RunTimeCalculator等のAlgorithm層はこれを参照して警告要否を判断する。
/// </summary>
public record ValidationRules(
    int? MinDwellTimeSec,   // 停車時間の下限
    int? MinHeadwaySec,     // 同一番線・同一区間の最小間隔
    int? MinTurnaroundSec,  // 折り返し余裕時分。乗務員交代を伴う場合も含む。
    int? TrackEntryMarginSec,   // 始発列車〈arrivalSecondsなし〉の進入所要時分相当の余裕。
    int? TrackPassMarginSec,    // 通過列車の前後余裕。対称値とし、通過時刻の前後この秒数ずつをTrack占有期間とみなす。
    bool EnableConflictDetection,
    bool EnableCarLengthCheck
);

public record ProjectSettings(
    ValidationRules ValidationRules
);
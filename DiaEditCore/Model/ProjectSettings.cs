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
    ValidationRules ValidationRules,
    // ダイヤグラム描画の始端（秒、始発時刻の基準・例：4:00=14400）。
    // Track用途ConflictChecker（6.5節）において、PrevTrainが存在しない真の始発列車について、
    // 占有区間の開始をこの値で打ち切る（TrackEntryMarginSecのような発車時刻起点の見込み値ではなく、
    // 「ダイヤグラム自体がここから始まる」という固定境界として扱う。v11.23で新設）。
    int DiagramBasedTimeSec = 14400
);
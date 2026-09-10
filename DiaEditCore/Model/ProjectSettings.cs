namespace DiaEditCore.Model;

/// <summary>
/// 保存時バリデーションの有効/無効・閾値を保持する。
/// ConflictChecker・RunTimeCalculator等のAlgorithm層はこれを参照して警告要否を判断する。
/// </summary>
/// <param name="MinDwellTimeSec">停車時間の下限</param>
/// <param name="MinHeadwaySec">同一番線・同一区間の最小間隔</param>
/// <param name="MinTurnaroundSec">折り返し余裕時分。</param>
/// <param name="TrackEntryMarginSec">始発列車〈arrivalSecondsなし〉の進入所要時分相当の余裕。</param>
/// <param name="TrackPassMarginSec">通過列車の前後余裕。対称値。</param>
/// <param name="EnableConflictDetection">支障検知有効化フラグ</param>
/// <param name="EnableCarLengthCheck">有効長判定有効化フラグ</param>
public record ValidationRules(
    int? MinDwellTimeSec, 
    int? MinHeadwaySec, 
    int? MinTurnaroundSec,
    int? TrackEntryMarginSec, 
    int? TrackPassMarginSec,
    bool EnableConflictDetection,
    bool EnableCarLengthCheck
);
/// <summary>
/// プロジェクト設定。
/// 1プロジェクト1JSON方針における保存ファイルのルート集約オブジェクトProjectFileの一部として保持される。
/// </summary>
/// <param name="ValidationRules">保存時バリデーションの有効/無効・閾値。</param>
/// <param name="DiagramBasedTimeSec">ダイヤグラムの起点時刻</param>
public record ProjectSettings(
    ValidationRules ValidationRules,
    int DiagramBasedTimeSec = 14400
);
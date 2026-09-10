namespace DiaEditCore.Model.TimeTable.Trains;

using DiaEditCore.Model;

/// <summary>
/// 駅作業種別
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description><c>None</c>なし。通常の停車・通過が該当。</description></item>
/// <item><description><c>PrevTrain</c>前列車接続</description></item>
/// <item><description><c>StartOp</c>出区</description></item>
/// <item><description><c>EndOp</c>入区</description></item>
/// <item><description><c>Shunting</c>入換</description></item>
/// <item><description><c>NextTrain</c>次列車接続</description></item>
/// <item><description><c>Coupling</c>増結・併合</description></item>
/// <item><description><c>Decoupling</c>解結・分割</description></item>
/// </list>
/// </remarks>
public enum StationWorkType
{
    None, PrevTrain, StartOp, EndOp, Shunting, NextTrain, Coupling, Decoupling
}

/// <summary>
/// 次列車接続種別
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description><c>Other</c>別列車</description></item>
/// <item><description><c>TypeChange</c>列車種別変更</description></item>
/// <item><description><c>InfoChange</c>列車情報変更</description></item>
/// <item><description><c>SameTrain</c>同列車扱い</description></item>
/// <item><description><c>Coupling</c>増結・併合</description></item>
/// </list>
/// </remarks>
public enum NextTrainType
{
    Other, TypeChange, InfoChange, SameTrain, Coupling,
}

/// <summary>
/// StartOp専用：出区編成単位を表す（1編成ずつ登録する）
/// </summary>
public sealed class StartOpCarSlot
{
    /// <summary>
    /// 編成位置
    /// </summary>
    public required int Position { get; set; }
    /// <summary>
    /// 使用編成の識別子
    /// </summary>
    public required CarCompositionId CarCompositionId { get; set; }
    /// <summary>
    /// 運用番号
    /// </summary>
    public required string OperationNumber { get; set; }
}

/// <summary>
/// Decoupling専用：分割後1グループ分の要素
/// </summary>
public sealed class CutGroupEntry
{
    /// <summary>
    /// 編成の識別子
    /// </summary>
    public required CarCompositionId CarCompositionId { get; set; }
    /// <summary>
    /// 運用番号
    /// </summary>
    public required string OperationNumber { get; set; }
}

/// <summary>
/// 解結・分割作業を表す
/// </summary>
/// <remarks>
/// OperationIdフィールドは意図的に持たない：合流するCarCompositionの運用番号は
/// 合流前の値をそのまま保持するため（CarCompositionに紐づく属性であり、Couplingでは変化しない）。
/// </remarks>
public sealed class DecouplingWork
{
    /// <summary>
    /// 分割後の前側のグループ
    /// </summary>
    /// <remarks>
    /// 分割前の走行方向に対する前側。最低1件必須。
    /// front/rear間でCarCompositionIdの重複は不可。
    /// </remarks>
    public required List<CutGroupEntry> FrontGroup { get; set; }
    /// <summary>
    /// 分割後の後ろ側のグループ
    /// </summary>
    /// <remarks>
    /// 分割前の走行方向に対する後ろ側。最低1件必須。
    /// </remarks>
    public required List<CutGroupEntry> RearGroup { get; set; }

    /// <summary>
    /// 分割後、編成グループの継承対象をどちらにするか決定するフラグ
    /// </summary>
    /// <remarks>
    /// false = front側が基準（自Trainがそのまま継続）。<br/>
    /// true = rear側が基準。
    /// 継続側でないほうが SplitOriginRef 経由の新Trainとして生まれる。
    /// </remarks>
    public bool IsRearBase { get; set; } = false;
}

/// <summary>
/// NextTrain.Coupling専用：相手Trainへの参照型
/// </summary>
public sealed class CouplingWork
{
    /// <summary>
    /// 連結する（しに行く）相手Trainの識別子
    /// </summary>
    public required TrainId PartnerTrainId { get; set; }
    /// <summary>
    /// 連結する（しに行く）相手TrainのStopKey
    /// </summary>
    public required StopKey PartnerStopKey { get; set; }

    /// <summary>
    /// 編成の連結位置
    /// </summary>
    /// <remarks>
    /// 相手編成の駅到着前の進行方向に準ずる。<br/>
    /// false = 相手編成の後ろに連結。true = 相手編成の前に連結。
    /// </remarks>
    public bool AttachToFront { get; set; } = false;

}

/// <summary>
/// PrevTrain専用：直前Trainから引き継いだCarCompositionのうち運用を変更するものだけの差分リスト。
/// </summary>
/// <remarks>
/// 省略時（＝該当CarCompositionIdがリストに現れない場合）＝全Composition継承。
/// </remarks>
public sealed class PrevTrainOperationOverride
{
    /// <summary>
    /// 編成の識別子
    /// </summary>
    public required CarCompositionId CarCompositionId { get; set; }
    /// <summary>
    /// 新規運用番号
    /// </summary>
    public required string NewOpNumber { get; set; }
}

/// <summary>
/// Decouplingで生じたTrain専用：自身の起点を示す
/// </summary>
/// <remarks>
/// GroupIndexは持たない：どちらのグループ（front/rear）を引き継いだかはDecouplingWork.IsRearBaseを
/// 直読みすれば一意に決まるため。
/// </remarks>
public sealed class SplitOriginRef
{
    /// <summary>
    /// 分割元のTrainの識別子
    /// </summary>
    public required TrainId OriginTrainId { get; set; }
    /// <summary>
    /// 分割元のTrainのStopKey
    /// </summary>
    public required StopKey OriginStopKey { get; set; }
}

/// <summary>
/// 駅作業を表す
/// </summary>
public sealed class StationWork
{
    /// <summary>
    /// 駅作業種別
    /// </summary>
    public required StationWorkType Type { get; set; }

    /// <summary>
    /// StartOp専用：出区編成リスト
    /// </summary>
    public List<StartOpCarSlot> StartOpConsist { get; set; } = new();

    /// <summary>
    /// PrevTrain専用：運用番号更新リスト
    /// </summary>
    public List<PrevTrainOperationOverride> PrevTrainOperationOverrides { get; set; } = new();

    /// <summary>
    /// Decoupling専用：解結・分割作業を表す
    /// </summary>
    public DecouplingWork? DecouplingDetail { get; set; }
    
    /// <summary>
    /// Coupling専用：相手Trainへの参照型
    /// </summary>
    public CouplingWork? CouplingDetail { get; set; }

    /// <summary>
    /// Decouplingで生じたTrain専用（PrevTrain）：自身の起点を示す
    /// </summary>
    public SplitOriginRef? SplitOrigin { get; set; }

    /// <summary>
    /// 次列車接続種別
    /// </summary>
    public NextTrainType? NextTrainType { get; set; }

    /// <summary>
    /// 入替作業で利用する構内進路の識別子
    /// </summary>
    public StationPathId? StationPathId { get; set; }

    /// <summary>
    /// 作業開始時刻
    /// </summary>
    /// <remarks>
    /// -1 は未設定。
    /// </remarks>
    public int StartOpSeconds { get; set; } = -1;

    /// <summary>
    /// 作業終了時刻
    /// </summary>
    /// <remarks>
    /// -1 は未設定。
    /// </remarks>
    public int EndOpSeconds { get; set; } = -1;
}
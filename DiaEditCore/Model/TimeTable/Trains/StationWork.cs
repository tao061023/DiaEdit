namespace DiaEditCore.Model.TimeTable.Trains;

using DiaEditCore.Model;

public enum StationWorkType
{
    None, PrevTrain, StartOp, EndOp, Shunting, NextTrain, Coupling, Decoupling
}

public enum NextTrainType
{
    Other, TypeChange, InfoChange, SameTrain,
    Coupling, // 次に発車する列車が本Trainの一部として併合される関係（Conflict除外の判定キー。6.5節ConflictFilter参照）
}

// StartOp専用：出区時点のconsistSequence内1要素。
public sealed class StartOpCarSlot
{
    public required int Position { get; set; }
    public required CarCompositionId CarCompositionId { get; set; }
    public required string OperationNumber { get; set; }
}

// vNEXT改訂：Coupling/Decoupling共通で使っていた CutGroup（GroupIndexフラット配列）を廃止し、
// front/rear 2バケット構造（DecouplingWork）と参照型（CouplingWork）に分離した。
// 理由（セッション議事）：
//   - 実例調査の結果、分割は必ず「前グループ／後グループ」の2分割にしかならないことが判明した
//     （3グループ以上の同時分割は発生しない）ため、N分割を許容する型は構造的に過大だった。
//   - Couplingは「自編成へ相手Train 1本をまるごと連結する」運用のみが確認されており、
//     相手編成の一部だけを連結する運用は存在しない（運用フロー側で「先に解結してから連結する」
//     ことで対応するため、データモデル側で部分連結を表現する必要がない）。
//   - 相手Trainの中身（CarCompositionId一覧）は CarConsistResolver.ResolveConsistAt で
//     相手Train側を再帰的に解決すれば都度導出できるため、Coupling側は「相手Trainへの参照」の
//     みを持てば足りる（discard-and-regenerate原則。CutGroup.TrainId廃止時と同じ理屈）。

// Decoupling専用：分割後の1グループ分の要素。
public sealed class CutGroupEntry
{
    public required CarCompositionId CarCompositionId { get; set; }
    public required string OperationNumber { get; set; }
}

public sealed class DecouplingWork
{
    // 分割前の走行方向に対する前側／後側。両方とも最低1件必須（Rule 8）。
    // front/rear間でCarCompositionIdの重複は不可（Rule 7、同一編成が両側に属することは物理的に不可能）。
    public required List<CutGroupEntry> FrontGroup { get; set; }
    public required List<CutGroupEntry> RearGroup { get; set; }

    // false = front側が基準（自Trainがそのまま継続）。true = rear側が基準。
    // 継続側でないほうが SplitOriginRef 経由の新Trainとして生まれる。
    public bool IsRearBase { get; set; } = false;
}

public sealed class CouplingWork
{
    // 連結される相手Train・その時点のStopKey（相手編成の中身はResolveConsistAtで都度導出、非保存）。
    public required TrainId PartnerTrainId { get; set; }
    public required StopKey PartnerStopKey { get; set; }

    // false = 自編成の後ろに連結。true = 自編成の前に連結。
    public bool AttachToFront { get; set; } = false;

    // OperationIdフィールドは意図的に持たない：合流するCarCompositionの運用番号は
    // 合流前の値をそのまま保持するため（CarCompositionに紐づく属性であり、Couplingでは変化しない）。
}

// PrevTrain専用：直前Trainから引き継いだCarCompositionのうち運用を変更するものだけの差分リスト。
// 省略時（＝該当CarCompositionIdがリストに現れない場合）＝全Composition継承。
public sealed class PrevTrainOperationOverride
{
    public required CarCompositionId CarCompositionId { get; set; }
    public required string NewOpNumber { get; set; }
}

// 分割で生じた新Train側が、自身の起点を示す。
// GroupIndexは持たない：どちらのグループ（front/rear）を引き継いだかはDecouplingWork.IsRearBaseを
// 直読みすれば一意に決まるため、SplitGroupAssignmentResolverによる推定（旧two-pointer方式）は不要になった。
public sealed class SplitOriginRef
{
    public required TrainId OriginTrainId { get; set; }
    public required StopKey OriginStopKey { get; set; }
}

public sealed class StationWork
{
    public required StationWorkType Type { get; set; }

    public List<StartOpCarSlot> StartOpConsist { get; set; } = new();               // StartOpのみ
    public List<PrevTrainOperationOverride> PrevTrainOperationOverrides { get; set; } = new(); // PrevTrainのみ

    // vNEXT改訂：CutGroups（List<CutGroup>）を廃止し、Type別に排他のDecouplingDetail/CouplingDetailへ分離。
    public DecouplingWork? DecouplingDetail { get; set; }   // Decouplingのみ
    public CouplingWork? CouplingDetail { get; set; }       // Couplingのみ

    public SplitOriginRef? SplitOrigin { get; set; }       // 分割由来の新Train先頭StopTimeが持つ（PrevTrainのみ）
    public NextTrainType? NextTrainType { get; set; }      // NextTrainのみ
    public StationPathId? StationPathId { get; set; }      // Shunting等

    public int StartOpSeconds { get; set; } = -1;
    public int EndOpSeconds { get; set; } = -1;
}
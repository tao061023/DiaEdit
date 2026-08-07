namespace DiaEditCore.Model.TimeTable;

using DiaEditCore.Model;

public enum StationWorkType
{
    None, PrevTrain, StartOp, EndOp, Shunting, NextTrain, Coupling, Decoupling
}

public enum NextTrainType
{
    Other, TypeChange, InfoChange, SameTrain,
    Coupling, // 新設。次に発車する列車が本Trainの一部として併合される関係（Conflict除外の判定キー。6.5節ConflictFilter参照）
}

// 確定運用への参照／未確定の仮ラベルを型で区別する（判別共用体、ObjectId.csの命名規約に合わせフラットなsealed recordで表現）
public abstract record OperationRef;
public sealed record ResolvedOperationRef(TrainOperationId Id) : OperationRef;
public sealed record ProvisionalOperationRef(string Label) : OperationRef;

// StartOp専用：出区時点のconsistSequence内1要素。
public sealed class StartOpCarSlot
{
    public required int Position { get; set; }
    public required CarCompositionId CarCompositionId { get; set; }

    // 必須。旧OperationNumber(string)から変更。空文字列によるStationWork.TrainOperationId継承フォールバックは廃止
    // （Composition単位が正データになったため、Train単位への継承は不要）。
    public required OperationRef OperationId { get; set; }
}

// Coupling/Decoupling専用：分割後の集合（旧TrainCutPointを再構成）。
// TrainIdフィールドは廃止：Coupling側の相手Train特定はTrainConnectionResolver（6.4節）が
// NextTrainType.Couplingとして導出する非永続情報に委ね、CutGroup自体には保持しない
// （discard-and-regenerate原則。参照: セッション議事、MergeToRefは正データとして不採用と決定）。
public sealed class CutGroup
{
    // 0始まり連番。分割前の走行方向に対する前方からの順。同一StationWork内で重複禁止（保存時検証、要Validator実装）。
    public required int GroupIndex { get; set; }
    public required CarCompositionId CarCompositionId { get; set; }

    // Decoupling: 必須（Resolved/Provisional問わず）。Rule 5改訂を参照。
    // Coupling:   ProvisionalOperationRefのみ許容（履歴の自由記述用途。実在チェック対象外）。
    public required OperationRef OperationId { get; set; }
}

// PrevTrain専用：直前Trainから引き継いだCarCompositionのうち運用を変更するものだけの差分リスト。
// 省略時（＝該当CarCompositionIdがリストに現れない場合）＝全Composition継承。
public sealed class PrevTrainOperationOverride
{
    public required CarCompositionId CarCompositionId { get; set; }

    // 変更後は必ずResolved（新運用への切替のため仮ラベルは想定しない）。
    public required TrainOperationId NewOperationId { get; set; }
}

// 分割で生じた新Train側が、自身の起点を示す（新設）。
// GroupIndexは持たない（v11.44改訂セッションで削除確定）：TrainConnectionResolver（6.4節）の
// PrevTrain/NextTrain導出は「1出発列車＝最大1到着列車」の一意マッチングを前提としており、
// 1到着列車→複数出発列車という分割そのものを表現できないため、OriginTrainId/OriginStopKeyという
// 「どのDecoupling由来か」の紐付けだけは明示データとして残す。一方、どのCutGroup（GroupIndex）を
// 引き継いだかは、以下の前提のもとCarConsistResolver（6.7節）側でtwo-pointer方式により導出できる：
//   - ランナウンド線はスコープ外（中間グループを飛び越して先に引き出すことはできない＝
//     残存編成は常に両端からしか出し入れできないdeque構造）
//   - 分割された編成が同時に動き出すことは信号システム上あり得ない（発車時刻は常に一意に順序付け可能）
// 導出アルゴリズム：(OriginTrainId, OriginStopKey)が一致する兄弟Train群を発車時刻昇順に並べ、
// 各Trainの経路にShunting（StationWorkType.Shunting、直前の継続方向に対する入替・反転の有無を示す
// 明示データ）が含まれるかどうかで、CutGroupsの手前側(lo)／奥側(hi)いずれから引き当てるかを判定する
// （2ポインタ：Shuntingなし→lo側から順に、Shuntingあり→hi側から順に）。
// 「兄弟Train間で発車時刻が重複してはならない」「two-pointer割当がCutGroups数と整合するか」は
// 単一StationWork内で完結しない横断検証のため、SplitOriginCrossValidator（新設予定、6.12.1節の
// TrainOperationCrossValidatorと同型）側の責務とする。
public sealed class SplitOriginRef
{
    public required TrainId OriginTrainId { get; set; }
    public required StopKey OriginStopKey { get; set; }
}

public sealed class StationWork
{
    public required StationWorkType Type { get; set; }

    public List<StartOpCarSlot> StartOpConsist { get; set; } = new();               // StartOpのみ
    public List<CutGroup> CutGroups { get; set; } = new();                          // Coupling/Decouplingのみ
    public List<PrevTrainOperationOverride> PrevTrainOperationOverrides { get; set; } = new(); // PrevTrainのみ（省略時＝全Composition継承）

    public SplitOriginRef? SplitOrigin { get; set; }       // 新設。分割由来の新Train先頭StopTimeが持つ
    public NextTrainType? NextTrainType { get; set; }      // NextTrainのみ
    public StationPathId? StationPathId { get; set; }      // Shunting等

    public int StartOpSeconds { get; set; } = -1;
    public int EndOpSeconds { get; set; } = -1;

    // TrainOperationId フィールドは廃止（StartOp: StartOpCarSlot.OperationId、PrevTrain: PrevTrainOperationOverridesへ分散）
}
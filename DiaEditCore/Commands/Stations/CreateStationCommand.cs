namespace DiaEditCore.Commands.Stations;

using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Session;

/// <summary>
/// 「新規登録（Create）」パターンの最初の具象実装。
///
/// ID採番方針：セッション中は既存Stationの最大IdValue+1を単純採番する
/// （欠番は詰めない）。Undo/Redoスタックが生きている間はIDの安定性を優先するため。
/// 欠番を詰める再採番（コンパクション）はプロジェクト読込時（Undoスタックがまだ存在しない
/// タイミング）に限定して行う方針とし、別タスクとして切り出す。
///
/// AffectedIdsは「新規オブジェクトは他から参照されないため空」。
///
/// TTargetは追加先のList&lt;Station&gt;そのもの（ProjectFile.Stations等、呼び出し元が渡す）。
/// CaptureSnapshotは「実行前は存在しない」ことを表すためnullを返し、Restoreでは
/// Apply時に生成したインスタンスをリストから除去する（属性変更パターンと異なり、
/// スナップショットは「値の複製」ではなく「実行前に存在しなかった」という事実そのもの）。
/// </summary>
public sealed class CreateStationCommand : UndoableCommand<List<Station>, Station?>
{
    private readonly IdAllocator<StationId> _idAllocator;
    private readonly DisplayName _displayName;
    private readonly StationType _type;
    private readonly string _operatingCode;
    private readonly string _telegraphCode;

    /// <summary>
    /// Execute()実行後、生成されたStationを呼び出し元（ViewModel/UI層）が参照するためのプロパティ。
    /// AffectedIdsが空集合のため、生成結果をUIへ伝える手段として別途公開する。
    /// </summary>
    public Station? Created { get; private set; }

    public CreateStationCommand(
        List<Station> stations,
        IdAllocator<StationId> idAllocator,
        DisplayName displayName,
        StationType type,
        string operatingCode = "",
        string telegraphCode = "")
        : base(stations, new HashSet<ObjectId>()) // 新規登録：AffectedIdsは対象自身のみ（空集合）
    {
        _idAllocator = idAllocator;
        _displayName = displayName.Clone();
        _type = type;
        _operatingCode = operatingCode;
        _telegraphCode = telegraphCode;
    }

    protected override IReadOnlySet<ObjectId> ComputeAffectedIdsAfterApply(List<Station> target) =>
        Created is not null
            ? new HashSet<ObjectId> { new StationObjectId(Created.Id) }
            : new HashSet<ObjectId>();

    protected override Station? CaptureSnapshot(List<Station> target) => null;

    protected override void Apply(List<Station> target)
    {
        if (Created is not null)
        {
            // Redo経路：CommandInvoker.Redo()はExecute()と同一パスを通るため、
            // ここが「2回目以降のApply呼び出し」＝Redoであることの唯一の判定材料になる。
            // 初回Execute時に生成・保持したインスタンスをそのまま再挿入することで、
            // ChangeStationAttributesCommand等が保持する直接参照との同一性を保つ（§9.1項目23）。
            // AllocateNextIdは呼び直さない：呼び直すとUndo/Redoの往復で別Idを持つ別インスタンスが
            // 生まれ、参照同一性の問題が再発する。
            target.Add(Created);
            return;
        }

        Created = new Station
        {
            Id = _idAllocator.AllocateNext(), // ← target.Max()+1 を廃止
            DisplayName = _displayName.Clone(),
            Type = _type,
            OperatingCode = _operatingCode,
            TelegraphCode = _telegraphCode
        };
        target.Add(Created);
    }

    protected override void Restore(List<Station> target, Station? snapshot)
    {
        if (Created is not null)
        {
            target.Remove(Created);
        }
    }
}
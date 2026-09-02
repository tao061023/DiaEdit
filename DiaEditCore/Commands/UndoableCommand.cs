namespace DiaEditCore.Commands;

using DiaEditCore.Model;

/// <summary>
/// CommandInvoker（呼び出し元）がコマンドの型引数を意識せずUndo/Redoスタックへ積めるようにするための
/// 非ジェネリック契約。UndoableCommand&lt;TTarget, TSnapshot&gt;はこれを実装する。
/// </summary>
public interface IUndoableCommand
{
    IReadOnlySet<ObjectId> Execute();
    IReadOnlySet<ObjectId> Undo();
}

/// <summary>
/// 6.11節：Undo/Redo可能なコマンドの基底型。
///
/// 確定した設計方針（v11.39、Composition層セッション）：
///   - 論点M：スナップショット方式を採用。Execute()前に対象オブジェクトの状態をまるごと複製し、
///     Undo()はその複製を書き戻すだけにする（逆操作を個別に書く方式は採用しない）。
///     「複製元＝正」という単純な構造にすることで、コマンドの種類が増えても事故が起きにくい。
///   - 論点N：AffectedIds（影響を受けるObjectIdの集合）は、DependencyResolver（§6.11、未実装）
///     による自動計算ではなく、コマンド実装者がコンストラクタで手動列挙する。将来
///     DependencyResolverができた場合も、この基底型のシグネチャは変えずに済む
///     （具象コマンド側でAffectedIdsの構築ロジックだけ差し替えればよい）。
///   - 論点O：Execute()/Undo()はaffectedIdsを返すだけの薄い型とし、ICacheChangeObserverへの
///     通知責務は持たない（単体テストでObserverのモックが常に必要になることを避けるため）。
///     通知はCommandInvoker（呼び出し元）が担う。
///
/// 型引数：
///   TTarget   ：このコマンドが変更する対象オブジェクトの型（例：Train、StationConnection）。
///   TSnapshot ：TTargetの状態を複製したスナップショットの型。
///               不変な値（record等）にして、CaptureSnapshot後にTargetを変更してもスナップショット
///               自体が影響を受けないようにすること（参照をそのまま持ち回すと複製の意味が無くなる）。
/// </summary>
public abstract class UndoableCommand<TTarget, TSnapshot> : IUndoableCommand
{
    private TSnapshot? _before;
    private bool _executed;
    private IReadOnlySet<ObjectId> _frozenAffectedIds;

    protected TTarget Target { get; }

    public IReadOnlySet<ObjectId> AffectedIds { get; }

    protected UndoableCommand(TTarget target, IReadOnlySet<ObjectId> affectedIds)
    {
        Target = target;
        AffectedIds = affectedIds;
        _frozenAffectedIds = affectedIds;
    }

    protected abstract TSnapshot CaptureSnapshot(TTarget target);
    protected abstract void Apply(TTarget target);
    protected abstract void Restore(TTarget target, TSnapshot snapshot);

    /// <summary>
    /// Execute()（Apply直後）で呼ばれ、以降Undo()が返すAffectedIdsを確定・凍結する。
    /// 既定はコンストラクタ渡しのAffectedIdsをそのまま返す（既存の全コマンドと同じ挙動、
    /// 後方互換）。Create系コマンドなど、Apply()完了までIdが定まらずコンストラクタ時点では
    /// 自身のObjectIdを含められないケースのみオーバーライドする（§9.1項目23関連の追加対応）。
    /// </summary>
    protected virtual IReadOnlySet<ObjectId> ComputeAffectedIdsAfterApply(TTarget target) => AffectedIds;

    public IReadOnlySet<ObjectId> Execute()
    {
        _before = CaptureSnapshot(Target);
        Apply(Target);
        _frozenAffectedIds = ComputeAffectedIdsAfterApply(Target);
        _executed = true;
        return _frozenAffectedIds;
    }

    public IReadOnlySet<ObjectId> Undo()
    {
        if (!_executed)
            throw new InvalidOperationException($"{GetType().Name}: Execute()より前にUndo()が呼ばれた");

        Restore(Target, _before!);
        return _frozenAffectedIds; // 再計算せず、直近のExecute()で確定した値を再利用
    }
}
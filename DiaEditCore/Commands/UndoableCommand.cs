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

    protected TTarget Target { get; }

    /// <summary>このコマンドの実行によって影響を受けるObjectIdの集合（論点N：手動列挙）。</summary>
    public IReadOnlySet<ObjectId> AffectedIds { get; }

    protected UndoableCommand(TTarget target, IReadOnlySet<ObjectId> affectedIds)
    {
        Target = target;
        AffectedIds = affectedIds;
    }

    /// <summary>Targetの現在の状態を複製したスナップショットを返す。Apply()より前に必ず呼ばれる。</summary>
    protected abstract TSnapshot CaptureSnapshot(TTarget target);

    /// <summary>Targetへ実際の変更を適用する。</summary>
    protected abstract void Apply(TTarget target);

    /// <summary>スナップショットの内容をTargetへ書き戻す（Undo本体）。</summary>
    protected abstract void Restore(TTarget target, TSnapshot snapshot);

    /// <summary>
    /// コマンドを実行する。CaptureSnapshot→Applyの順に呼ぶ。
    /// 戻り値はAffectedIds（呼び出し元CommandInvokerが通知に使う）。
    /// </summary>
    public IReadOnlySet<ObjectId> Execute()
    {
        _before = CaptureSnapshot(Target);
        Apply(Target);
        _executed = true;
        return AffectedIds;
    }

    /// <summary>
    /// コマンドを取り消す。Execute()未実行の状態で呼ぶとInvalidOperationExceptionになる
    /// （CommandInvoker側がUndoスタックに積まれたコマンドのみUndo対象とするため、通常この例外は
    /// 発生しないはずだが、コマンド単体を誤って直接Undo()した場合の構造的な誤用検知として残す）。
    /// </summary>
    public IReadOnlySet<ObjectId> Undo()
    {
        if (!_executed)
            throw new InvalidOperationException($"{GetType().Name}: Execute()より前にUndo()が呼ばれた");

        Restore(Target, _before!);
        return AffectedIds;
    }
}

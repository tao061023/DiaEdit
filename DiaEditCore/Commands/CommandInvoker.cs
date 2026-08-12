namespace DiaEditCore.Commands;

using DiaEditCore.ChangeNotification;
using DiaEditCore.Model;

/// <summary>
/// UndoableCommandの実行・取り消し・やり直しを管理し、ICacheChangeObserverへの通知を担う
/// （6.11節・7.3節「変更通知の流れ」）。
///
/// Composition層での登録単位：Singleton想定（アプリ全体でUndo/Redo履歴・購読者一覧を共有するため）。
/// DiaEditApp.ViewModels側のChangeNotificationBridgeが、コンストラクタでこのCommandInvokerに
/// Subscribeすることで通知を受け取る（論点K：ChangeNotificationBridge自体もSingleton想定）。
/// </summary>
public sealed class CommandInvoker
{
    private readonly Stack<IUndoableCommand> _undoStack = new();
    private readonly Stack<IUndoableCommand> _redoStack = new();
    private readonly HashSet<ICacheChangeObserver> _observers = new();

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public void Subscribe(ICacheChangeObserver observer) => _observers.Add(observer);
    public void Unsubscribe(ICacheChangeObserver observer) => _observers.Remove(observer);

    /// <summary>
    /// コマンドを実行し、Undoスタックに積む。新規コマンド実行によりRedo履歴は破棄される
    /// （Undo→別の変更、という操作列でRedoが無効になるのは一般的なエディタの挙動を踏襲）。
    /// </summary>
    public void Execute(IUndoableCommand command)
    {
        var affectedIds = command.Execute();
        _undoStack.Push(command);
        _redoStack.Clear();
        Notify(affectedIds);
    }

    public void Undo()
    {
        if (!CanUndo) return;

        var command = _undoStack.Pop();
        var affectedIds = command.Undo();
        _redoStack.Push(command);
        Notify(affectedIds);
    }

    public void Redo()
    {
        if (!CanRedo) return;

        var command = _redoStack.Pop();
        var affectedIds = command.Execute();
        _undoStack.Push(command);
        Notify(affectedIds);
    }

    private void Notify(IReadOnlySet<ObjectId> affectedIds)
    {
        foreach (var observer in _observers)
            observer.OnChanged(affectedIds);
    }
}

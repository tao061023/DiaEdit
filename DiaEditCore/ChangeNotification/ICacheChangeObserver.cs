namespace DiaEditCore.ChangeNotification;

using DiaEditCore.Model;

/// <summary>
/// UndoableCommand.Execute()/Undo()実行後、影響を受けたObjectIdの集合を受け取る通知インターフェース
/// （7.3節「変更通知の流れ」）。DiaEditCoreはこのインターフェースを公開するのみで、
/// 実装（購読側、DiaEditApp.ViewModels側のChangeNotificationBridge）が誰かを一切知らない
/// （DIPを満たす形でApp側との依存を切る）。
/// </summary>
public interface ICacheChangeObserver
{
    void OnChanged(IReadOnlySet<ObjectId> affectedIds);
}

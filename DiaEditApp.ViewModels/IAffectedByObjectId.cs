namespace DiaEditApp.ViewModels;

using DiaEditCore.Model;

/// <summary>
/// ChangeNotificationBridgeへ自身を登録するViewModelが実装するマーカーインターフェース（7.3節）。
/// ObservedIdsに含まれるObjectIdがaffectedIdsに含まれていた場合のみ、OnAffected()が呼ばれる。
///
/// 論点L（購読解除タイミング、v11.39確定）：ViewModelはIDisposableも実装し、Dispose()内で
/// ChangeNotificationBridge.Unsubscribe(this)を呼ぶ規約とする。View側（DiaEditApp、Avalonia依存が
/// あってよい層）がDataContext解除時に(DataContext as IDisposable)?.Dispose()を呼ぶことで、
/// Avalonia依存をDiaEditApp.ViewModels（Avalonia非依存）に持ち込まずに解放タイミングを確定できる。
/// </summary>
public interface IAffectedByObjectId
{
    IReadOnlySet<ObjectId> ObservedIds { get; }

    void OnAffected();
}

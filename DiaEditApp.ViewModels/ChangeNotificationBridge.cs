namespace DiaEditApp.ViewModels;

using DiaEditCore.ChangeNotification;
using DiaEditCore.Model;

/// <summary>
/// CommandInvoker（DiaEditCore側）からの通知（ICacheChangeObserver）を受け取り、
/// affectedIdsに含まれるObjectIdを購読しているViewModel（IAffectedByObjectId）のOnAffected()を
/// 呼び出すだけの薄いディスパッチャ（7.3節「変更通知の流れ」）。
///
/// Composition層での登録単位：Singleton（アプリ全体で1つ。CommandInvokerと同じライフタイム）。
/// DiaEditCoreは本クラスを一切参照しない（依存の向きはApp側→Core側のみ、DIPを維持）。
///
/// 「ObjectId → 購読中ViewModelの一覧」を管理する構造上、同じViewModelインスタンスの多重登録
/// （Subscribe呼び出しの重複）を防ぐため、内部でHashSet&lt;IAffectedByObjectId&gt;として保持する。
/// </summary>
public sealed class ChangeNotificationBridge : ICacheChangeObserver
{
    private readonly HashSet<IAffectedByObjectId> _subscribers = new();

    public void Subscribe(IAffectedByObjectId subscriber) => _subscribers.Add(subscriber);

    public void Unsubscribe(IAffectedByObjectId subscriber) => _subscribers.Remove(subscriber);

    public void OnChanged(IReadOnlySet<ObjectId> affectedIds)
    {
        // 通知中にOnAffected()側がSubscribe/Unsubscribeを行う可能性があるため、
        // _subscribersのスナップショットを取ってから走査する（列挙中変更によるInvalidOperationException回避）
        foreach (var subscriber in _subscribers.ToArray())
        {
            if (subscriber.ObservedIds.Overlaps(affectedIds))
                subscriber.OnAffected();
        }
    }
}

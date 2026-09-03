namespace DiaEditApp.ViewModels.Composition;

using Microsoft.Extensions.DependencyInjection;

using DiaEditApp.ViewModels.Stations;

using DiaEditCore.ChangeNotification;

/// <summary>
/// DiaEditApp.ViewModels側のDIコンテナ登録（7.3節）。
/// DiaEditCore.Composition.CoreServiceCollectionExtensions.AddDiaEditCore()の後に呼ぶ想定
/// （ChangeNotificationBridgeの登録がCommandInvokerに依存するため）。
/// </summary>
public static class ViewModelServiceCollectionExtensions
{
    public static IServiceCollection AddDiaEditAppViewModels(this IServiceCollection services)
    {
        // ChangeNotificationBridgeはCommandInvokerと同じくアプリ全体で1つ（論点K）。
        // ICacheChangeObserverとしての解決要求（CommandInvoker.Subscribe呼び出し側が使う）と、
        // ChangeNotificationBridge具象型としての解決要求（各ViewModelがSubscribe/Unsubscribeを
        // 呼ぶ側で使う）の両方が同一インスタンスを指すよう、具象型で登録したうえで
        // インターフェースはファクトリ経由で同じインスタンスを返す。
        services.AddSingleton<ChangeNotificationBridge>();
        services.AddSingleton<ICacheChangeObserver>(sp => sp.GetRequiredService<ChangeNotificationBridge>());

        // 画面ViewModelは開くたびに新規生成・閉じたら破棄するため、Transientで登録する（論点K）。
        // MainViewModelは現時点でIAffectedByObjectId未実装（監視対象を持たないスキャフォールドのため）。
        services.AddTransient<MainViewModel>();

        // M2-2：StationListViewModelはProjectSession／CommandInvokerをそのままコンストラクタ注入
        // されるプレーンな型のため、ファクトリラムダは不要（両者は既にAddDiaEditCore()側でSingleton
        // 登録済みという前提。DI登録側にロジックを持ち込まない案C、前回セッションでの合意）。
        services.AddTransient<StationListViewModel>();

        return services;
    }
}
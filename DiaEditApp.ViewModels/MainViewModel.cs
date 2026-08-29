namespace DiaEditApp.ViewModels;

using System;
using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using DiaEditApp.ViewModels.Navigation;
using DiaEditApp.ViewModels.Stations;

/// <summary>
/// M2-1：ナビゲーションツリー最小実装。UI設計書4.1節のツリー構造のうち、
/// 「駅／駅一覧」ノードのみ実ノード化し、他カテゴリは空のフォルダノードとする。
///
/// IServiceProviderをそのまま保持する点について：MainViewModelはアプリのルートVMであり、
/// 「選択されたノードに応じて画面ViewModelを都度DIコンテナから解決する」というコンテンツ
/// スイッチング責務そのものがサービスロケーションを本質的に必要とするため、ここに限り許容する
/// （個別の画面ViewModel側がIServiceProviderを持ち回ることはしない）。
/// </summary>
public partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly IServiceProvider _serviceProvider;

    public ObservableCollection<NavigationNodeViewModel> RootNodes { get; }

    [ObservableProperty]
    public partial NavigationNodeViewModel? SelectedNode { get; set; }

    [ObservableProperty]
    public partial ViewModelBase? CurrentContent { get; set; }

    public MainViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;

        RootNodes = new ObservableCollection<NavigationNodeViewModel>
        {
            NavigationNodeViewModel.Folder("駅",
                NavigationNodeViewModel.Leaf("駅一覧", sp => sp.GetRequiredServiceViewModel<StationListViewModel>())),
            NavigationNodeViewModel.Folder("路線"),
            NavigationNodeViewModel.Folder("車両"),
            NavigationNodeViewModel.Folder("時刻表"),
            NavigationNodeViewModel.Folder("お気に入り"),
        };
    }

    partial void OnSelectedNodeChanged(NavigationNodeViewModel? value)
    {
        // 前のコンテンツはIAffectedByObjectId/Disposeの規約（§7.3 論点L）に従い破棄する。
        (CurrentContent as IDisposable)?.Dispose();

        CurrentContent = value is { IsLeaf: true, ContentFactory: not null }
            ? value.ContentFactory(_serviceProvider)
            : null;
    }

    public void Dispose()
    {
        (CurrentContent as IDisposable)?.Dispose();
    }
}

file static class ServiceProviderExtensions
{
    // Microsoft.Extensions.DependencyInjection.GetRequiredService<T>()への薄いエイリアス。
    // 未登録時は例外を送出させ、サイレントにnullへフォールバックしないようにする。
    public static T GetRequiredServiceViewModel<T>(this IServiceProvider sp) where T : notnull
        => Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<T>(sp);
}
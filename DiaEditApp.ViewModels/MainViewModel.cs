namespace DiaEditApp.ViewModels;

using System;
using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.DependencyInjection;

using DiaEditApp.ViewModels.Navigation;
using DiaEditApp.ViewModels.Stations;

using DiaEditCore.ChangeNotification;
using DiaEditCore.Commands;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations;

/// <summary>
/// M2-1：ナビゲーションツリー最小実装。UI設計書4.1節のツリー構造のうち、
/// 「駅／駅一覧」ノードのみ実ノード化し、他カテゴリは空のフォルダノードとする。
///
/// IServiceProviderをそのまま保持する点について：MainViewModelはアプリのルートVMであり、
/// 「選択されたノードに応じて画面ViewModelを都度DIコンテナから解決する」というコンテンツ
/// スイッチング責務そのものがサービスロケーションを本質的に必要とするため、ここに限り許容する
/// （個別の画面ViewModel側がIServiceProviderを持ち回ることはしない）。
///
/// M2-5：Undo/Redo最小UI。CommandInvokerはCanUndo/CanRedoの変更通知を持たないプレーンな
/// クラスのため、既存のICacheChangeObserver通知経路（Execute/Undo/Redo実行のたびにNotifyされる）
/// に相乗りしてCanUndo/CanRedoを再評価する。affectedIdsの中身自体は使わない
/// （ProjectSessionのdirty化と同じ「とりあえず全部見直す」方針、discard-and-regenerateの精神を踏襲）。
/// </summary>
public partial class MainViewModel : ViewModelBase, ICacheChangeObserver, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly CommandInvoker _invoker;

    public ObservableCollection<NavigationNodeViewModel> RootNodes { get; }

    [ObservableProperty]
    public partial NavigationNodeViewModel? SelectedNode { get; set; }

    [ObservableProperty]
    public partial ViewModelBase? CurrentContent { get; set; }

    public MainViewModel(IServiceProvider serviceProvider, CommandInvoker invoker)
    {
        _serviceProvider = serviceProvider;
        _invoker = invoker;
        _invoker.Subscribe(this);

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

    partial void OnSelectedNodeChanged(NavigationNodeViewModel? value) => ShowContentForSelectedNode();

    /// <summary>
    /// SelectedNodeのContentFactoryからCurrentContentを再生成する。M2-3：StationDetailViewModel
    /// のGoBackコールバックとしても使う（「一覧に戻る」＝選択中ノードの内容を作り直すのと等価）。
    /// </summary>
    private void ShowContentForSelectedNode()
    {
        // 前のコンテンツはIAffectedByObjectId/Disposeの規約（§7.3 論点L）に従い破棄する。
        (CurrentContent as IDisposable)?.Dispose();

        var content = SelectedNode is { IsLeaf: true, ContentFactory: not null }
            ? SelectedNode.ContentFactory(_serviceProvider)
            : null;

        WireUpNavigation(content);
        CurrentContent = content;
    }

    /// <summary>
    /// M2-3：StationListViewModel.OpenDetailRequested（4.2.1節「ダブルクリックで駅詳細編集へ遷移」）を
    /// 購読し、StationDetailViewModelへの画面切替を行う。他のマスター画面（M2スコープ外）が
    /// 同種の遷移を持つ場合もここへ同じパターンで追加していく想定。
    /// </summary>
    private void WireUpNavigation(ViewModelBase? content)
    {
        if (content is StationListViewModel stationList)
        {
            stationList.OpenDetailRequested += OnOpenStationDetailRequested;
        }
    }

    private void OnOpenStationDetailRequested(Station station)
    {
        (CurrentContent as IDisposable)?.Dispose();

        // StationはDIコンテナ管理外の実行時パラメータのため、ActivatorUtilitiesで
        // session/invokerをDIから解決しつつstation/goBackを追加引数として渡す。
        CurrentContent = ActivatorUtilities.CreateInstance<StationDetailViewModel>(
            _serviceProvider, station, (Action)ShowContentForSelectedNode);
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => _invoker.Undo();

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => _invoker.Redo();

    private bool CanUndo() => _invoker.CanUndo;
    private bool CanRedo() => _invoker.CanRedo;

    void ICacheChangeObserver.OnChanged(IReadOnlySet<ObjectId> affectedIds)
    {
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        _invoker.Unsubscribe(this);
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
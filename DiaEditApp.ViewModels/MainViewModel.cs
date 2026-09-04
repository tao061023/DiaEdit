namespace DiaEditApp.ViewModels;

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.DependencyInjection;

using DiaEditApp.ViewModels.Navigation;
using DiaEditApp.ViewModels.Stations;

using DiaEditCore.ChangeNotification;
using DiaEditCore.Commands;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Serialization.Json;
using DiaEditCore.Session;

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
    private readonly ProjectSession _session;
    private readonly IFileDialogService _fileDialogService;
    private readonly IAppSettingsService _appSettings;

    public ObservableCollection<NavigationNodeViewModel> RootNodes { get; }

    [ObservableProperty]
    public partial NavigationNodeViewModel? SelectedNode { get; set; }

    [ObservableProperty]
    public partial ViewModelBase? CurrentContent { get; set; }

    [ObservableProperty]
    public partial string? SaveErrorMessage { get; set; }

    public bool HasSaveError => !string.IsNullOrEmpty(SaveErrorMessage);
    partial void OnSaveErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasSaveError));

    public MainViewModel(
        IServiceProvider serviceProvider, CommandInvoker invoker, ProjectSession session,
        IFileDialogService fileDialogService, IAppSettingsService appSettings)
    {
        _serviceProvider = serviceProvider;
        _invoker = invoker;
        _session = session;
        _fileDialogService = fileDialogService;
        _appSettings = appSettings;
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
        var detail = ActivatorUtilities.CreateInstance<StationDetailViewModel>(
            _serviceProvider, station, (Action)ShowContentForSelectedNode);

        // UI設計書§4.2.3「構内配線図ポップアップ」の入口。StationDetailViewModelはWireUpNavigation
        // （ナビゲーションツリー経由生成専用）を通らずここで直接生成されるため、購読もここで行う。
        detail.OpenFloorUnitDetailRequested += OnOpenFloorUnitDetailRequested;

        CurrentContent = detail;
    }

    /// <summary>
    /// UI設計書§4.2.3の入口。FloorUnitDetailViewModel（Rail管理画面）の「戻る」は、
    /// ShowContentForSelectedNode（ナビゲーションツリーの選択状態から再生成＝駅一覧に戻ってしまう）
    /// ではなく、遷移元のStation詳細画面を再生成するコールバックとする
    /// （どの駅のFloorUnitを開いていたかという文脈をここで保持する必要があるため）。
    /// </summary>
    private void OnOpenFloorUnitDetailRequested(FloorUnit floorUnit)
    {
        (CurrentContent as IDisposable)?.Dispose();

        var station = _session.Current.Stations.First(s => s.Id == floorUnit.StationId);

        CurrentContent = ActivatorUtilities.CreateInstance<FloorUnitDetailViewModel>(
            _serviceProvider, floorUnit, (Action)(() => OnOpenStationDetailRequested(station)));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        SaveErrorMessage = null;

        var path = _appSettings.LastProjectFilePath;
        if (string.IsNullOrEmpty(path))
        {
            path = await _fileDialogService.PickSaveProjectFileAsync("新規プロジェクト.dedit");
            if (path is null) return; // ユーザーがキャンセル
        }

        try
        {
            JsonProjectFileSerializer.Save(_session.Current, path);
            _appSettings.LastProjectFilePath = path;
            _appSettings.Save();
        }
        catch (ProjectFileValidationException ex)
        {
            // 保存不可のissueを一括表示（Tao氏合意の暫定方針）。
            SaveErrorMessage = string.Join(Environment.NewLine, ex.Issues.Select(i => i.Message));
        }
        catch (IOException ex)
        {
            // ディスク書き込み失敗等。ProjectFileValidationExceptionとは別枠で表示。
            SaveErrorMessage = $"保存に失敗しました：{ex.Message}";
        }
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
namespace DiaEditApp.ViewModels.Stations;

using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DiaEditCore.ChangeNotification;
using DiaEditCore.Commands;
using DiaEditCore.Commands.Stations;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Session;

/// <summary>
/// M2-2：駅一覧（マスター）画面のViewModel。UI設計書4.2.1節の列構成
/// （駅名／種別／事業者管理番号／電報略号）でテーブル表示する。
///
/// データ取得・更新方針（DI登録方式の検討結果、案C採用）：ProjectSession／CommandInvokerを
/// そのままコンストラクタ注入する。Stationsは表示専用のObservableCollectionへコピーし、
/// CommandInvokerへICacheChangeObserverとして直接購読することで同期する
/// （M2-4でChangeNotificationBridge経由の配線に置き換えるまでの暫定対応。MainViewModelの
/// Undo/Redoボタンなど、本VM外から発生した変更もOnChanged経由で拾えるようにするため、
/// 自分がExecuteしたときだけReload()する方式では不十分だったことがM2-5動作確認で判明した）。
/// </summary>
public sealed partial class StationListViewModel : ViewModelBase, ICacheChangeObserver, IDisposable
{
    private readonly ProjectSession _session;
    private readonly CommandInvoker _invoker;

    public ObservableCollection<Station> Stations { get; } = new();

    [ObservableProperty]
    public partial Station? SelectedStation { get; set; }

    /// <summary>UI設計書4.2.1節「ダブルクリックで駅詳細編集へ遷移」。MainViewModelが購読し、
    /// CurrentContentをStationDetailViewModelへ差し替える（M2-3）。</summary>
    public event Action<Station>? OpenDetailRequested;

    public StationListViewModel(ProjectSession session, CommandInvoker invoker)
    {
        _session = session;
        _invoker = invoker;
        _invoker.Subscribe(this);
        Reload();
    }

    /// <summary>
    /// ProjectSession.Current.Stationsの内容でStationsを再同期する。
    /// </summary>
    public void Reload()
    {
        Stations.Clear();
        foreach (var station in _session.Current.Stations)
            Stations.Add(station);
    }

    void ICacheChangeObserver.OnChanged(IReadOnlySet<ObjectId> affectedIds) => Reload();

    /// <summary>
    /// UI設計書4.2.1節「+駅追加」。StationCreationWorkflowが返すTransactionCommand
    /// （CreateStationCommand＋CreateFloorUnitCommandの複合、n≥1制約対応）をExecuteする。
    /// 名称等の初期値はM2-3の駅詳細編集画面で確定させる想定のため、ここでは仮の名称で
    /// 新規作成し、生成後に選択状態にするところまでを担う。
    /// </summary>
    [RelayCommand]
    private void AddStation()
    {
        var command = StationCreationWorkflow.CreateStationWithDefaultFloorUnit(
            _session.Current.Stations,
            _session.Current.FloorUnits,
            _session.StationIds,
            _session.FloorUnitIds,
            new DisplayName { Name = "新規駅" },
            StationType.Standard);

        _invoker.Execute(command); // OnChanged経由でReload()される
        SelectedStation = Stations.LastOrDefault();
    }

    /// <summary>
    /// 選択中の駅を削除する。DeleteStationCommandのコンストラクタが直接参照元の存在を検査し、
    /// 参照元が残る場合はInvalidOperationExceptionを送出する（現状はUI側での例外ハンドリング・
    /// メッセージ表示は未実装。M2-3以降でダイアログ表示に差し替える）。
    /// </summary>
    [RelayCommand]
    private void DeleteSelectedStation()
    {
        if (SelectedStation is null) return;

        var command = new DeleteStationCommand(_session.Current.Stations, SelectedStation, _session);
        _invoker.Execute(command); // OnChanged経由でReload()される
        SelectedStation = null;
    }

    /// <summary>UI設計書4.2.1節「ダブルクリックで駅詳細編集へ遷移」。View側のDoubleTappedハンドラから
    /// 対象Stationを渡して呼ばれる想定。</summary>
    [RelayCommand]
    private void OpenDetail(Station? station)
    {
        if (station is not null)
            OpenDetailRequested?.Invoke(station);
    }

    public void Dispose() => _invoker.Unsubscribe(this);
}
namespace DiaEditApp.ViewModels.Stations;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
/// M2-3：駅詳細編集画面（UI設計書4.2.2節）のViewModel。
///
/// 上部：DisplayName／Type／OperatingCode／TelegraphCode／ShowsInStationTimetableOverrideの編集。
/// 保存は明示的な「保存」ボタン（SaveCommand）でChangeStationAttributesCommandを1回だけExecuteする
/// （ChangeStationAttributesCommandはStationSnapshotを1回のUndo単位とする設計のため、
/// フィールド変更のたびに自動保存するとUndoスタックが不必要に細分化される）。
///
/// 中央：FloorUnit一覧。今回のセッション合意により並べ替え（DisplayOrder変更）は見送り、
/// 追加・削除のみ実装する。
///
/// 遷移方式：Station（DIコンテナ管理外の実行時パラメータ）をコンストラクタで受け取るため、
/// MainViewModel側はActivatorUtilities.CreateInstanceで生成する（session/invokerはDIから、
/// station/goBackは呼び出し側から）。goBackはMainViewModel.ShowContentForSelectedNodeへの
/// コールバックで、一覧画面へ戻る。
/// </summary>
public sealed partial class StationDetailViewModel : ViewModelBase, ICacheChangeObserver, IDisposable
{
    /// <summary>ComboBoxのItemsSource用。StationType列挙値の全件。</summary>
    public IReadOnlyList<StationType> StationTypes { get; } = Enum.GetValues<StationType>();

    private readonly Station _station;
    private readonly ProjectSession _session;
    private readonly CommandInvoker _invoker;
    private readonly Action _goBack;

    public ObservableCollection<FloorUnit> FloorUnits { get; } = new();

    [ObservableProperty]
    public partial string DisplayNameText { get; set; } = "";

    [ObservableProperty]
    public partial StationType Type { get; set; }

    [ObservableProperty]
    public partial string OperatingCode { get; set; } = "";

    [ObservableProperty]
    public partial string TelegraphCode { get; set; } = "";

    /// <summary>null＝未設定（Type由来の派生値へフォールバック）。ResolvedShowsInStationTimetableで
    /// その派生値を補助表示する（4.2.2節）。</summary>
    [ObservableProperty]
    public partial bool? ShowsInStationTimetableOverride { get; set; }

    public bool ResolvedShowsInStationTimetable => _station.ResolveShowsInStationTimetable();

    [ObservableProperty]
    public partial FloorUnit? SelectedFloorUnit { get; set; }

    /// <summary>FloorUnit削除がn≥1制約／直接参照元により拒否された場合のメッセージ表示用。
    /// UI設計書側にエラーダイアログの仕様がまだ無いため、暫定的に文字列プロパティで表示する。</summary>
    [ObservableProperty]
    public partial string? DeleteFloorUnitError { get; set; }

    public bool HasDeleteFloorUnitError => !string.IsNullOrEmpty(DeleteFloorUnitError);

    partial void OnDeleteFloorUnitErrorChanged(string? value) => OnPropertyChanged(nameof(HasDeleteFloorUnitError));

    public StationDetailViewModel(Station station, ProjectSession session, CommandInvoker invoker, Action goBack)
    {
        _station = station;
        _session = session;
        _invoker = invoker;
        _goBack = goBack;
        _invoker.Subscribe(this);

        LoadFromStation();
        ReloadFloorUnits();
    }

    private void LoadFromStation()
    {
        DisplayNameText = _station.DisplayName.Name;
        Type = _station.Type;
        OperatingCode = _station.OperatingCode;
        TelegraphCode = _station.TelegraphCode;
        ShowsInStationTimetableOverride = _station.ShowsInStationTimetableOverride;
        OnPropertyChanged(nameof(ResolvedShowsInStationTimetable));
    }

    private void ReloadFloorUnits()
    {
        FloorUnits.Clear();
        foreach (var fu in _session.Current.FloorUnits
                     .Where(f => f.StationId == _station.Id)
                     .OrderBy(f => f.DisplayOrder))
        {
            FloorUnits.Add(fu);
        }
    }

    void ICacheChangeObserver.OnChanged(IReadOnlySet<ObjectId> affectedIds)
    {
        // Undo/Redoで_station自体が書き戻された場合も含め、常に全項目を再読込する
        // （discard-and-regenerateの方針を踏襲。差分判定はしない）。
        LoadFromStation();
        ReloadFloorUnits();
    }

    [RelayCommand]
    private void Save()
    {
        var newValues = new StationSnapshot(
            new DisplayName { Name = DisplayNameText },
            Type,
            OperatingCode,
            TelegraphCode,
            ShowsInStationTimetableOverride);

        var command = new ChangeStationAttributesCommand(_station, newValues, _session);
        _invoker.Execute(command); // OnChanged経由で自画面もLoadFromStation()される
    }

    [RelayCommand]
    private void AddFloorUnit()
    {
        var nextOrder = FloorUnits.Count == 0 ? 0 : FloorUnits.Max(f => f.DisplayOrder) + 1;
        var command = new CreateFloorUnitCommand(_session.Current.FloorUnits, _station.Id, name: "", displayOrder: nextOrder);
        _invoker.Execute(command); // OnChanged経由でReloadFloorUnits()される
    }

    [RelayCommand]
    private void DeleteSelectedFloorUnit()
    {
        if (SelectedFloorUnit is null) return;

        DeleteFloorUnitError = null;
        try
        {
            var command = new DeleteFloorUnitCommand(_session.Current.FloorUnits, SelectedFloorUnit, _session);
            _invoker.Execute(command); // OnChanged経由でReloadFloorUnits()される
            SelectedFloorUnit = null;
        }
        catch (InvalidOperationException ex)
        {
            // n≥1制約または直接参照元が残っているケース（DeleteFloorUnitCommandのコンストラクタ内検査）。
            // 専用ダイアログはUI設計書未確定のため、暫定的に画面内メッセージで表示する。
            DeleteFloorUnitError = ex.Message;
        }
    }

    [RelayCommand]
    private void GoBack() => _goBack();

    public void Dispose() => _invoker.Unsubscribe(this);
}
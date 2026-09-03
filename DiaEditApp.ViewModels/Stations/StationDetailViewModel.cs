namespace DiaEditApp.ViewModels.Stations;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DiaEditApp.ViewModels; // IAffectedByObjectId, ChangeNotificationBridge

using DiaEditCore.Commands;
using DiaEditCore.Commands.Stations;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Session;

public sealed partial class StationDetailViewModel : ViewModelBase, IAffectedByObjectId, IDisposable
{
    public IReadOnlyList<StationType> StationTypes { get; } = Enum.GetValues<StationType>();

    private readonly Station _station;
    private readonly ProjectSession _session;
    private readonly CommandInvoker _invoker;
    private readonly ChangeNotificationBridge _bridge;
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

    [ObservableProperty]
    public partial bool? ShowsInStationTimetableOverride { get; set; }

    // §9.2項目31：入力欄が変更されるたびにIsDirtyを再評価し、「決定」ボタンの活性制御に使う。
    partial void OnDisplayNameTextChanged(string value) => OnPropertyChanged(nameof(IsDirty));
    partial void OnTypeChanged(StationType value) => OnPropertyChanged(nameof(IsDirty));
    partial void OnOperatingCodeChanged(string value) => OnPropertyChanged(nameof(IsDirty));
    partial void OnTelegraphCodeChanged(string value) => OnPropertyChanged(nameof(IsDirty));
    partial void OnShowsInStationTimetableOverrideChanged(bool? value) => OnPropertyChanged(nameof(IsDirty));

    /// <summary>
    /// §9.2項目31：現在の入力値がStationの現状値と一致しているかどうか。
    /// Save()側の差分判定（BuildEditedSnapshotとCaptureCurrentSnapshotの比較）と
    /// 同じ比較規約を使う（DisplayNameのIEquatable実装、§9.2項目31関連対応）。
    /// </summary>
    public bool IsDirty => !BuildEditedSnapshot().Equals(CaptureCurrentSnapshot());

    public bool ResolvedShowsInStationTimetable => _station.ResolveShowsInStationTimetable();

    [ObservableProperty]
    public partial FloorUnit? SelectedFloorUnit { get; set; }

    [ObservableProperty]
    public partial string? DeleteFloorUnitError { get; set; }

    public bool HasDeleteFloorUnitError => !string.IsNullOrEmpty(DeleteFloorUnitError);

    partial void OnDeleteFloorUnitErrorChanged(string? value) => OnPropertyChanged(nameof(HasDeleteFloorUnitError));

    /// <summary>
    /// M2-4：ChangeNotificationBridge向けの監視対象。Station自身に加え、現在この駅に属する
    /// 全FloorUnitのIdを含める。固定集合ではなく都度算出するプロパティとすることで、
    /// FloorUnit追加直後（Execute完了後・Notify呼び出し前）の新規Idも自動的に拾える
    /// （ChangeNotificationBridge.OnChangedはOverlaps判定のたびにこのgetterを再評価するため）。
    /// </summary>
    public IReadOnlySet<ObjectId> ObservedIds
    {
        get
        {
            var ids = new HashSet<ObjectId> { new StationObjectId(_station.Id) };
            foreach (var fu in _session.Current.FloorUnits.Where(f => f.StationId == _station.Id))
                ids.Add(new FloorUnitObjectId(fu.Id));
            return ids;
        }
    }

    public StationDetailViewModel(
        Station station, ProjectSession session, CommandInvoker invoker,
        ChangeNotificationBridge bridge, Action goBack)
    {
        _station = station;
        _session = session;
        _invoker = invoker;
        _bridge = bridge;
        _goBack = goBack;
        _bridge.Subscribe(this);

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
        OnPropertyChanged(nameof(IsDirty));
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

    void IAffectedByObjectId.OnAffected()
    {
        if (!_session.Current.Stations.Contains(_station))
        {
            _goBack();
            return;
        }

        LoadFromStation();
        ReloadFloorUnits();
    }

    /// <summary>
    /// §9.2項目31：入力欄の現在値から、保存対象となるStationSnapshotを組み立てる。
    /// DisplayNameはAbbreviation／Translationsを現状のまま保持し、Nameのみ入力値で上書きする
    /// （UIにAbbreviation/Translations編集欄が未実装のため、既存値を欠落させないための対応）。
    /// </summary>
    private StationSnapshot BuildEditedSnapshot()
    {
        var editedDisplayName = _station.DisplayName.Clone();
        editedDisplayName.Name = DisplayNameText;

        return new StationSnapshot(
            editedDisplayName,
            Type,
            OperatingCode,
            TelegraphCode,
            ShowsInStationTimetableOverride);
    }

    /// <summary>§9.2項目31：Stationの現在値をそのままStationSnapshot化する（差分判定の比較対象）。</summary>
    private StationSnapshot CaptureCurrentSnapshot() => new(
        _station.DisplayName,
        _station.Type,
        _station.OperatingCode,
        _station.TelegraphCode,
        _station.ShowsInStationTimetableOverride);

    /// <summary>
    /// §9.2項目31：差分がある場合のみChangeStationAttributesCommandを発行し、
    /// 決定確定時は一覧へ自動遷移する。差分が無ければコマンドを発行せずそのまま一覧へ戻る
    /// （無条件Execute()によるno-opのUndoスタックエントリ蓄積を防ぐ、§9.2項目32の再発防止）。
    /// </summary>
    [RelayCommand]
    private void Save()
    {
        var newValues = BuildEditedSnapshot();

        if (newValues.Equals(CaptureCurrentSnapshot()))
        {
            _goBack();
            return;
        }

        var command = new ChangeStationAttributesCommand(_station, newValues, _session);
        _invoker.Execute(command); // OnAffected経由で自画面もLoadFromStation()される
        _goBack();
    }

    [RelayCommand]
    private void AddFloorUnit()
    {
        var nextOrder = FloorUnits.Count == 0 ? 0 : FloorUnits.Max(f => f.DisplayOrder) + 1;
        var command = new CreateFloorUnitCommand(_session.Current.FloorUnits, _session.FloorUnitIds, _station.Id, name: "", displayOrder: nextOrder);
        _invoker.Execute(command); // OnAffected経由でReloadFloorUnits()される
    }

    [RelayCommand]
    private void DeleteSelectedFloorUnit()
    {
        if (SelectedFloorUnit is null) return;

        DeleteFloorUnitError = null;
        try
        {
            var command = new DeleteFloorUnitCommand(_session.Current.FloorUnits, SelectedFloorUnit, _session);
            _invoker.Execute(command); // OnAffected経由でReloadFloorUnits()される
            SelectedFloorUnit = null;
        }
        catch (InvalidOperationException ex)
        {
            DeleteFloorUnitError = ex.Message;
        }
    }

    [RelayCommand]
    private void GoBack() => _goBack();

    public void Dispose() => _bridge.Unsubscribe(this);
}
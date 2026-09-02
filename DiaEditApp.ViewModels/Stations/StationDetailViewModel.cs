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
        _invoker.Execute(command); // OnAffected経由で自画面もLoadFromStation()される
    }

    [RelayCommand]
    private void AddFloorUnit()
    {
        var nextOrder = FloorUnits.Count == 0 ? 0 : FloorUnits.Max(f => f.DisplayOrder) + 1;
        var command = new CreateFloorUnitCommand(_session.Current.FloorUnits, _station.Id, name: "", displayOrder: nextOrder);
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
namespace DiaEditApp.Views.Stations;

using Avalonia.Controls;
using Avalonia.Input;

using DiaEditApp.ViewModels.Stations;

public partial class StationListView : UserControl
{
    public StationListView()
    {
        InitializeComponent();
    }

    // UI設計書4.2.1節「ダブルクリックで駅詳細編集へ遷移」。
    // ListBox.SelectedItemは単一クリックの時点で確定しているため、DoubleTapped時点の
    // SelectedStationをそのままOpenDetailCommandへ渡せばよい。
    private void OnStationDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is StationListViewModel vm && vm.SelectedStation is not null)
            vm.OpenDetailCommand.Execute(vm.SelectedStation);
    }
}
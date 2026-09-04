namespace DiaEditApp.Views.Stations;

using Avalonia.Controls;
using Avalonia.Input;

using DiaEditApp.ViewModels.Stations;

public partial class StationDetailView : UserControl
{
    public StationDetailView()
    {
        InitializeComponent();
    }

    // UI設計書§4.2.3「構内配線図ポップアップ」の入口。StationListView.OnStationDoubleTappedと同型。
    private void OnFloorUnitDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is StationDetailViewModel vm && vm.SelectedFloorUnit is not null)
            vm.OpenFloorUnitDetailCommand.Execute(vm.SelectedFloorUnit);
    }
}

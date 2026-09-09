namespace DiaEditApp.Views.Stations;

using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;

using DiaEditApp.ViewModels.Stations;
using DiaEditCore.Model.Stations.FloorUnitObjects;

public partial class FloorUnitDetailView : UserControl
{
    public FloorUnitDetailView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// キャンバス上のRail図形（Line）クリックをViewModel.SelectRailFromCanvasへ橋渡しする。
    /// Avalonia Behaviorsパッケージ未導入のため、既存の駅一覧⇔詳細遷移と同様、
    /// コードビハインドでの最小限のイベント購読方針を踏襲する（v13.3で確立した運用ルール）。
    /// </summary>
    private void OnRailShapePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Line { Tag: Rail rail } && DataContext is FloorUnitDetailViewModel vm)
        {
            vm.SelectRailFromCanvas(rail);
        }
    }
}
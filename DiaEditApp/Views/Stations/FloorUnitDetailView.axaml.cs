namespace DiaEditApp.Views.Stations;

using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;

using DiaEditApp.ViewModels.Stations;
using DiaEditCore.Model;
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

    /// <summary>
    /// キャンバス上の端点図形（Ellipse）へのPointerPressedをドラッグ開始として扱う。
    /// ViewModel.TryStartEndpointDragが開始を認めた場合のみ、以降のPointerMoved/Releasedを
    /// 確実に受け取れるようキャンバス自身へPointerCaptureする（Ellipse上でCaptureすると
    /// ポインタがEllipse範囲外へ出た瞬間に後続イベントが来なくなるため、あえてCanvas側で捕捉する）。
    /// </summary>
    private void OnEndpointPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { Tag: ObjectId objectId } || DataContext is not FloorUnitDetailViewModel vm)
            return;

        var canvas = this.FindControl<Canvas>("FloorUnitCanvas");
        if (canvas is null) return;

        var canvasPos = e.GetPosition(canvas);
        if (vm.TryStartEndpointDrag(objectId, new CanvasPoint(canvasPos.X, canvasPos.Y)))
        {
            e.Pointer.Capture(canvas);
            e.Handled = true;
        }
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not Canvas canvas || DataContext is not FloorUnitDetailViewModel vm) return;
        var pos = e.GetPosition(canvas);
        vm.UpdateEndpointDrag(new CanvasPoint(pos.X, pos.Y));
    }

    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not Canvas canvas || DataContext is not FloorUnitDetailViewModel vm) return;
        var pos = e.GetPosition(canvas);
        vm.EndEndpointDrag(new CanvasPoint(pos.X, pos.Y));
        e.Pointer.Capture(null);
    }

    /// <summary>
    /// 何らかの理由（他コントロールへのフォーカス移動等）でPointerCaptureが失われた場合、
    /// ドラッグ中のプレビューを破棄する（モデル未変更のままの中断を保証）。
    /// </summary>
    private void OnCanvasPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (DataContext is FloorUnitDetailViewModel vm)
        {
            vm.CancelEndpointDrag();
        }
    }
}
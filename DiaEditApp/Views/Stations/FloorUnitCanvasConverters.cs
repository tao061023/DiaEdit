namespace DiaEditApp.Views.Stations;

using Avalonia.Data.Converters;
using Avalonia.Media;

using DiaEditApp.ViewModels.Stations; // EndpointVisualKind, CanvasPoint参照のため

/// <summary>
/// FloorUnitDetailView（キャンバス、§4.2.3・§4.4.2-23ステージ1）専用の値コンバータ群。
/// </summary>
public static class FloorUnitCanvasConverters
{
    /// <summary>選択中Railは強調色（オレンジ系）、非選択は通常色（濃灰）で描画する。</summary>
    public static readonly IValueConverter SelectedStroke =
        new FuncValueConverter<bool, IBrush>(sel => sel ? Brushes.OrangeRed : Brushes.DimGray);

    /// <summary>選択中Railは線を太く表示する。</summary>
    public static readonly IValueConverter SelectedStrokeWidth =
        new FuncValueConverter<bool, double>(sel => sel ? 3.0 : 1.5);

    /// <summary>Point中心座標から、8x8円の左上Canvas座標へオフセットする（中心合わせ表示用）。</summary>
    public static readonly IValueConverter CenterOffset =
        new FuncValueConverter<double, double>(v => v - 4);

    /// <summary>
    /// DiaEditApp.ViewModels.Stations.CanvasPoint（double、View非依存の表示専用座標）→
    /// Avalonia.Point（double）への変換。Line.StartPoint／EndPointが後者の型を要求するため必須。
    /// </summary>
    public static readonly IValueConverter ToAvaloniaPoint =
        new FuncValueConverter<CanvasPoint, Avalonia.Point>(p => new Avalonia.Point(p.X, p.Y));

    /// <summary>端点種別ごとに色分けする（BoundaryPoint=青／EntryPoint=緑／BufferStop=赤／Switcher=紫／None=灰）。</summary>
    public static readonly IValueConverter KindToBrush =
        new FuncValueConverter<EndpointVisualKind, IBrush>(kind => kind switch
        {
            EndpointVisualKind.None => Brushes.LightGray,
            EndpointVisualKind.BoundaryPoint => Brushes.DodgerBlue,
            EndpointVisualKind.EntryPoint => Brushes.SeaGreen,
            EndpointVisualKind.BufferStop => Brushes.Firebrick,
            EndpointVisualKind.Switcher => Brushes.MediumPurple,
            _ => Brushes.Black,
        });
}
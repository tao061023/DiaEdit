namespace DiaEditApp.Views.Stations;

using Avalonia.Controls;
using Avalonia.Input;

using DiaEditApp.ViewModels.Stations;

public partial class StationDetailView : UserControl
{
    public StationDetailView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// §9.2項目31：Enterキーでの決定確定。
    /// 注意：AvaloniaにIME変換中かどうかを問い合わせる公開APIが存在しないため
    /// （2026-09時点、Avalonia 12.1で確認）、IME変換確定用のEnterが本ハンドラまで
    /// 到達しないことは、TextBoxがITextInputMethodClientとしてIME処理を内部で
    /// 完結させるという一般的な挙動に依存している。実機での日本語入力確認が必須
    /// （Windows IME・macOS各種入力ソースで動作検証すること）。
    /// もし変換確定のEnterで誤発火する場合は、TextBox.KeyDownではなくTextBox側の
    /// PreviewKeyDown相当（Tunnelルーティング）でe.Handledを個別制御する必要がある。
    /// </summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        if (DataContext is StationDetailViewModel vm && vm.SaveCommand.CanExecute(null))
        {
            vm.SaveCommand.Execute(null);
            e.Handled = true;
        }
    }
}
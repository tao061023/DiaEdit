using System;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using Microsoft.Extensions.DependencyInjection;

using DiaEditApp.ViewModels;
using DiaEditApp.ViewModels.Composition;
using DiaEditApp.Views;
using DiaEditCore.Composition;

namespace DiaEditApp;

public partial class App : Application
{
    /// <summary>
    /// アプリ全体のServiceProvider。7.3節の通り、DiaEditCore・DiaEditApp.ViewModels・
    /// DiaEditApp.Servicesの登録をここへ一括反映してから構築する。
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        services.AddDiaEditCore();
        services.AddDiaEditAppViewModels();
        // 将来DiaEditApp.Services（IFileDialogService等、Avalonia依存の実装）が増えたら
        // ここに services.AddDiaEditAppServices() 等を追加する（7.3節参照）。

        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
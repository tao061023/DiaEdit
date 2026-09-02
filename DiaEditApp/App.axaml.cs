using System;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using Microsoft.Extensions.DependencyInjection;

using DiaEditApp.Services;
using DiaEditApp.ViewModels;
using DiaEditApp.ViewModels.Composition;
using DiaEditApp.Views;
using DiaEditCore.Composition;
using DiaEditCore.Commands;
using DiaEditCore.Model;
using DiaEditCore.Serialization.Json;
using DiaEditCore.Session;

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
        // AppSettingsは起動シーケンスの一部としてDI構築より先に読み込み、
        // 読み込んだ同一インスタンスをSingletonとして登録する
        // （MainViewModelがLastProjectFilePathを保存時に書き換えるため、
        //  「読み込んだもの」と「以後参照するもの」を同一インスタンスにする必要がある）。
        var appSettings = AppSettings.Load();

        var services = new ServiceCollection();
        services.AddDiaEditCore();
        services.AddDiaEditAppViewModels();
        services.AddDiaEditAppServices();
        services.AddSingleton(appSettings);
        services.AddSingleton<IAppSettingsService>(sp => new AppSettingsService(sp.GetRequiredService<AppSettings>()));

        Services = services.BuildServiceProvider();

        var invoker = Services.GetRequiredService<CommandInvoker>();
        var bridge = Services.GetRequiredService<ChangeNotificationBridge>();
        invoker.Subscribe(bridge);

        var session = Services.GetRequiredService<ProjectSession>();
        session.Load(LoadLastProjectOrCreateNew(appSettings));

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ProjectFile LoadLastProjectOrCreateNew(AppSettings appSettings)
    {
        if (!string.IsNullOrEmpty(appSettings.LastProjectFilePath))
        {
            try
            {
                return JsonProjectFileSerializer.Load(appSettings.LastProjectFilePath);
            }
            catch
            {
                // ファイル削除・破損・UnsupportedSchemaVersionException等、理由を問わず
                // 起動を止めずに新規空プロジェクトへフォールバックする。
            }
        }

        // TODO（ProjectSettings.cs未確認のためブロック中）：ProjectFile.ProjectSettingsはrequiredの
        // ため、妥当な既定値を構築する必要がある。ProjectSettings.cs入手後、CreateEmptyProjectFile()
        // 側でnew ProjectSettings { ... } を正しく埋める。
        return CreateEmptyProjectFile();
    }

    private static ProjectFile CreateEmptyProjectFile()
    {
        // 既定値の方針：ValidationRulesの各閾値はnull（未設定＝当該チェック項目はスキップ）とし、
        // 路線ごとの実情に合わせてユーザーが後から設定する前提とする。EnableConflictDetection／
        // EnableCarLengthCheckは安全側としてONにしておく（§5.7の既知バグ：EnableCarLengthCheck=false
        // でもEffectiveLengthCheckerが常時ハードエラーになる問題は別途修正対象であり、ここでの
        // 既定値選択には影響しない）。DiagramBasedTimeSecはProjectSettingsのデフォルト値(14400=4:00)
        // をそのまま使う。
        var validationRules = new ValidationRules(
            MinDwellTimeSec: null,
            MinHeadwaySec: null,
            MinTurnaroundSec: null,
            TrackEntryMarginSec: null,
            TrackPassMarginSec: null,
            EnableConflictDetection: true,
            EnableCarLengthCheck: true);

        return new ProjectFile
        {
            SchemaVersion = JsonProjectFileSerializer.CurrentSchemaVersion,
            ProjectSettings = new ProjectSettings(validationRules),
        };
    }
}
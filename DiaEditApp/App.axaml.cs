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
        var services = new ServiceCollection();
        services.AddDiaEditCore();
        services.AddDiaEditAppViewModels();
        // 将来DiaEditApp.Services（IFileDialogService等、Avalonia依存の実装）が増えたら
        // ここに services.AddDiaEditAppServices() 等を追加する（7.3節参照）。

        Services = services.BuildServiceProvider();

        // 起動時プロジェクト初期化方針（M2-1ブートストラップ確定分）：
        // 直近使用ファイルのパスをAppSettingsから読み、開ければそれをLoad()する。
        // パスが無い／読込失敗（ファイル削除・破損・スキーマ非対応等）の場合は
        // 空の新規ProjectFileを生成してLoad()する（起動を止めないことを優先）。
        var appSettings = AppSettings.Load();
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
namespace DiaEditApp.Services;

using DiaEditApp.ViewModels;

/// <summary>
/// AppSettings（読み書きの実体、静的Load/Saveを持つPOCO）をIAppSettingsServiceとして
/// DIコンテナへ公開するアダプタ。App.axaml.cs起動時にAppSettings.Load()で読み込んだ
/// 1つのインスタンスをそのままラップすることで、MainViewModel側の変更がAppSettings本体へ
/// 反映される（同一インスタンス共有）。
/// </summary>
public sealed class AppSettingsService : IAppSettingsService
{
    private readonly AppSettings _settings;

    public AppSettingsService(AppSettings settings) => _settings = settings;

    public string? LastProjectFilePath
    {
        get => _settings.LastProjectFilePath;
        set => _settings.LastProjectFilePath = value;
    }

    public void Save() => _settings.Save();
}
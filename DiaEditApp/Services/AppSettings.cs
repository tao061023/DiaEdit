namespace DiaEditApp.Services;

using System;
using System.IO;
using System.Text.Json;

/// <summary>
/// アプリ全体の設定（現時点では直近使用プロジェクトファイルパスのみ）。
/// ProjectFile本体（DiaEditCore側、1プロジェクト1JSON）とは別に、
/// %AppData%\DiaEdit\settings.json へ保存する軽量な設定ファイル。
///
/// Avalonia依存を持たない単純なPOCO＋staticな読み書きヘルパーとして
/// DiaEditApp.Services（Avalonia依存があってよい層、7.3節）に置く。
/// </summary>
public sealed class AppSettings
{
    public string? LastProjectFilePath { get; set; }

    private static string SettingsFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DiaEdit",
            "settings.json");

    /// <summary>
    /// 設定ファイルを読み込む。存在しない・破損している場合は例外を送出せず、
    /// LastProjectFilePath=nullの既定値を返す（起動シーケンスを止めないため）。
    /// </summary>
    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
                return new AppSettings();

            var json = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            // 設定ファイルの破損等で起動不能になることを避ける。設定は失うが起動は継続する。
            return new AppSettings();
        }
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(SettingsFilePath)!;
        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

        // JsonProjectFileSerializer.Saveと同様、一時ファイル経由での置き換えとする
        var tempPath = SettingsFilePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, SettingsFilePath, overwrite: true);
    }
}
namespace DiaEditApp.Services;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

using DiaEditApp.ViewModels;

public sealed class FileDialogService : IFileDialogService
{
    public async Task<string?> PickSaveProjectFileAsync(string? suggestedFileName)
    {
        var topLevel = GetTopLevel();
        if (topLevel?.StorageProvider is not { } storageProvider) return null;

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "プロジェクトを保存",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "dedit",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("DiaEdit プロジェクト") { Patterns = new[] { "*.dedit" } }
            }
        });

        return file?.TryGetLocalPath();
    }

    // DIコンテナ構築時点ではMainWindowがまだ生成されていないため、コンストラクタで
    // Windowを受け取らず、呼び出し時にApplication.Currentから都度解決する。
    private static TopLevel? GetTopLevel() =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
}
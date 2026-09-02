namespace DiaEditApp.ViewModels;

public interface IFileDialogService
{
    /// <summary>プロジェクトの保存先パスをユーザーに選ばせる。キャンセル時はnull。</summary>
    Task<string?> PickSaveProjectFileAsync(string? suggestedFileName);
}
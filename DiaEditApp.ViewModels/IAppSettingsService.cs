namespace DiaEditApp.ViewModels;

/// <summary>
/// MainViewModelが必要とするAppSettings操作の抽象。IFileDialogServiceと同じ理由
/// （DiaEditApp.ViewModelsはDiaEditApp.Services、ひいてはDiaEditApp本体を参照しないという
/// 依存方向の規約、7.3節）により、具象のAppSettingsクラスを直接注入せずインターフェース越しに使う。
/// </summary>
public interface IAppSettingsService
{
    string? LastProjectFilePath { get; set; }
    void Save();
}
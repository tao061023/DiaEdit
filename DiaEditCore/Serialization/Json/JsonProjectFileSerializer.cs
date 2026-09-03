namespace DiaEditCore.Serialization.Json;

using System.Text.Json;

using DiaEditCore.Model;
using DiaEditCore.Serialization.Validation;

public sealed class ProjectFileValidationException : Exception
{
    public IReadOnlyList<IValidationIssue> Issues { get; }

    public ProjectFileValidationException(IReadOnlyList<IValidationIssue> issues)
        : base($"保存を中止しました。{issues.Count}件のissueが検出されました（Warning＝保存不可相当）。")
    {
        Issues = issues;
    }
}

public sealed class UnsupportedSchemaVersionException : Exception
{
    public int FoundVersion { get; }
    public int SupportedVersion { get; }

    public UnsupportedSchemaVersionException(int foundVersion, int supportedVersion)
        : base($"保存ファイルのSchemaVersion={foundVersion}は未対応です（対応バージョン：{supportedVersion}）。")
    {
        FoundVersion = foundVersion;
        SupportedVersion = supportedVersion;
    }
}

/// <summary>
/// ProjectFileの保存・読込（1プロジェクト1JSON、7.3.1節・§8.2項目2/13クローズ）。
/// SchemaVersion検証・保存時の全Validator実行（SaveValidationRunner）ゲートをここに集約する。
/// </summary>
public static class JsonProjectFileSerializer
{
    public const int CurrentSchemaVersion = 1;

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true, // 論点H：所有構造によるネストは採用しないため、
                                   // 整形出力＋プロパティ宣言順序（§4.5実装順序）で可読性を確保する
        };
        options.Converters.Add(new IntIdJsonConverterFactory());
        options.Converters.Add(new RestrictionTargetJsonConverter());
        options.Converters.Add(new RailEndpointRefJsonConverter());
        options.Converters.Add(new ObjectIdJsonConverter());
        return options;
    }

    /// <summary>
    /// 保存する。SaveValidationRunner.ValidateAllで1件でもissueが検出された場合、
    /// ファイルへは一切書き込まずProjectFileValidationExceptionを送出する
    /// （本プロジェクトの運用ではValidationSeverity.Warning＝保存不可相当）。
    /// </summary>
    public static void Save(ProjectFile project, string path)
    {
        var issues = Serialization.Validation.SaveValidationRunner.ValidateAll(project);
        if (issues.Count > 0)
            throw new ProjectFileValidationException(issues);

        project.SchemaVersion = CurrentSchemaVersion;

        var options = CreateOptions();
        var json = JsonSerializer.Serialize(project, options);

        // 書き込み中の異常（ディスク容量不足等）で既存ファイルを破損させないよう、
        // 一時ファイルへ書き出してから置き換える
        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, path, overwrite: true);
    }

    /// <summary>
    /// 読込む。SchemaVersionが未対応の場合はUnsupportedSchemaVersionExceptionを送出する。
    /// 読込内容自体のバリデーション（ファイルは開けたが内容が不正）は呼び出し側の判断とする
    /// （読込直後にSaveValidationRunner.ValidateAllを呼んでUIへ警告表示する等）。
    /// </summary>
    public static ProjectFile Load(string path)
    {
        var options = CreateOptions();
        var json = File.ReadAllText(path);
        var project = JsonSerializer.Deserialize<ProjectFile>(json, options)
            ?? throw new JsonException($"{path}: デシリアライズ結果がnull");

        if (project.SchemaVersion != CurrentSchemaVersion)
            throw new UnsupportedSchemaVersionException(project.SchemaVersion, CurrentSchemaVersion);

        return project;
    }
}

using System.Text;
using DiaEdit.DocGen;
using DiaEdit.DocGen.Extraction;
using DiaEdit.DocGen.Manifest;
using DiaEdit.DocGen.Models;
using DiaEdit.DocGen.Rendering;

var options = CliOptions.Parse(args);

Console.WriteLine($"[DocGen] project={options.ProjectName} src={options.SrcDir}");

// 1. 台帳ロード
var manifest = DocGenManifest.Load(options.ManifestPath);
var projectEntry = GetProjectEntry(manifest, options.ProjectName);
if (projectEntry is null)
{
    Console.Error.WriteLine(
        $"[DocGen] ERROR: Unknown project name '{options.ProjectName}'. " +
        "Expected one of: DiaEditCore, DiaEditApp.ViewModels, DiaEditApp. " +
        "Check the --project argument in this project's postbuild event.");
    return 1;
}

// 2. ソース収集
var srcFiles = SourceFileCollector.Collect(options.SrcDir);
if (srcFiles.Count == 0)
{
    Console.WriteLine($"[DocGen] No source files found under {options.SrcDir}. Skipping.");
    return 0;
}

// 3. 型宣言収集（ファイル単位、失敗しても継続）
var allRawTypes = new List<RawTypeDeclaration>();
foreach (var file in srcFiles)
{
    try
    {
        allRawTypes.AddRange(TypeDeclarationCollector.Collect(file));
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[DocGen] WARN: failed to parse {file.AbsolutePath}: {ex.Message}");
    }
}

if (allRawTypes.Count == 0)
{
    Console.WriteLine("[DocGen] No type declarations found. Skipping manifest/doc update.");
    return 0;
}

// 4. partial統合
var mergedTypes = PartialTypeMerger.Merge(allRawTypes);

// 5. フォルダ単位グルーピング（"" はルート直下ファイル。台帳登録・レンダリング両方から除外）
var byFolder = mergedTypes
    .GroupBy(t => Path.GetDirectoryName(t.PrimaryRelativePath)?.Replace('\\', '/') ?? "")
    .OrderBy(g => g.Key, StringComparer.Ordinal)
    .ToList();

var rootLevelTypes = byFolder.Where(g => g.Key == "").SelectMany(g => g).ToList();
if (rootLevelTypes.Count > 0)
{
    Console.WriteLine(
        $"[DocGen] WARN: skipping {rootLevelTypes.Count} root-level file(s) not under a named folder: " +
        string.Join(", ", rootLevelTypes.Select(t => t.TypeName)));
}

// 6. 未登録フォルダの採番（空文字列キーは除外）
var allFolderKeys = byFolder.Select(g => g.Key).Where(k => k != "").ToList();
ManifestAllocator.AllocateMissingFolders(projectEntry, allFolderKeys);

// 7. フォルダキー → 型ドキュメントモデル一覧、の辞書を構築
var contentByFolderKey = byFolder
    .Where(g => g.Key != "")
    .ToDictionary(
        g => g.Key,
        g => (IReadOnlyList<TypeDocModel>)g.Select(TypeDocBuilder.Build).ToList());

// 8. 章全体をmanifestの番号順に組み立てる
var renderedChapter = ChapterRenderer.RenderChapter(
    options.ProjectName, projectEntry, options.ProjectName, contentByFolderKey);

// 9. mdの該当章マーカーを丸ごと差し替え、台帳を保存
var editor = new MarkdownDocumentEditor(options.DocPath);
editor.UpsertChapter(options.ProjectName, projectEntry.Number, renderedChapter);
editor.Save(options.DocPath);
manifest.Save(options.ManifestPath);

Console.WriteLine(
    $"[DocGen] Updated chapter {projectEntry.Number} ({options.ProjectName}): " +
    $"{contentByFolderKey.Count} folder(s), {mergedTypes.Count} type(s).");

return 0;

// ---- ローカル関数 ----

static DocGenManifest.ProjectEntry? GetProjectEntry(DocGenManifest manifest, string projectName) =>
    projectName switch
    {
        "DiaEditCore" => manifest.DiaEditCore,
        "DiaEditApp.ViewModels" => manifest.DiaEditAppViewModels,
        "DiaEditApp" => manifest.DiaEditApp,
        _ => null
    };
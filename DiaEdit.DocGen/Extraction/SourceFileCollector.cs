namespace DiaEdit.DocGen.Extraction;

using DiaEdit.DocGen.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
public sealed record CollectedSourceFile(
    string AbsolutePath,
    string RelativePathFromProjectRoot); // 例: "Algorithm/CacheBuilder/BaseRunTimeIndexBuilder.cs"

public static class SourceFileCollector
{
    private static readonly string[] ExcludedDirNames = { "bin", "obj" };
    private static readonly string[] ExcludedFileSuffixes = { ".g.cs", ".g.i.cs", ".Designer.cs", ".AssemblyInfo.cs" };

    public static IReadOnlyList<CollectedSourceFile> Collect(string projectRootDir)
    {
        var results = new List<CollectedSourceFile>();
        CollectRecursive(projectRootDir, projectRootDir, results);

        // ファイルシステムの列挙順は非決定的なため、
        // 相対パスの序数(Ordinal)ソートで決定的な順序に固定する。
        // ※これは「初出順(台帳)」の判定順ではなく、単一ファイル内の処理決定性のため。
        return results
            .OrderBy(f => f.RelativePathFromProjectRoot, StringComparer.Ordinal)
            .ToList();
    }

    private static void CollectRecursive(
        string currentDir, string projectRootDir, List<CollectedSourceFile> results)
    {
        foreach (var dir in Directory.EnumerateDirectories(currentDir))
        {
            var dirName = Path.GetFileName(dir);
            if (ExcludedDirNames.Contains(dirName, StringComparer.OrdinalIgnoreCase))
                continue;

            CollectRecursive(dir, projectRootDir, results);
        }

        foreach (var file in Directory.EnumerateFiles(currentDir, "*.cs"))
        {
            if (ExcludedFileSuffixes.Any(suffix =>
                    file.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
                continue;

            var relative = Path.GetRelativePath(projectRootDir, file)
                .Replace(Path.DirectorySeparatorChar, '/'); // Windows/他OS間でパス区切りを統一

            results.Add(new CollectedSourceFile(file, relative));
        }
    }
}
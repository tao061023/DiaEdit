namespace DiaEdit.DocGen.Extraction;

using DiaEdit.DocGen.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
public static class PartialTypeMerger
{
    public static IReadOnlyList<MergedTypeDeclaration> Merge(
        IReadOnlyList<RawTypeDeclaration> raw)
    {
        var groups = raw.GroupBy(r => (r.Namespace, r.ContainingTypeName, r.TypeName));

        var merged = new List<MergedTypeDeclaration>();
        foreach (var group in groups)
        {
            var parts = group
                .OrderBy(g => IsMainFile(g) ? 0 : 1)
                .ThenBy(g => Path.GetFileName(g.AbsoluteFilePath), StringComparer.Ordinal)
                .ToList();

            var primary = parts[0];

            // enum は複数ファイルに分割される想定がないため、先頭のEnumSyntaxをそのまま使う
            var enumSyntax = parts.Select(p => p.EnumSyntax).FirstOrDefault(s => s is not null);

            var allSyntaxes = parts
                .Select(p => p.Syntax)
                .Where(s => s is not null)
                .Select(s => s!)
                .ToList();

            merged.Add(new MergedTypeDeclaration(
                Namespace: group.Key.Namespace,
                TypeName: group.Key.TypeName,
                ContainingTypeName: group.Key.ContainingTypeName,
                PrimaryFilePath: primary.AbsoluteFilePath,
                PrimaryRelativePath: primary.RelativePathFromProjectRoot,
                AllSyntaxes: allSyntaxes,
                EnumSyntax: enumSyntax));
        }
        return merged;
    }

    private static bool IsMainFile(RawTypeDeclaration r) =>
        Path.GetFileNameWithoutExtension(r.AbsoluteFilePath) == r.TypeName;
}
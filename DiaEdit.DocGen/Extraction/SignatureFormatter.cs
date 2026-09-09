namespace DiaEdit.DocGen.Extraction;

using DiaEdit.DocGen.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
public static class SignatureFormatter
{
    // メソッド：修飾子 + 戻り値 + 名前 + 型引数 + 引数リスト（本体は含めない）
    public static string FormatMethod(MethodDeclarationSyntax m)
    {
        var modifiers = string.Join(" ", m.Modifiers.Select(x => x.Text));
        var typeParams = m.TypeParameterList?.ToString() ?? "";
        var parameters = FormatParameterList(m.ParameterList);
        return $"{modifiers} {m.ReturnType} {m.Identifier}{typeParams}{parameters}".Trim();
    }

    // コンストラクタ
    public static string FormatConstructor(ConstructorDeclarationSyntax c)
    {
        var modifiers = string.Join(" ", c.Modifiers.Select(x => x.Text));
        return $"{modifiers} {c.Identifier}{FormatParameterList(c.ParameterList)}".Trim();
    }

    // プロパティ：型 名前 { get; set; } のアクセサ有無のみ反映（実装は含めない）
    public static string FormatProperty(PropertyDeclarationSyntax p)
    {
        var modifiers = string.Join(" ", p.Modifiers.Select(x => x.Text));
        var accessors = p.AccessorList?.Accessors
            .Select(a => a.Modifiers.Count > 0
                ? $"{string.Join(" ", a.Modifiers.Select(x => x.Text))} {a.Keyword}"
                : a.Keyword.ToString())
            ?? Enumerable.Empty<string>();
        var accessorText = string.Join("; ", accessors);
        var defaultValue = p.Initializer is not null ? $" {p.Initializer}" : "";
        return $"{modifiers} {p.Type} {p.Identifier} {{ {accessorText}; }}{defaultValue}".Trim();
    }

    // positional record: record struct SelectionKey(Type Name, ...) のフィールド一覧化
    public static IReadOnlyList<(string Type, string Name)> FormatRecordParameters(
        ParameterListSyntax? paramList)
    {
        if (paramList is null) return Array.Empty<(string, string)>();
        return paramList.Parameters
            .Select(p => (p.Type?.ToString() ?? "?", p.Identifier.ToString()))
            .ToList();
    }

    // 改行・インデントを畳んで1行の引数リストに整形（元コードのフォーマットに引きずられないように）
    private static string FormatParameterList(ParameterListSyntax paramList)
    {
        var parts = paramList.Parameters.Select(p =>
        {
            var mods = p.Modifiers.Count > 0 ? string.Join(" ", p.Modifiers.Select(x => x.Text)) + " " : "";
            var defaultVal = p.Default is not null ? $" {p.Default}" : "";
            return $"{mods}{p.Type} {p.Identifier}{defaultVal}";
        });
        return $"({string.Join(", ", parts)})";
    }
}
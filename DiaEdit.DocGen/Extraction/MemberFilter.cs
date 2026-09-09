namespace DiaEdit.DocGen.Extraction;

using DiaEdit.DocGen.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public static class MemberFilter
{
    private static readonly HashSet<string> ExcludedModifiers = new() { "private" };

    public static bool ShouldInclude(MemberDeclarationSyntax member)
    {
        var modifiers = member.Modifiers.Select(m => m.Text).ToHashSet();

        // private のみ除外。ただし private かつ static かつ readonly（定数実装詳細）も除外対象
        if (modifiers.Contains("private") && !modifiers.Contains("protected"))
            return false; // 純粋なprivateのみ除外。private protectedは通す

        // アクセス修飾子が一切ない場合：
        // - interface内メンバー → 暗黙public → 含める
        // - それ以外（クラス内など）→ 暗黙private → 除外
        var hasExplicitAccessibility = modifiers.Overlaps(
            new[] { "public", "internal", "protected" });

        if (!hasExplicitAccessibility)
        {
            var isInterfaceMember = member.Parent is InterfaceDeclarationSyntax;
            return isInterfaceMember;
        }

        return true; // public/internal/protected/protected internal/private protected
    }
}
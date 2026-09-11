namespace DiaEdit.DocGen.Extraction;

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DiaEdit.DocGen.Models;

public static class XmlDocParser
{
    public static XmlDocSummary Parse(SyntaxNode node)
    {
        var trivia = node.GetLeadingTrivia()
            .Select(t => t.GetStructure())
            .OfType<DocumentationCommentTriviaSyntax>()
            .FirstOrDefault();

        if (trivia is null)
            return new XmlDocSummary(null, null, new Dictionary<string, string>(), null,
                Array.Empty<(string, string)>());

        var summary = ExtractElementText(trivia, "summary");
        var remarks = ExtractElementText(trivia, "remarks");
        var returns = ExtractElementText(trivia, "returns");

        var paramDict = new Dictionary<string, string>();
        foreach (var paramElem in trivia.Content.OfType<XmlElementSyntax>()
                     .Where(e => e.StartTag.Name.ToString() == "param"))
        {
            var name = paramElem.StartTag.Attributes
                .OfType<XmlNameAttributeSyntax>()
                .FirstOrDefault()?.Identifier.ToString();
            if (name is not null)
                paramDict[name] = NormalizeWhitespace(GetInnerText(paramElem));
        }

        var exceptions = trivia.Content.OfType<XmlElementSyntax>()
            .Where(e => e.StartTag.Name.ToString() == "exception")
            .Select(e => (CrefText(e.StartTag), NormalizeWhitespace(GetInnerText(e))))
            .ToList();

        return new XmlDocSummary(summary, remarks, paramDict, returns, exceptions);
    }

    private static string? ExtractElementText(DocumentationCommentTriviaSyntax trivia, string tag)
    {
        var elem = trivia.Content.OfType<XmlElementSyntax>()
            .FirstOrDefault(e => e.StartTag.Name.ToString() == tag);
        return elem is null ? null : NormalizeWhitespace(GetInnerText(elem));
    }

    private static string GetInnerText(XmlElementSyntax elem)
    {
        var sb = new StringBuilder();
        foreach (var node in elem.Content)
        {
            switch (node)
            {
                case XmlTextSyntax text:
                    foreach (var token in text.TextTokens)
                        sb.Append(token.ToString());
                    break;

                case XmlEmptyElementSyntax empty when empty.Name.ToString() == "br":
                    sb.Append('\n');
                    break;

                case XmlEmptyElementSyntax empty:
                    // <see cref="..."/> や <paramref name="..."/> 等の自己終了タグ。
                    // 参照先テキストが取れればそれを、取れなければ何も追記しない（黙って無視）。
                    var emptyRefText = TryGetReferenceText(empty.Name.ToString(), empty.Attributes);
                    if (emptyRefText is not null)
                        sb.Append(emptyRefText);
                    break;

                case XmlElementSyntax nested:
                    // <see cref="...">テキスト</see> のように開始・終了タグを持つ形。
                    // cref参照が取れればそれを優先、無ければ内部テキストを再帰的に拾う。
                    var nestedRefText = TryGetReferenceText(
                        nested.StartTag.Name.ToString(), nested.StartTag.Attributes);
                    sb.Append(nestedRefText ?? GetInnerText(nested));
                    break;
            }
        }
        return sb.ToString();
    }

    // <see cref="X"/> / <seealso cref="X"/> → "X"
    // <paramref name="x"/> / <typeparamref name="x"/> → "x"
    // 上記以外のタグ名は対象外（null を返し、既存の再帰/無視にフォールバック）
    private static string? TryGetReferenceText(
        string tagName, SyntaxList<XmlAttributeSyntax> attributes)
    {
        if (tagName is "see" or "seealso")
        {
            var cref = attributes.OfType<XmlCrefAttributeSyntax>().FirstOrDefault();
            return cref?.Cref.ToString();
        }

        if (tagName is "paramref" or "typeparamref")
        {
            var name = attributes.OfType<XmlNameAttributeSyntax>().FirstOrDefault();
            return name?.Identifier.ToString();
        }

        return null;
    }

    private static string NormalizeWhitespace(string s)
    {
        var lines = s.Replace("\r\n", "\n").Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0);
        return string.Join("\n", lines).Trim();
    }

    private static string CrefText(XmlElementStartTagSyntax startTag)
    {
        var crefAttr = startTag.Attributes
            .OfType<XmlCrefAttributeSyntax>()
            .FirstOrDefault();
        return crefAttr?.Cref.ToString() ?? "";
    }
}
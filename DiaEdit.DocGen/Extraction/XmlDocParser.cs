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
                case XmlElementSyntax nested:
                    // <see cref="..."/> 等がネストされている場合、参照先の型名だけ拾う
                    var nestedCref = nested.StartTag.Attributes
                        .OfType<XmlCrefAttributeSyntax>().FirstOrDefault();
                    sb.Append(nestedCref is not null
                        ? nestedCref.Cref.ToString()
                        : GetInnerText(nested));
                    break;
            }
        }
        return sb.ToString();
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
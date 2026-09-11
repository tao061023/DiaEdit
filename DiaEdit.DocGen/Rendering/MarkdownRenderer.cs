namespace DiaEdit.DocGen.Rendering;

using DiaEdit.DocGen.Models;

public static class MarkdownRenderer
{
    public static string RenderType(TypeDocModel type)
    {
        var lines = new List<string>();

        lines.Add($"#### `{type.Namespace}.{type.TypeName}` ({type.Kind})");
        lines.Add("");
        if (type.Summary is not null)
        {
            lines.Add(type.Summary);
            lines.Add("");
        }

        // positional record のフィールド一覧（record struct SelectionKey(...) 形式）
        if (type.RecordParameters.Count > 0)
        {
            lines.Add("| Field | Type |");
            lines.Add("|---|---|");
            foreach (var (fieldType, name) in type.RecordParameters)
                lines.Add($"| {name} | `{fieldType}` |");
            lines.Add("");
        }

        // 通常クラス/recordのプロパティ一覧（Station.cs のようなPOCO向け）
        if (type.Properties.Count > 0)
        {
            var hasAnyDescription = type.Properties.Any(p => p.Summary is not null || p.Remarks is not null);
            if (hasAnyDescription)
            {
                lines.Add("| Field | Type | 説明 |");
                lines.Add("|---|---|---|");
                foreach (var p in type.Properties)
                {
                    var typeCell = p.DefaultValue is null
                        ? $"`{p.Type}`"
                        : $"`{p.Type}` (既定値 `{p.DefaultValue}`)";
                    lines.Add($"| {p.Name} | {typeCell} | {BuildPropertyDescriptionCell(p.Summary, p.Remarks)} |");
                }
            }
            else
            {
                // 全プロパティにコメントが無い場合は説明列自体を省略してさらに圧縮
                lines.Add("| Field | Type |");
                lines.Add("|---|---|");
                foreach (var p in type.Properties)
                {
                    var typeCell = p.DefaultValue is null
                        ? $"`{p.Type}`"
                        : $"`{p.Type}` (既定値 `{p.DefaultValue}`)";
                    lines.Add($"| {p.Name} | {typeCell} |");
                }
            }
            lines.Add("");
        }

        if (type.Remarks is not null)
        {
            lines.Add("> " + type.Remarks.Replace("\n", "\n> "));
            lines.Add("");
        }

        if (type.EnumMembers.Count > 0)
        {
            lines.Add("| Value | 説明 |");
            lines.Add("|---|---|");
            foreach (var m in type.EnumMembers)
                lines.Add($"| {m.Name} | {m.Summary ?? ""} |");
            lines.Add("");
        }

        // メソッド・コンストラクタのみ、従来通りフル展開
        foreach (var member in type.Members)
        {
            lines.Add("---");
            lines.Add("");
            lines.Add($"##### `{member.Signature}`");
            lines.Add("");

            if (member.Doc.Summary is not null)
            {
                lines.Add(member.Doc.Summary);
                lines.Add("");
            }

            if (member.Doc.Params.Count > 0)
            {
                lines.Add("**Parameters**");
                lines.Add("");
                foreach (var (name, desc) in member.Doc.Params)
                    lines.Add($"- `{name}`: {desc}");
                lines.Add("");
            }

            if (member.Doc.Returns is not null)
            {
                lines.Add("**Returns**");
                lines.Add(member.Doc.Returns);
                lines.Add("");
            }

            if (member.Doc.Remarks is not null)
            {
                lines.Add("**Remarks**");
                lines.Add(member.Doc.Remarks);
                lines.Add("");
            }
        }

        while (lines.Count > 0 && lines[^1] == "")
            lines.RemoveAt(lines.Count - 1);

        return string.Join("\n", lines);
    }

    // プロパティ用の説明セルを組み立てる。SummaryとRemarksを両方持つ場合は区切って両方載せる。
    private static string BuildPropertyDescriptionCell(string? summary, string? remarks)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(summary))
            parts.Add(SanitizeForTableCell(summary));
        if (!string.IsNullOrEmpty(remarks))
            parts.Add($"**制約/備考：** {SanitizeForTableCell(remarks)}");
        return string.Join("<br><br>", parts);
    }

    // テーブルセル内に生の改行や "|" が入るとMarkdownテーブル構文が壊れるため、
    // 改行は<br>に、パイプはエスケープに変換する（Switcher.Mechanism等、複数行Summaryで既に発生していた問題の修正を兼ねる）。
    private static string SanitizeForTableCell(string text) =>
        text.Replace("|", "\\|").Replace("\n", "<br>");
}
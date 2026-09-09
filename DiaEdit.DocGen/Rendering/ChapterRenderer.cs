namespace DiaEdit.DocGen.Rendering;

using System.Text;
using DiaEdit.DocGen.Manifest;
using DiaEdit.DocGen.Models;

public static class ChapterRenderer
{
    /// <summary>
    /// 1プロジェクト分の章を、manifestのSections/Subfolders番号順に並べて丸ごと構築する。
    /// </summary>
    public static string RenderChapter(
        string projectName,
        DocGenManifest.ProjectEntry entry,
        string chapterTitle,
        IReadOnlyDictionary<string, IReadOnlyList<TypeDocModel>> contentByFolderKey)
        // contentByFolderKey: "Model/Stations" 等 → そのフォルダに属する型のドキュメント一覧
    {
        var lines = new List<string>
        {
            DocGenMarkers.ChapterBegin(projectName),
            $"## {entry.Number}. {chapterTitle}",
            ""
        };

        foreach (var section in entry.Sections.OrderBy(s => ParseNum(s.Value.Number)))
        {
            var topFolder = section.Key;
            lines.Add($"### {section.Value.Number} {topFolder}");
            lines.Add("");

            // このセクション直下ファイル（サブフォルダ無し）
            if (contentByFolderKey.TryGetValue(topFolder, out var directTypes) && directTypes.Count > 0)
            {
                AppendTypes(lines, directTypes);
            }

            // サブフォルダ（項）を番号順に
            foreach (var sub in section.Value.Subfolders.OrderBy(s => ParseNum(s.Value)))
            {
                if (lines.Count > 0 && lines[^1] != "") lines.Add(""); // 念のための保険
                var folderKey = $"{topFolder}/{sub.Key}";
                lines.Add($"#### {sub.Value} {sub.Key}");
                lines.Add("");

                if (contentByFolderKey.TryGetValue(folderKey, out var subTypes) && subTypes.Count > 0)
                    AppendTypes(lines, subTypes);
            }
        }

        while (lines.Count > 0 && lines[^1] == "") lines.RemoveAt(lines.Count - 1);
        lines.Add("");
        lines.Add(DocGenMarkers.ChapterEnd(projectName));

        return string.Join("\n", lines);
    }

    private static void AppendTypes(List<string> lines, IReadOnlyList<TypeDocModel> types)
    {
        var trivial = types.Where(t => t.IsTrivialIdWrapper())
                            .OrderBy(t => t.TypeName, StringComparer.Ordinal).ToList();
        var substantial = types.Except(trivial)
                            .OrderBy(t => t.TypeName, StringComparer.Ordinal).ToList();

        if (trivial.Count > 0)
        {
            lines.Add($"**ID型一覧**：{string.Join(", ", trivial.Select(t => $"`{t.TypeName}`"))}");
            lines.Add("");
            lines.Add("---");
            lines.Add("");
        }

        for (var i = 0; i < substantial.Count; i++)
        {
            lines.Add(MarkdownRenderer.RenderType(substantial[i]));
            if (i < substantial.Count - 1) { lines.Add(""); lines.Add("---"); lines.Add(""); }
        }
        lines.Add("");
    }

    private static string ParseNum(string number) =>
        string.Join(".", number.Split('.').Select(n => n.PadLeft(4, '0')));
}
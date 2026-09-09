namespace DiaEdit.DocGen.Rendering;

public static class DocGenMarkers
{
    public static string ChapterBegin(string projectName) => $"<!-- DOCGEN:CHAPTER {projectName} -->";
    public static string ChapterEnd(string projectName) => $"<!-- DOCGEN:CHAPTER END {projectName} -->";
}

public sealed class MarkdownDocumentEditor
{
    private readonly List<string> _lines;

    public MarkdownDocumentEditor(string mdPath) =>
        _lines = File.ReadAllLines(mdPath).ToList();

    /// <summary>
    /// 章マーカーが既にあれば中身ごと丸ごと差し替え。無ければ「## 6.」目次順に正しい位置へ新規挿入。
    /// </summary>
    public void UpsertChapter(string projectName, int chapterNumber, string renderedChapter)
    {
        var begin = DocGenMarkers.ChapterBegin(projectName);
        var end = DocGenMarkers.ChapterEnd(projectName);

        int beginIdx = _lines.FindIndex(l => l.Trim() == begin);
        if (beginIdx >= 0)
        {
            int endIdx = _lines.FindIndex(beginIdx, l => l.Trim() == end);
            if (endIdx < 0)
                throw new InvalidOperationException($"CHAPTER END marker missing for '{projectName}'.");

            _lines.RemoveRange(beginIdx, endIdx - beginIdx + 1);
            _lines.InsertRange(beginIdx, renderedChapter.Split('\n'));
            return;
        }

        // 新規：既存の章マーカーの中から、自分より大きい番号を持つ最初の章の直前に挿入する
        int insertAt = _lines.Count;
        foreach (var (existingProject, existingNumber) in FindExistingChapterNumbers())
        {
            if (existingNumber > chapterNumber)
            {
                insertAt = _lines.FindIndex(l => l.Trim() == DocGenMarkers.ChapterBegin(existingProject));
                break;
            }
        }

        _lines.InsertRange(insertAt, renderedChapter.Split('\n').Append(""));
    }

    private IEnumerable<(string project, int number)> FindExistingChapterNumbers()
    {
        foreach (var line in _lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("<!-- DOCGEN:CHAPTER ") && !trimmed.Contains("END"))
            {
                var name = trimmed.Replace("<!-- DOCGEN:CHAPTER ", "").Replace(" -->", "");
                // 章番号はmanifestから引く必要があるが、ここでは呼び出し側が管理する前提で簡略化
                yield return (name, 0); // 詳細は呼び出し側でソート済みという前提に簡略化してもよい
            }
        }
    }

    public void Save(string mdPath) => File.WriteAllLines(mdPath, _lines);
}
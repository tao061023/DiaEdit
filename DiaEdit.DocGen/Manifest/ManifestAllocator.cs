// Manifest/ManifestAllocator.cs
namespace DiaEdit.DocGen.Manifest;

public static class ManifestAllocator
{
    public static void AllocateMissingFolders(
        DocGenManifest.ProjectEntry entry,
        IReadOnlyList<string> allFolderKeys) // "Algorithm/CacheBuilder" 等
    {
        // 深さ1(第一階層フォルダ)のみ抽出。今回の対象(CacheBuilder/Dependency)は既に深さ2なので
        // 実際には深さ2以上の採番ロジックが必要。ここでは「未登録の第一階層」「未登録の第二階層」を両対応する。
        var topFolders = allFolderKeys
            .Select(k => k.Split('/', 2)[0])
            .Distinct()
            .OrderBy(f => f, StringComparer.Ordinal);

        int nextTopNumber = entry.Sections.Values
            .Select(s => int.Parse(s.Number.Split('.')[1]))
            .DefaultIfEmpty(0)
            .Max() + 1;

        foreach (var top in topFolders)
        {
            if (!entry.Sections.ContainsKey(top))
            {
                entry.Sections[top] = new DocGenManifest.SectionEntry
                {
                    Number = $"{entry.Number}.{nextTopNumber}",
                    Subfolders = new Dictionary<string, string>()
                };
                nextTopNumber++;
            }
        }

        foreach (var key in allFolderKeys.Where(k => k.Contains('/')).OrderBy(k => k, StringComparer.Ordinal))
        {
            var segments = key.Split('/', 2);
            var top = segments[0];
            var rest = segments[1];

            var section = entry.Sections[top];
            if (section.Subfolders.ContainsKey(rest)) continue;

            int nextSubNumber = section.Subfolders.Values
                .Select(n => int.Parse(n.Split('.').Last()))
                .DefaultIfEmpty(0)
                .Max() + 1;

            section.Subfolders[rest] = $"{section.Number}.{nextSubNumber}";
        }
    }
}
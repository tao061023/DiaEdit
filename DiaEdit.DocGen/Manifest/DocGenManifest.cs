namespace DiaEdit.DocGen.Manifest;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class DocGenManifest
{
    [JsonPropertyName("DiaEditCore")]
    public ProjectEntry? DiaEditCore { get; set; }

    [JsonPropertyName("DiaEditApp.ViewModels")]
    public ProjectEntry? DiaEditAppViewModels { get; set; }

    [JsonPropertyName("DiaEditApp")]
    public ProjectEntry? DiaEditApp { get; set; }

    // プロジェクト名文字列からエントリを引くためのヘルパー（Program.cs側の利便性のため）
    [JsonIgnore]
    public Dictionary<string, ProjectEntry> Projects => new()
    {
        ["DiaEditCore"] = DiaEditCore ?? new ProjectEntry(),
        ["DiaEditApp.ViewModels"] = DiaEditAppViewModels ?? new ProjectEntry(),
        ["DiaEditApp"] = DiaEditApp ?? new ProjectEntry(),
    };

    public sealed class ProjectEntry
    {
        public int Number { get; set; }
        public Dictionary<string, SectionEntry> Sections { get; set; } = new();
    }

    public sealed class SectionEntry
    {
        public string Number { get; set; } = "";
        public Dictionary<string, string> Subfolders { get; set; } = new();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static DocGenManifest Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"Manifest file not found: {path}. Sections must be manually seeded before first run.");

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<DocGenManifest>(json, JsonOptions)
               ?? throw new InvalidOperationException($"Failed to parse manifest: {path}");
    }

    public void Save(string path)
    {
        SortSubfolders(DiaEditCore);
        SortSubfolders(DiaEditAppViewModels);
        SortSubfolders(DiaEditApp);

        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(path, json);
    }

    private static void SortSubfolders(ProjectEntry? entry)
    {
        if (entry is null) return;
        foreach (var section in entry.Sections.Values)
        {
            section.Subfolders = section.Subfolders
                .OrderBy(kv => ParseSortKey(kv.Value))
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        }
    }

    private static string ParseSortKey(string number) =>
        string.Join(".", number.Split('.').Select(n => n.PadLeft(4, '0')));
}
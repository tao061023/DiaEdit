namespace DiaEditCore.Model;

public sealed class DisplayName
{
    public required string Name { get; set; }
    public string? Abbreviation { get; set; }
    public Dictionary<string, string> Translations { get; set; } = new();

    public string Resolve(string localeCode)
        => Translations.TryGetValue(localeCode, out var v) ? v : Name;

    public string ResolveCompact(string localeCode)
        => Abbreviation ?? Resolve(localeCode);
}
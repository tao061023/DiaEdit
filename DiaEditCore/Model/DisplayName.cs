namespace DiaEditCore.Model;

public sealed class DisplayName
{
    public required string Name { get; set; }
    public string? Abbreviation { get; set; }
    public Dictionary<string, string> Translations { get; set; } = new();

    public string Resolve(string localeCode)
        => Translations.TryGetValue(localeCode, out var v) ? v : Name;

    /// <summary>
    /// Name/Abbreviation/Translationsをディープコピーした新しいDisplayNameを返す。
    /// DisplayNameは参照型（class）かつTranslationsがミュータブルなDictionaryのため、
    /// スナップショット保持（UndoableCommand等）で外部参照を残さないために使う。
    /// </summary>
    public DisplayName Clone() => new()
    {
        Name = Name,
        Abbreviation = Abbreviation,
        Translations = new Dictionary<string, string>(Translations)
    };
}

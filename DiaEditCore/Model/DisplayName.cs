namespace DiaEditCore.Model;

using System;
using System.Linq;

/// <summary>
/// 多言語対応、および略称を保持するための共通型
/// </summary>
public sealed class DisplayName : IEquatable<DisplayName>
{
    /// <summary>
    /// 正式名称
    /// </summary>
    public required string Name { get; set; }
    /// <summary>
    /// 略称
    /// </summary>
    public string? Abbreviation { get; set; }
    /// <summary>
    /// BCP47言語コード＋訳語
    /// </summary>
    public Dictionary<string, string> Translations { get; set; } = new();

    /// <summary>
    /// localCodeに紐づくNameまたはAbbreviationを引き当てる
    /// </summary>
    /// <param name="localeCode">言語コード</param>
    /// <returns>
    /// <list type="bullet">
    /// <item><description><c>localeCodeに紐づく値がある場合</c>：v</description></item>
    /// <item><description><c>localeCodeに紐づく値がない場合</c>：Name</description></item>
    /// </list>
    /// </returns>
    public string Resolve(string localeCode)
        => Translations.TryGetValue(localeCode, out var v) ? v : Name;

    /// <summary>
    /// Name/Abbreviation/Translationsをディープコピーした新しいDisplayNameを返す。
    /// </summary>
    /// <remarks>
    /// DisplayNameは参照型（class）かつTranslationsがミュータブルなDictionaryのため、
    /// スナップショット保持（UndoableCommand等）で外部参照を残さないために使う。
    /// </remarks>
    public DisplayName Clone() => new()
    {
        Name = Name,
        Abbreviation = Abbreviation,
        Translations = new Dictionary<string, string>(Translations)
    };

    /// <summary>
    /// 値等価の実装。参照型であるDisplayNameをスナップショット比較に対応させる。
    /// </summary>
    public bool Equals(DisplayName? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name
            && Abbreviation == other.Abbreviation
            && Translations.Count == other.Translations.Count
            && Translations.All(kv => other.Translations.TryGetValue(kv.Key, out var v) && v == kv.Value);
    }

    public override bool Equals(object? obj) => Equals(obj as DisplayName);

    public override int GetHashCode() => HashCode.Combine(Name, Abbreviation, Translations.Count);
}

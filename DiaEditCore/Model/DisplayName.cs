namespace DiaEditCore.Model;

using System;
using System.Linq;

public sealed class DisplayName : IEquatable<DisplayName>
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

    /// <summary>
    /// 値等価の実装（§9.2項目31：Save時差分判定のため新設）。DisplayNameは参照型だが、
    /// スナップショット比較（変更なしなら保存操作自体を無効化する）用途では内容の一致を
    /// 見る必要があるため、Translationsも含め中身で比較する。
    /// StationSnapshot（record）はメンバの既定比較にEqualityComparer&lt;DisplayName&gt;.Defaultを
    /// 使うため、これが未実装だと参照比較にフォールバックし、Clone()由来の別インスタンス同士は
    /// 常に不一致と判定されてしまう（§9.2項目31実装時に実機で確認された不具合の原因）。
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

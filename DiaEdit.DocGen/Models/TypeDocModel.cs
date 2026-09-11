namespace DiaEdit.DocGen.Models;

public sealed record TypeDocModel(
    string Namespace,
    string TypeName,
    string Kind,
    string? Summary,
    string? Remarks,
    IReadOnlyList<(string Type, string Name)> RecordParameters,
    IReadOnlyList<PropertyDocModel> Properties,   // ← 追加
    IReadOnlyList<MemberDocModel> Members,         // ← プロパティは含めない。メソッド/コンストラクタのみ
    IReadOnlyList<EnumMemberDocModel> EnumMembers);

public sealed record PropertyDocModel(
    string Type,
    string Name,
    string? DefaultValue,
    string? Summary,
    string? Remarks);   // ← 追加：プロパティ単位の使用制約・検証ルールを保持する

public sealed record MemberDocModel(
    string Kind, // "method" / "constructor"（"property" は廃止、Propertiesへ移動）
    string Signature,
    XmlDocSummary Doc);

public sealed record EnumMemberDocModel(
    string Name,
    string? Summary);

public static class TypeDocModelExtensions
{
    // "Value: int" または "Id: XxxId" の1フィールドのみ、メンバー無し、コメント無し
    public static bool IsTrivialIdWrapper(this TypeDocModel t) =>
        t.Members.Count == 0
        && t.Summary is null
        && t.RecordParameters.Count == 1
        && (t.RecordParameters[0].Name is "Value" or "Id");
}
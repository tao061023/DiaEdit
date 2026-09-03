namespace DiaEditCore.Serialization.Json;

using System.Text.Json;
using System.Text.Json.Serialization;

using DiaEditCore.Model;
using DiaEditCore.Model.TimeTable;

/// <summary>
/// RestrictionTarget（判別共用体：Segment / Rail）用の独自JsonConverter。
/// 組み込みの[JsonPolymorphic]/[JsonDerivedType]属性を使わない理由：それらをModel層のクラスに
/// 直接付けると、Model層がSystem.Text.Json.Serializationへ依存することになり、
/// 「Modelは薄いPOCOであるべき」という層分離の原則に反するため（§8.2項目2、v11.36で確定）。
///
/// 種別ラベルは"kind"フィールドに明示する（値の形からの逆算による判別は、将来型が増えた際に
/// 形が衝突する可能性を排除できないため採用しない。§8.2項目2 v11.36変更履歴参照）。
///
/// 他の判別共用体（TrainCutPoint関連やRailEndpointRef等）にも同一パターンを適用する。
/// このクラスをテンプレートとして、対象の共用体ごとに専用Converterを1つずつ用意する方針とする
/// （JsonConverterFactoryによる汎用化は、共用体ごとに派生型・保持フィールドの形が異なるため見送った）。
/// </summary>
public sealed class RestrictionTargetJsonConverter : JsonConverter<RestrictionTarget>
{
    private const string KindField = "kind";
    private const string SegmentKind = "Segment";
    private const string RailKind = "Rail";

    public override RestrictionTarget Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (!root.TryGetProperty(KindField, out var kindProp))
            throw new JsonException($"RestrictionTarget: \"{KindField}\"フィールドが存在しない");

        var kind = kindProp.GetString();
        return kind switch
        {
            SegmentKind => new RestrictionTarget.Segment(
                ReadId<StationConnectionSegmentId>(root, "stationConnectionSegmentId", options)),
            RailKind => new RestrictionTarget.Rail(
                ReadId<RailId>(root, "railId", options)),
            _ => throw new JsonException($"RestrictionTarget: 未知のkind \"{kind}\""),
        };
    }

    public override void Write(Utf8JsonWriter writer, RestrictionTarget value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        switch (value)
        {
            case RestrictionTarget.Segment segment:
                writer.WriteString(KindField, SegmentKind);
                writer.WritePropertyName("stationConnectionSegmentId");
                JsonSerializer.Serialize(writer, segment.StationConnectionSegmentId, options);
                break;

            case RestrictionTarget.Rail rail:
                writer.WriteString(KindField, RailKind);
                writer.WritePropertyName("railId");
                JsonSerializer.Serialize(writer, rail.RailId, options);
                break;

            default:
                throw new JsonException($"RestrictionTarget: 未対応の派生型 {value.GetType().Name}");
        }
        writer.WriteEndObject();
    }

    private static TId ReadId<TId>(JsonElement root, string propertyName, JsonSerializerOptions options)
    {
        if (!root.TryGetProperty(propertyName, out var prop))
            throw new JsonException($"RestrictionTarget: \"{propertyName}\"フィールドが存在しない");

        return prop.Deserialize<TId>(options)!;
    }
}

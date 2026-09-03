namespace DiaEditCore.Serialization.Json;

using System.Text.Json;
using System.Text.Json.Serialization;

using DiaEditCore.Model;

/// <summary>
/// ObjectId（判別共用体：BoundaryPoint / EntryPoint / BufferStop / Switcher /
/// VirtualConflictObject / Rail / StationConnectionSegment）用の独自JsonConverter。
/// 実装パターンはRestrictionTargetJsonConverterと同一（7.3.1節、§8.2項目13）。
/// 各派生型は対応するID型を1つだけ保持するという共通の形を持つが、将来この前提が崩れる
/// 可能性を排除できないため、あえて汎用化せず本Converter内で派生型ごとに明示的に分岐する。
/// </summary>
public sealed class ObjectIdJsonConverter : JsonConverter<ObjectId>
{
    private const string KindField = "kind";
    private const string BoundaryPointKind = "BoundaryPoint";
    private const string EntryPointKind = "EntryPoint";
    private const string BufferStopKind = "BufferStop";
    private const string SwitcherKind = "Switcher";
    private const string VirtualConflictObjectKind = "VirtualConflictObject";
    private const string RailKind = "Rail";
    private const string StationConnectionSegmentKind = "StationConnectionSegment";

    public override ObjectId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (!root.TryGetProperty(KindField, out var kindProp))
            throw new JsonException($"ObjectId: \"{KindField}\"フィールドが存在しない");

        var kind = kindProp.GetString();
        return kind switch
        {
            BoundaryPointKind => new BoundaryPointObjectId(ReadId<BoundaryPointId>(root, "id", options)),
            EntryPointKind => new EntryPointObjectId(ReadId<EntryPointId>(root, "id", options)),
            BufferStopKind => new BufferStopObjectId(ReadId<BufferStopId>(root, "id", options)),
            SwitcherKind => new SwitcherObjectId(ReadId<SwitcherId>(root, "id", options)),
            VirtualConflictObjectKind => new VirtualConflictObjectIdObject(ReadId<VirtualConflictObjectId>(root, "id", options)),
            RailKind => new RailObjectId(ReadId<RailId>(root, "id", options)),
            StationConnectionSegmentKind => new StationConnectionSegmentObjectId(ReadId<StationConnectionSegmentId>(root, "id", options)),
            _ => throw new JsonException($"ObjectId: 未知のkind \"{kind}\""),
        };
    }

    public override void Write(Utf8JsonWriter writer, ObjectId value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        switch (value)
        {
            case BoundaryPointObjectId v:
                writer.WriteString(KindField, BoundaryPointKind);
                writer.WritePropertyName("id");
                JsonSerializer.Serialize(writer, v.Id, options);
                break;

            case EntryPointObjectId v:
                writer.WriteString(KindField, EntryPointKind);
                writer.WritePropertyName("id");
                JsonSerializer.Serialize(writer, v.Id, options);
                break;

            case BufferStopObjectId v:
                writer.WriteString(KindField, BufferStopKind);
                writer.WritePropertyName("id");
                JsonSerializer.Serialize(writer, v.Id, options);
                break;

            case SwitcherObjectId v:
                writer.WriteString(KindField, SwitcherKind);
                writer.WritePropertyName("id");
                JsonSerializer.Serialize(writer, v.Id, options);
                break;

            case VirtualConflictObjectIdObject v:
                writer.WriteString(KindField, VirtualConflictObjectKind);
                writer.WritePropertyName("id");
                JsonSerializer.Serialize(writer, v.Id, options);
                break;

            case RailObjectId v:
                writer.WriteString(KindField, RailKind);
                writer.WritePropertyName("id");
                JsonSerializer.Serialize(writer, v.Id, options);
                break;

            case StationConnectionSegmentObjectId v:
                writer.WriteString(KindField, StationConnectionSegmentKind);
                writer.WritePropertyName("id");
                JsonSerializer.Serialize(writer, v.Id, options);
                break;

            default:
                throw new JsonException($"ObjectId: 未対応の派生型 {value.GetType().Name}");
        }
        writer.WriteEndObject();
    }

    private static TId ReadId<TId>(JsonElement root, string propertyName, JsonSerializerOptions options)
    {
        if (!root.TryGetProperty(propertyName, out var prop))
            throw new JsonException($"ObjectId: \"{propertyName}\"フィールドが存在しない");

        return prop.Deserialize<TId>(options)!;
    }
}

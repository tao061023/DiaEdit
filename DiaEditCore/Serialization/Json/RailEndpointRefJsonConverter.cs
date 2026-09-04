namespace DiaEditCore.Serialization.Json;

using System.Text.Json;
using System.Text.Json.Serialization;

using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;

/// <summary>
/// RailEndpointRef（判別共用体：None / BoundaryPoint / EntryPoint / BufferStop / Switcher）用の
/// 独自JsonConverter。実装パターンはRestrictionTargetJsonConverterと同一（7.3.1節、§8.2項目13）。
/// SwitcherEndpointRefのみportIndexという追加フィールドを持つ点に注意。
/// </summary>
public sealed class RailEndpointRefJsonConverter : JsonConverter<RailEndpointRef>
{
    private const string KindField = "kind";
    private const string NoneKind = "None";
    private const string BoundaryPointKind = "BoundaryPoint";
    private const string EntryPointKind = "EntryPoint";
    private const string BufferStopKind = "BufferStop";
    private const string SwitcherKind = "Switcher";

    public override RailEndpointRef Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (!root.TryGetProperty(KindField, out var kindProp))
            throw new JsonException($"RailEndpointRef: \"{KindField}\"フィールドが存在しない");

        var kind = kindProp.GetString();
        return kind switch
        {
            NoneKind => new NoneEndpointRef(ReadId<NoneEndpointId>(root, "noneEndpointId", options)),
            BoundaryPointKind => new BoundaryPointEndpointRef(ReadId<BoundaryPointId>(root, "boundaryPointId", options)),
            EntryPointKind => new EntryPointEndpointRef(ReadId<EntryPointId>(root, "entryPointId", options)),
            BufferStopKind => new BufferStopEndpointRef(ReadId<BufferStopId>(root, "bufferStopId", options)),
            SwitcherKind => new SwitcherEndpointRef(
                ReadId<SwitcherId>(root, "switcherId", options),
                ReadInt(root, "portIndex")),
            _ => throw new JsonException($"RailEndpointRef: 未知のkind \"{kind}\""),
        };
    }

    public override void Write(Utf8JsonWriter writer, RailEndpointRef value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        switch (value)
        {
            case NoneEndpointRef none:
                writer.WriteString(KindField, NoneKind);
                writer.WritePropertyName("noneEndpointId");
                JsonSerializer.Serialize(writer, none.Id, options);
                break;

            case BoundaryPointEndpointRef boundaryPoint:
                writer.WriteString(KindField, BoundaryPointKind);
                writer.WritePropertyName("boundaryPointId");
                JsonSerializer.Serialize(writer, boundaryPoint.Id, options);
                break;

            case EntryPointEndpointRef entryPoint:
                writer.WriteString(KindField, EntryPointKind);
                writer.WritePropertyName("entryPointId");
                JsonSerializer.Serialize(writer, entryPoint.Id, options);
                break;

            case BufferStopEndpointRef bufferStop:
                writer.WriteString(KindField, BufferStopKind);
                writer.WritePropertyName("bufferStopId");
                JsonSerializer.Serialize(writer, bufferStop.Id, options);
                break;

            case SwitcherEndpointRef switcher:
                writer.WriteString(KindField, SwitcherKind);
                writer.WritePropertyName("switcherId");
                JsonSerializer.Serialize(writer, switcher.Id, options);
                writer.WriteNumber("portIndex", switcher.PortIndex);
                break;

            default:
                throw new JsonException($"RailEndpointRef: 未対応の派生型 {value.GetType().Name}");
        }
        writer.WriteEndObject();
    }

    private static TId ReadId<TId>(JsonElement root, string propertyName, JsonSerializerOptions options)
    {
        if (!root.TryGetProperty(propertyName, out var prop))
            throw new JsonException($"RailEndpointRef: \"{propertyName}\"フィールドが存在しない");

        return prop.Deserialize<TId>(options)!;
    }

    private static int ReadInt(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var prop))
            throw new JsonException($"RailEndpointRef: \"{propertyName}\"フィールドが存在しない");

        return prop.GetInt32();
    }
}

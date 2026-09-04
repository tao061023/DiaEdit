namespace DiaEditCore.Tests.Serialization.Json;

using System.Text.Json;

using DiaEditCore.Model;
using DiaEditCore.Model.Stations.FloorUnitObjects;
using DiaEditCore.Serialization.Json;

using Xunit;

public class RailEndpointRefJsonConverterTests
{
    private static JsonSerializerOptions MakeOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new IntIdJsonConverterFactory());
        options.Converters.Add(new RailEndpointRefJsonConverter());
        return options;
    }

    [Fact]
    public void NoneEndpointRefは往復変換で値が保持される()
    {
        // 旧テスト名「NoneEndpointRefは往復変換できる」から改名：
        // v13.9でNoneEndpointRefがId必須の実体参照になったため、他の4ケース同様
        // Idそのものが往復変換で保持されることまで検証する（旧実装はマーカーのみで中身を持たず、
        // 型判定のみで足りていたが、その前提が崩れたため）。
        var options = MakeOptions();
        RailEndpointRef original = new NoneEndpointRef(new NoneEndpointId(7));

        var json = JsonSerializer.Serialize(original, options);
        var restored = JsonSerializer.Deserialize<RailEndpointRef>(json, options);

        var v = Assert.IsType<NoneEndpointRef>(restored);
        Assert.Equal(new NoneEndpointId(7), v.Id);
    }

    [Fact]
    public void BoundaryPointEndpointRefは往復変換で値が保持される()
    {
        var options = MakeOptions();
        RailEndpointRef original = new BoundaryPointEndpointRef(new BoundaryPointId(1));

        var json = JsonSerializer.Serialize(original, options);
        var restored = JsonSerializer.Deserialize<RailEndpointRef>(json, options);

        var v = Assert.IsType<BoundaryPointEndpointRef>(restored);
        Assert.Equal(new BoundaryPointId(1), v.Id);
    }

    [Fact]
    public void EntryPointEndpointRefは往復変換で値が保持される()
    {
        var options = MakeOptions();
        RailEndpointRef original = new EntryPointEndpointRef(new EntryPointId(2));

        var json = JsonSerializer.Serialize(original, options);
        var restored = JsonSerializer.Deserialize<RailEndpointRef>(json, options);

        var v = Assert.IsType<EntryPointEndpointRef>(restored);
        Assert.Equal(new EntryPointId(2), v.Id);
    }

    [Fact]
    public void BufferStopEndpointRefは往復変換で値が保持される()
    {
        var options = MakeOptions();
        RailEndpointRef original = new BufferStopEndpointRef(new BufferStopId(3));

        var json = JsonSerializer.Serialize(original, options);
        var restored = JsonSerializer.Deserialize<RailEndpointRef>(json, options);

        var v = Assert.IsType<BufferStopEndpointRef>(restored);
        Assert.Equal(new BufferStopId(3), v.Id);
    }

    [Fact]
    public void SwitcherEndpointRefはIdとPortIndexの両方が往復変換で保持される()
    {
        var options = MakeOptions();
        RailEndpointRef original = new SwitcherEndpointRef(new SwitcherId(4), 2);

        var json = JsonSerializer.Serialize(original, options);
        var restored = JsonSerializer.Deserialize<RailEndpointRef>(json, options);

        var v = Assert.IsType<SwitcherEndpointRef>(restored);
        Assert.Equal(new SwitcherId(4), v.Id);
        Assert.Equal(2, v.PortIndex);
    }

    [Fact]
    public void Rail全体をシリアライズしても両端が正しく復元される()
    {
        var options = MakeOptions();
        var rail = new Rail
        {
            Id = new RailId(1),
            LengthM = 10,
            SpeedLimitKph = 25,
            Role = RailRole.Normal,
            EndpointA = new SwitcherEndpointRef(new SwitcherId(1), 0),
            EndpointB = new BufferStopEndpointRef(new BufferStopId(1)),
        };

        var json = JsonSerializer.Serialize(rail, options);
        var restored = JsonSerializer.Deserialize<Rail>(json, options);

        Assert.NotNull(restored);
        Assert.IsType<SwitcherEndpointRef>(restored!.EndpointA);
        Assert.IsType<BufferStopEndpointRef>(restored.EndpointB);
    }

    [Fact]
    public void kindフィールドが無ければJsonExceptionが送出される()
    {
        var options = MakeOptions();

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RailEndpointRef>("{}", options));
    }

    [Fact]
    public void SwitcherでportIndexが無ければJsonExceptionが送出される()
    {
        var options = MakeOptions();
        var json = """{"kind":"Switcher","switcherId":1}""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RailEndpointRef>(json, options));
    }
    
    [Fact]
    public void Noneでnoneendpointidが無ければJsonExceptionが送出される()
    {
        // 案A（既存プロジェクトファイルとの後方互換なし）の裏付けテスト。
        // 旧形式のJSON（kind:"None"のみ、noneEndpointIdフィールドが無い）を模したケース。
        var options = MakeOptions();
        var json = """{"kind":"None"}""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RailEndpointRef>(json, options));
    }
}

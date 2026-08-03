using System.Text.Json;

using DiaEditCore.Model;
using DiaEditCore.Serialization.Json;

using Xunit;

namespace DiaEditCore.Tests.Serialization.Json;

public class ObjectIdJsonConverterTests
{
    private static JsonSerializerOptions MakeOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new IntIdJsonConverterFactory());
        options.Converters.Add(new ObjectIdJsonConverter());
        return options;
    }

    public static IEnumerable<object[]> AllDerivedTypes()
    {
        yield return new object[] { new BoundaryPointObjectId(new BoundaryPointId(1)) };
        yield return new object[] { new EntryPointObjectId(new EntryPointId(2)) };
        yield return new object[] { new BufferStopObjectId(new BufferStopId(3)) };
        yield return new object[] { new SwitcherObjectId(new SwitcherId(4)) };
        yield return new object[] { new VirtualConflictObjectIdObject(new VirtualConflictObjectId(5)) };
        yield return new object[] { new RailObjectId(new RailId(6)) };
        yield return new object[] { new StationConnectionSegmentObjectId(new StationConnectionSegmentId(7)) };
    }

    [Theory]
    [MemberData(nameof(AllDerivedTypes))]
    public void 各派生型は往復変換で型_値ともに保持される(ObjectId original)
    {
        var options = MakeOptions();

        var json = JsonSerializer.Serialize(original, options);
        var restored = JsonSerializer.Deserialize<ObjectId>(json, options);

        Assert.Equal(original, restored);
        Assert.Equal(original.GetType(), restored!.GetType());
    }

    [Fact]
    public void kindフィールドが無ければJsonExceptionが送出される()
    {
        var options = MakeOptions();

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ObjectId>("{}", options));
    }

    [Fact]
    public void kindフィールドが未知の値ならJsonExceptionが送出される()
    {
        var options = MakeOptions();
        var json = """{"kind":"Unknown","id":1}""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ObjectId>(json, options));
    }

    [Fact]
    public void ObjectIdのリストをまとめてシリアライズしても種別が区別される()
    {
        var options = MakeOptions();
        var list = new List<ObjectId>
        {
            new RailObjectId(new RailId(1)),
            new SwitcherObjectId(new SwitcherId(1)),
        };

        var json = JsonSerializer.Serialize(list, options);
        var restored = JsonSerializer.Deserialize<List<ObjectId>>(json, options);

        Assert.NotNull(restored);
        Assert.IsType<RailObjectId>(restored![0]);
        Assert.IsType<SwitcherObjectId>(restored[1]);
    }
}

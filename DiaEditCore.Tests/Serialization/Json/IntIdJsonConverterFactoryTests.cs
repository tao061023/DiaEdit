using System.Text.Json;

using DiaEditCore.Model;
using DiaEditCore.Serialization.Json;

using Xunit;

namespace DiaEditCore.Tests.Serialization.Json;

public class IntIdJsonConverterFactoryTests
{
    private static JsonSerializerOptions MakeOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new IntIdJsonConverterFactory());
        return options;
    }

    [Fact]
    public void StationIdは素朴な数値としてシリアライズされる()
    {
        var options = MakeOptions();
        var id = new StationId(42);

        var json = JsonSerializer.Serialize(id, options);

        Assert.Equal("42", json);
    }

    [Fact]
    public void StationIdは素朴な数値からデシリアライズできる()
    {
        var options = MakeOptions();

        var id = JsonSerializer.Deserialize<StationId>("42", options);

        Assert.Equal(new StationId(42), id);
    }

    [Fact]
    public void 往復変換でStationIdの値が保持される()
    {
        var options = MakeOptions();
        var original = new StationId(123);

        var json = JsonSerializer.Serialize(original, options);
        var restored = JsonSerializer.Deserialize<StationId>(json, options);

        Assert.Equal(original, restored);
    }

    [Fact]
    public void 異なるID型でも同一Factoryで往復変換できる()
    {
        // IIntId実装型であれば型ごとにConverterを書かずとも汎用的に動作することを確認する
        var options = MakeOptions();

        var railId = JsonSerializer.Deserialize<RailId>(
            JsonSerializer.Serialize(new RailId(7), options), options);
        var trainId = JsonSerializer.Deserialize<TrainId>(
            JsonSerializer.Serialize(new TrainId(99), options), options);

        Assert.Equal(new RailId(7), railId);
        Assert.Equal(new TrainId(99), trainId);
    }

    [Fact]
    public void ID型を含むオブジェクト全体をシリアライズしてもネストしたオブジェクトにならない()
    {
        var options = MakeOptions();
        var rail = new { Id = new RailId(1), Name = "テストレール" };

        var json = JsonSerializer.Serialize(rail, options);

        Assert.Contains("\"Id\":1", json);
        Assert.DoesNotContain("\"value\"", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void 文字列など数値以外が指定されるとJsonExceptionが送出される()
    {
        var options = MakeOptions();

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<StationId>("\"not-a-number\"", options));
    }

    [Fact]
    public void IIntIdを実装しない型には適用されない()
    {
        var factory = new IntIdJsonConverterFactory();

        Assert.False(factory.CanConvert(typeof(int)));
        Assert.False(factory.CanConvert(typeof(string)));
        Assert.False(factory.CanConvert(typeof(Point))); // Common.cs: IIntId非実装のreadonly record struct
    }

    [Fact]
    public void IIntIdを実装する型には適用される()
    {
        var factory = new IntIdJsonConverterFactory();

        Assert.True(factory.CanConvert(typeof(StationId)));
        Assert.True(factory.CanConvert(typeof(RailId)));
    }
}

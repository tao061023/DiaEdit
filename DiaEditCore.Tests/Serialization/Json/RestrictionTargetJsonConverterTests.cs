using System.Text.Json;

using DiaEditCore.Model;
using DiaEditCore.Model.TimeTable;
using DiaEditCore.Serialization.Json;

using Xunit;

namespace DiaEditCore.Tests.Serialization.Json;

public class RestrictionTargetJsonConverterTests
{
    private static JsonSerializerOptions MakeOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new IntIdJsonConverterFactory()); // RestrictionTarget内部のID型変換に必要
        options.Converters.Add(new RestrictionTargetJsonConverter());
        return options;
    }

    [Fact]
    public void Segment派生型は往復変換で値が保持される()
    {
        var options = MakeOptions();
        RestrictionTarget original = new RestrictionTarget.Segment(new StationConnectionSegmentId(5));

        var json = JsonSerializer.Serialize(original, options);
        var restored = JsonSerializer.Deserialize<RestrictionTarget>(json, options);

        var segment = Assert.IsType<RestrictionTarget.Segment>(restored);
        Assert.Equal(new StationConnectionSegmentId(5), segment.StationConnectionSegmentId);
    }

    [Fact]
    public void Rail派生型は往復変換で値が保持される()
    {
        var options = MakeOptions();
        RestrictionTarget original = new RestrictionTarget.Rail(new RailId(9));

        var json = JsonSerializer.Serialize(original, options);
        var restored = JsonSerializer.Deserialize<RestrictionTarget>(json, options);

        var rail = Assert.IsType<RestrictionTarget.Rail>(restored);
        Assert.Equal(new RailId(9), rail.RailId);
    }

    [Fact]
    public void シリアライズ結果にkindフィールドが含まれる()
    {
        var options = MakeOptions();
        RestrictionTarget target = new RestrictionTarget.Segment(new StationConnectionSegmentId(1));

        var json = JsonSerializer.Serialize(target, options);

        Assert.Contains("\"kind\":\"Segment\"", json);
    }

    [Fact]
    public void kindフィールドが無ければJsonExceptionが送出される()
    {
        var options = MakeOptions();
        var json = """{"stationConnectionSegmentId":1}""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RestrictionTarget>(json, options));
    }

    [Fact]
    public void kindフィールドが未知の値ならJsonExceptionが送出される()
    {
        var options = MakeOptions();
        var json = """{"kind":"Unknown"}""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RestrictionTarget>(json, options));
    }

    [Fact]
    public void TemporaryRestriction全体をシリアライズしてもTargetがkindで判別可能な形になる()
    {
        var options = MakeOptions();
        var restriction = new TemporaryRestriction(
            new TemporaryRestrictionId(1),
            new RestrictionTarget.Rail(new RailId(3)),
            ExtraRunTimeSec: 30,
            SpeedLimitKph: null,
            DateRange: new DateRange(new DateTime(2026, 1, 1), new DateTime(2026, 1, 31)),
            Note: "テスト規制"
        );

        var json = JsonSerializer.Serialize(restriction, options);
        var restored = JsonSerializer.Deserialize<TemporaryRestriction>(json, options);

        Assert.NotNull(restored);
        var rail = Assert.IsType<RestrictionTarget.Rail>(restored!.Target);
        Assert.Equal(new RailId(3), rail.RailId);
        Assert.Equal(30, restored.ExtraRunTimeSec);
    }
}

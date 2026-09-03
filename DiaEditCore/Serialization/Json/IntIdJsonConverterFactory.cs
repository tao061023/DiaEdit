namespace DiaEditCore.Serialization.Json;

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using DiaEditCore.Model;

/// <summary>
/// IIntIdを実装するreadonly record struct（StationId等）を、
/// {"value": 5} のようなネストしたオブジェクトではなく素朴な数値 5 としてシリアライズ／デシリアライズする。
/// §8.2項目2（v11.36でクローズ）。
///
/// 対象型はIIntIdを実装し、かつ「int1つだけを受け取るコンストラクタ」を持つことを前提とする
/// （Ids.csの`readonly record struct XxxId(int Value) : IIntId`という宣言パターンに合致する型のみ対応）。
/// 対象外の型に対してCanConvertがfalseを返すことで、他の型のシリアライズには一切影響しない。
/// </summary>
public sealed class IntIdJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
        => typeof(IIntId).IsAssignableFrom(typeToConvert) && typeToConvert.IsValueType;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(IntIdJsonConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private sealed class IntIdJsonConverter<T> : JsonConverter<T>
        where T : struct, IIntId
    {
        // Ids.cs側の `record struct XxxId(int Value)` は必ずこの形のコンストラクタを1つだけ持つため、
        // 型ごとにConverterをキャッシュする際にコンストラクタ解決も一度だけ行う。
        private static readonly ConstructorInfo Ctor = ResolveConstructor();

        private static ConstructorInfo ResolveConstructor()
        {
            var ctor = typeof(T).GetConstructor(new[] { typeof(int) });
            if (ctor is null)
                throw new InvalidOperationException(
                    $"{typeof(T).Name}: IIntId実装型は int 一つを受け取るコンストラクタを持つ必要がある");
            return ctor;
        }

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.Number)
                throw new JsonException($"{typeToConvert.Name}: 数値であるべき箇所に{reader.TokenType}が指定された");

            var value = reader.GetInt32();
            return (T)Ctor.Invoke(new object[] { value });
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(value.Value);
        }
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Enums;

namespace API.Json;

public sealed class CardLanguageJsonConverter : JsonConverter<CardLanguage>
{
    public override CardLanguage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("CardLanguage must be a string.");
        }

        var value = reader.GetString();
        if (CardLanguageExtensions.TryParse(value, out var language))
        {
            return language;
        }

        throw new JsonException($"Invalid card language '{value}'.");
    }

    public override void Write(Utf8JsonWriter writer, CardLanguage value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToCode());
    }
}

public sealed class NullableCardLanguageJsonConverter : JsonConverter<CardLanguage?>
{
    public override CardLanguage? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("CardLanguage must be a string.");
        }

        var value = reader.GetString();
        if (CardLanguageExtensions.TryParse(value, out var language))
        {
            return language;
        }

        throw new JsonException($"Invalid card language '{value}'.");
    }

    public override void Write(Utf8JsonWriter writer, CardLanguage? value, JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value.ToCode());
    }
}

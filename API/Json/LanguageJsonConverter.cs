using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Enums;

namespace API.Json;

public sealed class LanguageJsonConverter : JsonConverter<Language>
{
    public override Language Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Language must be a string.");
        }

        var value = reader.GetString();
        if (LanguageExtensions.TryParse(value, out var language))
        {
            return language;
        }

        throw new JsonException($"Invalid language '{value}'.");
    }

    public override void Write(Utf8JsonWriter writer, Language value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToCode());
    }
}

public sealed class NullableLanguageJsonConverter : JsonConverter<Language?>
{
    public override Language? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Language must be a string.");
        }

        var value = reader.GetString();
        if (LanguageExtensions.TryParse(value, out var language))
        {
            return language;
        }

        throw new JsonException($"Invalid language '{value}'.");
    }

    public override void Write(Utf8JsonWriter writer, Language? value, JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value.ToCode());
    }
}

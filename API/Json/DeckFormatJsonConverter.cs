using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Enums;

namespace API.Json;

public sealed class DeckFormatJsonConverter : JsonConverter<DeckFormat>
{
    public override DeckFormat Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Format must be a string.");
        }

        var value = reader.GetString();
        if (DeckFormatExtensions.TryParse(value, out var format))
        {
            return format;
        }

        throw new JsonException($"Invalid format '{value}'.");
    }

    public override void Write(Utf8JsonWriter writer, DeckFormat value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToCode());
    }
}

public sealed class NullableDeckFormatJsonConverter : JsonConverter<DeckFormat?>
{
    public override DeckFormat? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Format must be a string.");
        }

        var value = reader.GetString();
        if (DeckFormatExtensions.TryParse(value, out var format))
        {
            return format;
        }

        throw new JsonException($"Invalid format '{value}'.");
    }

    public override void Write(Utf8JsonWriter writer, DeckFormat? value, JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value.ToCode());
    }
}

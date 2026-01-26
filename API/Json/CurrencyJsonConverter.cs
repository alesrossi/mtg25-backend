using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Enums;

namespace API.Json;

public sealed class CurrencyJsonConverter : JsonConverter<Currency>
{
    public override Currency Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Currency must be a string.");
        }

        var value = reader.GetString();
        if (CurrencyExtensions.TryParse(value, out var currency))
        {
            return currency;
        }

        throw new JsonException($"Invalid currency '{value}'.");
    }

    public override void Write(Utf8JsonWriter writer, Currency value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToCode());
    }
}

public sealed class NullableCurrencyJsonConverter : JsonConverter<Currency?>
{
    public override Currency? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Currency must be a string.");
        }

        var value = reader.GetString();
        if (CurrencyExtensions.TryParse(value, out var currency))
        {
            return currency;
        }

        throw new JsonException($"Invalid currency '{value}'.");
    }

    public override void Write(Utf8JsonWriter writer, Currency? value, JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value.ToCode());
    }
}

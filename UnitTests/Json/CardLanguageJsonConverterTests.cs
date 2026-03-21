using System.Text.Json;
using API.Extensions;
using API.Json;
using Core.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace UnitTests.Json;

/// <summary>
/// Tests for CardLanguageJsonConverter serialization/deserialization.
/// Covers both the converter directly and its registration in ConfigureHttpJsonOptions
/// (the serializer used by minimal API endpoints).
/// </summary>
public class CardLanguageJsonConverterTests
{
    private readonly JsonSerializerOptions _options;

    public CardLanguageJsonConverterTests()
    {
        _options = new JsonSerializerOptions();
        _options.Converters.Add(new CardLanguageJsonConverter());
        _options.Converters.Add(new NullableCardLanguageJsonConverter());
    }

    // ── Serialization ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(CardLanguage.En, "\"en\"")]
    [InlineData(CardLanguage.It, "\"it\"")]
    [InlineData(CardLanguage.Es, "\"es\"")]
    [InlineData(CardLanguage.Fr, "\"fr\"")]
    [InlineData(CardLanguage.De, "\"de\"")]
    [InlineData(CardLanguage.Pt, "\"pt\"")]
    [InlineData(CardLanguage.Ja, "\"ja\"")]
    [InlineData(CardLanguage.Ko, "\"ko\"")]
    [InlineData(CardLanguage.Ru, "\"ru\"")]
    [InlineData(CardLanguage.Zhs, "\"zhs\"")]
    [InlineData(CardLanguage.Zht, "\"zht\"")]
    [InlineData(CardLanguage.He, "\"he\"")]
    [InlineData(CardLanguage.La, "\"la\"")]
    [InlineData(CardLanguage.Grc, "\"grc\"")]
    [InlineData(CardLanguage.Ar, "\"ar\"")]
    [InlineData(CardLanguage.Sa, "\"sa\"")]
    [InlineData(CardLanguage.Ph, "\"ph\"")]
    [InlineData(CardLanguage.Qya, "\"qya\"")]
    public void Serialize_CardLanguage_WritesStringCode(CardLanguage language, string expectedJson)
    {
        var json = JsonSerializer.Serialize(language, _options);

        json.Should().Be(expectedJson);
    }

    [Fact]
    public void Serialize_NullableCardLanguage_WithValue_WritesStringCode()
    {
        CardLanguage? language = CardLanguage.Ja;

        var json = JsonSerializer.Serialize(language, _options);

        json.Should().Be("\"ja\"");
    }

    [Fact]
    public void Serialize_NullableCardLanguage_Null_WritesNull()
    {
        CardLanguage? language = null;

        var json = JsonSerializer.Serialize(language, _options);

        json.Should().Be("null");
    }

    // ── Deserialization by code ────────────────────────────────────────────────

    [Theory]
    [InlineData("\"en\"", CardLanguage.En)]
    [InlineData("\"it\"", CardLanguage.It)]
    [InlineData("\"es\"", CardLanguage.Es)]
    [InlineData("\"fr\"", CardLanguage.Fr)]
    [InlineData("\"de\"", CardLanguage.De)]
    [InlineData("\"pt\"", CardLanguage.Pt)]
    [InlineData("\"ja\"", CardLanguage.Ja)]
    [InlineData("\"ko\"", CardLanguage.Ko)]
    [InlineData("\"ru\"", CardLanguage.Ru)]
    [InlineData("\"zhs\"", CardLanguage.Zhs)]
    [InlineData("\"zht\"", CardLanguage.Zht)]
    [InlineData("\"he\"", CardLanguage.He)]
    [InlineData("\"la\"", CardLanguage.La)]
    [InlineData("\"grc\"", CardLanguage.Grc)]
    [InlineData("\"ar\"", CardLanguage.Ar)]
    [InlineData("\"sa\"", CardLanguage.Sa)]
    [InlineData("\"ph\"", CardLanguage.Ph)]
    [InlineData("\"qya\"", CardLanguage.Qya)]
    public void Deserialize_StringCode_ReturnsCorrectLanguage(string json, CardLanguage expected)
    {
        var result = JsonSerializer.Deserialize<CardLanguage>(json, _options);

        result.Should().Be(expected);
    }

    // ── Deserialization by printed/alternate codes ─────────────────────────────

    [Theory]
    [InlineData("\"sp\"", CardLanguage.Es)]   // Scryfall printed code for Spanish
    [InlineData("\"jp\"", CardLanguage.Ja)]   // Scryfall printed code for Japanese
    [InlineData("\"kr\"", CardLanguage.Ko)]   // Scryfall printed code for Korean
    [InlineData("\"cs\"", CardLanguage.Zhs)]  // Scryfall printed code for Simplified Chinese
    [InlineData("\"ct\"", CardLanguage.Zht)]  // Scryfall printed code for Traditional Chinese
    [InlineData("\"ag\"", CardLanguage.Grc)]  // Scryfall printed code for Ancient Greek
    public void Deserialize_AlternateCode_ReturnsCorrectLanguage(string json, CardLanguage expected)
    {
        var result = JsonSerializer.Deserialize<CardLanguage>(json, _options);

        result.Should().Be(expected);
    }

    // ── Deserialization by full language name ──────────────────────────────────

    [Theory]
    [InlineData("\"English\"", CardLanguage.En)]
    [InlineData("\"Italian\"", CardLanguage.It)]
    [InlineData("\"Spanish\"", CardLanguage.Es)]
    [InlineData("\"French\"", CardLanguage.Fr)]
    [InlineData("\"German\"", CardLanguage.De)]
    [InlineData("\"Portuguese\"", CardLanguage.Pt)]
    [InlineData("\"Japanese\"", CardLanguage.Ja)]
    [InlineData("\"Korean\"", CardLanguage.Ko)]
    [InlineData("\"Russian\"", CardLanguage.Ru)]
    [InlineData("\"Simplified Chinese\"", CardLanguage.Zhs)]
    [InlineData("\"Traditional Chinese\"", CardLanguage.Zht)]
    [InlineData("\"Hebrew\"", CardLanguage.He)]
    [InlineData("\"Latin\"", CardLanguage.La)]
    [InlineData("\"Ancient Greek\"", CardLanguage.Grc)]
    [InlineData("\"Arabic\"", CardLanguage.Ar)]
    [InlineData("\"Sanskrit\"", CardLanguage.Sa)]
    [InlineData("\"Phyrexian\"", CardLanguage.Ph)]
    [InlineData("\"Quenya\"", CardLanguage.Qya)]
    public void Deserialize_FullLanguageName_ReturnsCorrectLanguage(string json, CardLanguage expected)
    {
        var result = JsonSerializer.Deserialize<CardLanguage>(json, _options);

        result.Should().Be(expected);
    }

    [Fact]
    public void Deserialize_NullableCardLanguage_Null_ReturnsNull()
    {
        var result = JsonSerializer.Deserialize<CardLanguage?>("null", _options);

        result.Should().BeNull();
    }

    [Fact]
    public void Deserialize_NullableCardLanguage_WithValue_ReturnsLanguage()
    {
        var result = JsonSerializer.Deserialize<CardLanguage?>("\"ja\"", _options);

        result.Should().Be(CardLanguage.Ja);
    }

    [Fact]
    public void Deserialize_InvalidValue_ThrowsJsonException()
    {
        var act = () => JsonSerializer.Deserialize<CardLanguage>("\"xyz\"", _options);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Serialize_DoesNotReturnInteger()
    {
        // Regression: without the converter registered, CardLanguage serializes as an int.
        var json = JsonSerializer.Serialize(CardLanguage.En, _options);

        json.Should().NotBe("0");
        json.Should().Be("\"en\"");
    }

    // ── ConfigureHttpJsonOptions registration (minimal API serializer) ─────────

    [Fact]
    public void ConfigureHttpJsonOptions_IncludesCardLanguageConverter()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApiConfiguration(new ConfigurationBuilder().Build());

        var provider = services.BuildServiceProvider();
        var httpJsonOptions = provider.GetRequiredService<IOptions<JsonOptions>>().Value;

        var hasConverter = httpJsonOptions.SerializerOptions.Converters
            .Any(c => c is CardLanguageJsonConverter);

        hasConverter.Should().BeTrue("CardLanguageJsonConverter must be registered in ConfigureHttpJsonOptions for minimal API endpoints to serialize CardLanguage as a string code");
    }

    [Fact]
    public void ConfigureHttpJsonOptions_IncludesNullableCardLanguageConverter()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApiConfiguration(new ConfigurationBuilder().Build());

        var provider = services.BuildServiceProvider();
        var httpJsonOptions = provider.GetRequiredService<IOptions<JsonOptions>>().Value;

        var hasConverter = httpJsonOptions.SerializerOptions.Converters
            .Any(c => c is NullableCardLanguageJsonConverter);

        hasConverter.Should().BeTrue("NullableCardLanguageJsonConverter must be registered in ConfigureHttpJsonOptions");
    }

    [Fact]
    public void ConfigureHttpJsonOptions_SerializesCardLanguageAsStringCode()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApiConfiguration(new ConfigurationBuilder().Build());

        var provider = services.BuildServiceProvider();
        var serializerOptions = provider.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;

        var json = JsonSerializer.Serialize(CardLanguage.En, serializerOptions);

        json.Should().Be("\"en\"", "minimal API endpoints must serialize CardLanguage as its Scryfall string code, not as an integer");
    }
}

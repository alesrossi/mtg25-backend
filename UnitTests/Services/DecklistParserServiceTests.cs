using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Configuration;
using API.Dtos.Cards;
using API.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace UnitTests.Services;

public class DecklistParserServiceTests
{
    [Fact]
    public async Task ParseAsync_WithValidDecklist_ReturnsDeckCards()
    {
        // Arrange
        var cards = new[]
        {
            CreateOracleCardDto("1", "oracle-1", "Lightning Bolt", "LEA", "Limited Edition Alpha"),
            CreateOracleCardDto("2", "oracle-2", "Opt", "INV", "Invasion"),
            CreateOracleCardDto("3", "oracle-3", "Negate", "M10", "Magic 2010")
        };

        var parser = CreateParser(cards);

        var decklist = new[]
        {
            "4 Lightning Bolt",
            "2 Opt",
            string.Empty,
            "3 Negate"
        };

        // Act
        var result = await parser.ParseAsync(decklist);

        // Assert
        result.IsSuccessful.Should().BeTrue();
        result.DeckCards.Should().HaveCount(3);

        var lightningBolt = result.DeckCards.Single(dc => dc.Name == "Lightning Bolt");
        lightningBolt.MaindeckQuantity.Should().Be(4);
        lightningBolt.SideboardQuantity.Should().Be(0);

        var negate = result.DeckCards.Single(dc => dc.Name == "Negate");
        negate.MaindeckQuantity.Should().Be(0);
        negate.SideboardQuantity.Should().Be(3);
    }

    [Fact]
    public async Task ParseAsync_WithUnknownCard_ReportsError()
    {
        // Arrange
        var cards = new[]
        {
            CreateOracleCardDto("1", "oracle-1", "Lightning Bolt", "LEA", "Limited Edition Alpha")
        };

        var parser = CreateParser(cards);

        var decklist = new[]
        {
            "4 Lightning Bolt",
            "2 Totally Unknown"
        };

        // Act
        var result = await parser.ParseAsync(decklist);

        // Assert
        result.Errors.Should().Contain(error => error.Contains("Totally Unknown"));
        result.DeckCards.Should().HaveCount(1);
    }

    [Fact]
    public async Task ParseAsync_WithInvalidQuantity_ReportsError()
    {
        // Arrange
        var cards = new[]
        {
            CreateOracleCardDto("1", "oracle-1", "Lightning Bolt", "LEA", "Limited Edition Alpha")
        };

        var parser = CreateParser(cards);

        var decklist = new[]
        {
            "four Lightning Bolt"
        };

        // Act
        var result = await parser.ParseAsync(decklist);

        // Assert
        result.Errors.Should().Contain(error => error.Contains("Line 1"));
        result.DeckCards.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_WithQuantitySuffix_ReturnsDeckCards()
    {
        // Arrange
        var cards = new[]
        {
            CreateOracleCardDto("1", "oracle-1", "Lightning Bolt", "LEA", "Limited Edition Alpha")
        };

        var parser = CreateParser(cards);

        var decklist = new[]
        {
            "4x Lightning Bolt"
        };

        // Act
        var result = await parser.ParseAsync(decklist);

        // Assert
        result.IsSuccessful.Should().BeTrue();
        result.DeckCards.Should().HaveCount(1);
        result.DeckCards.Single().MaindeckQuantity.Should().Be(4);
    }

    [Fact]
    public async Task ParseAsync_WithTrailingSetCode_IgnoresSetSpecifier()
    {
        // Arrange
        var cards = new[]
        {
            CreateOracleCardDto("1", "oracle-1", "Lightning Bolt", "LEA", "Limited Edition Alpha")
        };

        var parser = CreateParser(cards);

        var decklist = new[]
        {
            "4 Lightning Bolt (LEA)"
        };

        // Act
        var result = await parser.ParseAsync(decklist);

        // Assert
        result.IsSuccessful.Should().BeTrue();
        result.DeckCards.Should().HaveCount(1);
        result.DeckCards.Single().MaindeckQuantity.Should().Be(4);
    }

    private static DecklistParserService CreateParser(IEnumerable<OracleCardDto> cards)
    {
        var cardDataService = new CardDataService(
            Options.Create(new PathsConfig()),
            Options.Create(new ScryfallConfig()));

        var byId = cards.ToDictionary(c => c.Id);
        var byName = cards.ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);

        typeof(CardDataService).GetProperty(nameof(CardDataService.CardDataById))!
            .SetValue(cardDataService, byId);
        typeof(CardDataService).GetProperty(nameof(CardDataService.CardDataByName))!
            .SetValue(cardDataService, byName);

        var validationService = new ValidationService();
        return new DecklistParserService(cardDataService, validationService);
    }

    private static OracleCardDto CreateOracleCardDto(string id, string oracleId, string name, string setCode, string setName)
    {
        var imageUrl = "https://example.com/card.png";

        return new OracleCardDto(
            Object: "card",
            Id: id,
            OracleId: oracleId,
            MultiverseIds: new List<int>(),
            MtgoId: null,
            TcgPlayerId: null,
            CardMarketId: null,
            Name: name,
            Lang: "en",
            ReleasedAt: DateTime.UtcNow,
            Uri: null,
            ScryfallUri: null,
            Layout: null,
            HighResImage: true,
            ImageStatus: null,
            ImageUris: new ImageUris(imageUrl, imageUrl, imageUrl, imageUrl, imageUrl, imageUrl),
            ManaCost: null,
            Cmc: 1,
            TypeLine: null,
            OracleText: null,
            Power: null,
            Toughness: null,
            Colors: new List<string?>(),
            ColorIdentity: new List<string?>(),
            Keywords: new List<string?>(),
            AllParts: new List<RelatedCard?>(),
            Legalities: new Legalities(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null),
            Games: new List<string?> { "paper" },
            Reserved: false,
            GameChanger: false,
            Foil: true,
            NonFoil: true,
            Finishes: new List<string?>(),
            Oversized: false,
            Promo: false,
            Reprint: false,
            Variation: false,
            SetId: Guid.NewGuid().ToString(),
            Set: setCode,
            SetName: setName,
            SetType: null,
            SetUri: null,
            SetSearchUri: null,
            ScryfallSetUri: null,
            RulingsUri: null,
            PrintsSearchUri: null,
            CollectorNumber: "1",
            Digital: false,
            Rarity: "Common",
            Watermark: null,
            FlavorText: null,
            CardBackId: null
        );
    }
}

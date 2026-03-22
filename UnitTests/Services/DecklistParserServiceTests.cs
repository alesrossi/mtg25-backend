using API.Dtos.Cards;
using API.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TestUtilities.Builders;
using TestUtilities.Scryfall;

namespace UnitTests.Services;

public class DecklistParserServiceTests
{
    private readonly TestDataBuilder _builder = new();

    [Fact]
    public async Task ParseAsync_WithValidDecklist_ReturnsDeckCards()
    {
        // Arrange
        var cards = new[]
        {
            _builder.CreateOracleCard("1", "oracle-1", "Lightning Bolt", "LEA", "Limited Edition Alpha"),
            _builder.CreateOracleCard("2", "oracle-2", "Opt", "INV", "Invasion"),
            _builder.CreateOracleCard("3", "oracle-3", "Negate", "M10", "Magic 2010")
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
            _builder.CreateOracleCard("1", "oracle-1", "Lightning Bolt", "LEA", "Limited Edition Alpha")
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
            _builder.CreateOracleCard("1", "oracle-1", "Lightning Bolt", "LEA", "Limited Edition Alpha")
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
            _builder.CreateOracleCard("1", "oracle-1", "Lightning Bolt", "LEA", "Limited Edition Alpha")
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
            _builder.CreateOracleCard("1", "oracle-1", "Lightning Bolt", "LEA", "Limited Edition Alpha")
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

    [Fact]
    public async Task ParseAsync_WithFlavorName_ReturnsCard()
    {
        // Arrange
        var cards = new[]
        {
            _builder.CreateOracleCard("1", "oracle-1", "Garruk Wildspeaker", "MED", "Magic: The Gathering—Conspiracy",
                flavorName: "Garruk, the Veil-Cursed")
        };

        var parser = CreateParser(cards);

        var decklist = new[] { "2 Garruk, the Veil-Cursed" };

        // Act
        var result = await parser.ParseAsync(decklist);

        // Assert
        result.IsSuccessful.Should().BeTrue();
        result.DeckCards.Should().HaveCount(1);
        result.DeckCards.Single().MaindeckQuantity.Should().Be(2);
    }

    [Fact]
    public async Task ParseAsync_WithPrintedName_ReturnsCard()
    {
        // Arrange
        var cards = new[]
        {
            _builder.CreateOracleCard("1", "oracle-1", "Lightning Bolt", "LEA", "Limited Edition Alpha",
                printedName: "Saetta Fulminante")
        };

        var parser = CreateParser(cards);

        var decklist = new[] { "4 Saetta Fulminante" };

        // Act
        var result = await parser.ParseAsync(decklist);

        // Assert
        result.IsSuccessful.Should().BeTrue();
        result.DeckCards.Should().HaveCount(1);
        result.DeckCards.Single().MaindeckQuantity.Should().Be(4);
    }

    [Fact]
    public async Task ParseAsync_FlavorNameAndRegularNameResolveSameCard()
    {
        // Arrange
        var cards = new[]
        {
            _builder.CreateOracleCard("1", "oracle-1", "Garruk Wildspeaker", "MED", "Magic: The Gathering—Conspiracy",
                flavorName: "Garruk, the Veil-Cursed")
        };

        var parser = CreateParser(cards);

        // Same card referenced by both names — should merge into one entry
        var decklist = new[]
        {
            "1 Garruk Wildspeaker",
            string.Empty,
            "1 Garruk, the Veil-Cursed"
        };

        // Act
        var result = await parser.ParseAsync(decklist);

        // Assert
        result.DeckCards.Should().HaveCount(1);
        result.DeckCards.Single().MaindeckQuantity.Should().Be(1);
        result.DeckCards.Single().SideboardQuantity.Should().Be(1);
    }

    private static DecklistParserService CreateParser(IEnumerable<ScryfallCardDto> cards)
    {
        var cardDataService = CardDataServiceTestHelper.CreateWithCards(cards);
        var validationService = new ValidationService();
        return new DecklistParserService(cardDataService, validationService, NullLogger<DecklistParserService>.Instance);
    }
}

using API.Services;
using Core.Interfaces;
using Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Core.Enums;

namespace UnitTests.Services;

public class DeckValidationServiceTests
{
    private readonly IDeckValidationService _validationService;

    public DeckValidationServiceTests()
    {
        _validationService = new DeckValidationService(NullLogger<DeckValidationService>.Instance);
    }

    [Theory]
    [InlineData(DeckFormat.Standard, 60)]
    [InlineData(DeckFormat.Modern, 60)]
    [InlineData(DeckFormat.Legacy, 60)]
    [InlineData(DeckFormat.Pioneer, 60)]
    [InlineData(DeckFormat.Commander, 100)]
    [InlineData(DeckFormat.Limited, 40)]
    public void GetMinimumCardCount_ReturnsCorrectMinimum(DeckFormat format, int expectedMinimum)
    {
        // Act
        var result = _validationService.GetMinimumCardCount(format);

        // Assert
        result.Should().Be(expectedMinimum);
    }

    [Theory]
    [InlineData(DeckFormat.Standard, null)]
    [InlineData(DeckFormat.Modern, null)]
    [InlineData(DeckFormat.Legacy, null)]
    [InlineData(DeckFormat.Pioneer, null)]
    [InlineData(DeckFormat.Commander, 100)]
    [InlineData(DeckFormat.Limited, null)]
    public void GetMaximumCardCount_ReturnsCorrectMaximum(DeckFormat format, int? expectedMaximum)
    {
        // Act
        var result = _validationService.GetMaximumCardCount(format);

        // Assert
        result.Should().Be(expectedMaximum);
    }

    [Theory]
    [InlineData(DeckFormat.Standard, true)]
    [InlineData(DeckFormat.Modern, true)]
    [InlineData(DeckFormat.Legacy, true)]
    [InlineData(DeckFormat.Pioneer, true)]
    [InlineData(DeckFormat.Commander, false)]
    [InlineData(DeckFormat.Limited, true)]
    public void FormatAllowsSideboard_ReturnsCorrectValue(DeckFormat format, bool allowsSideboard)
    {
        // Act
        var result = _validationService.FormatAllowsSideboard(format);

        // Assert
        result.Should().Be(allowsSideboard);
    }

    [Theory]
    [InlineData(DeckFormat.Standard, 15)]
    [InlineData(DeckFormat.Modern, 15)]
    [InlineData(DeckFormat.Legacy, 15)]
    [InlineData(DeckFormat.Pioneer, 15)]
    [InlineData(DeckFormat.Commander, 0)]
    [InlineData(DeckFormat.Limited, 0)]
    public void GetMaximumSideboardSize_ReturnsCorrectSize(DeckFormat format, int expectedSize)
    {
        // Act
        var result = _validationService.GetMaximumSideboardSize(format);

        // Assert  
        result.Should().Be(expectedSize);
    }

    [Fact]
    public async Task ValidateDeckAsync_UnknownFormat_ReturnsError()
    {
        // Arrange
        var deck = new Deck
        {
            Name = "Test Deck",
            Format = (DeckFormat)999,
            OwnerId = "user1",
            NumberOfCards = 60,
            DeckList = string.Empty,
            IsPublic = true
        };

        // Act
        var result = await _validationService.ValidateDeckAsync(deck);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Message.Should().Contain("Unknown format: 999");
        result.Errors[0].Type.Should().Be(ValidationErrorType.Format);
    }

    [Fact]
    public async Task ValidateDeckAsync_StandardDeckTooFewCards_ReturnsError()
    {
        // Arrange
        var deck = new Deck
        {
            Name = "Test Deck",
            Format = DeckFormat.Standard,
            OwnerId = "user1",
            NumberOfCards = 45,
            DeckList = string.Empty,
            IsPublic = true
        };

        // Act
        var result = await _validationService.ValidateDeckAsync(deck);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Message.Should().Contain("Deck must contain at least 60 cards");
        result.Errors[0].Type.Should().Be(ValidationErrorType.CardCount);
    }

    [Fact]
    public async Task ValidateDeckAsync_CommanderDeckTooManyCards_ReturnsError()
    {
        // Arrange
        var deck = new Deck
        {
            Name = "Test Deck",
            Format = DeckFormat.Commander,
            OwnerId = "user1",
            NumberOfCards = 105,
            DeckList = string.Empty,
            IsPublic = true
        };

        // Act
        var result = await _validationService.ValidateDeckAsync(deck);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Message.Should().Contain("Deck cannot contain more than 100 cards");
        result.Errors[0].Type.Should().Be(ValidationErrorType.CardCount);
    }

    [Fact]
    public async Task ValidateDeckAsync_ValidStandardDeck_ReturnsValid()
    {
        // Arrange
        var deck = new Deck
        {
            Name = "Test Deck",
            Format = DeckFormat.Standard,
            OwnerId = "user1",
            NumberOfCards = 60,
            DeckList = string.Empty,
            IsPublic = true
        };

        // Act
        var result = await _validationService.ValidateDeckAsync(deck);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateDeckWithCardsAsync_EmptyDeck_ReturnsError()
    {
        // Arrange
        var deck = new Deck
        {
            Name = "Test Deck",
            Format = DeckFormat.Standard,
            OwnerId = "user1",
            NumberOfCards = 0,
            DeckList = string.Empty,
            IsPublic = true
        };
        // ReSharper disable once CollectionNeverUpdated.Local
        var deckCards = new List<DeckCard>();

        // Act
        var result = await _validationService.ValidateDeckWithCardsAsync(deck, deckCards);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("Deck cannot be empty"));
    }

    [Fact]
    public async Task ValidateDeckWithCardsAsync_StandardTooManyCardsOfSameType_ReturnsError()
    {
        // Arrange
        var deck = new Deck
        {
            Name = "Test Deck",
            Format = DeckFormat.Standard,
            OwnerId = "user1",
            NumberOfCards = 65,
            DeckList = string.Empty,
            IsPublic = true
        };

        var deckCards = new List<DeckCard>
        {
            new()
            {
                ScryfallId = "oracle1",
                OracleId = "oracle1",
                Name = "Lightning Bolt",
                SetCode = "LEA",
                DeckId = 1,
                MaindeckQuantity = 5, // Too many copies
                SideboardQuantity = 0,
                TypeLine = "Instant"
            }
        };

        // Act
        var result = await _validationService.ValidateDeckWithCardsAsync(deck, deckCards);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => 
            e.Message.Contains("Lightning Bolt") && 
            e.Message.Contains("appears 5 times") &&
            e.Type == ValidationErrorType.CardLegality);
    }

    [Fact]
    public async Task ValidateDeckWithCardsAsync_CommanderDuplicateCard_ReturnsError()
    {
        // Arrange
        var deck = new Deck
        {
            Name = "Commander Deck",
            Format = DeckFormat.Commander,
            OwnerId = "user1",
            NumberOfCards = 100,
            DeckList = string.Empty,
            IsPublic = true
        };

        var deckCards = new List<DeckCard>
        {
            new()
            {
                ScryfallId = "oracle1",
                OracleId = "oracle1",
                Name = "Lightning Bolt",
                SetCode = "LEA",
                DeckId = 1,
                MaindeckQuantity = 2, // Not allowed in singleton format
                SideboardQuantity = 0,
                TypeLine = "Instant"
            }
        };

        // Act
        var result = await _validationService.ValidateDeckWithCardsAsync(deck, deckCards);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => 
            e.Message.Contains("Lightning Bolt") && 
            e.Message.Contains("appears 2 times") &&
            e.Message.Contains("allows only 1 copy") &&
            e.Type == ValidationErrorType.Singleton);
    }

    [Fact]
    public async Task ValidateDeckWithCardsAsync_CommanderSideboardCards_ReturnsError()
    {
        // Arrange
        var deck = new Deck
        {
            Name = "Commander Deck",
            Format = DeckFormat.Commander,
            OwnerId = "user1",
            NumberOfCards = 100,
            DeckList = string.Empty,
            IsPublic = true
        };

        var deckCards = new List<DeckCard>
        {
            new()
            {
                ScryfallId = "oracle1",
                OracleId = "oracle1",
                Name = "Lightning Bolt",
                SetCode = "LEA",
                DeckId = 1,
                MaindeckQuantity = 0,
                SideboardQuantity = 1, // Commander doesn't allow sideboards
                TypeLine = "Instant"
            }
        };

        // Act
        var result = await _validationService.ValidateDeckWithCardsAsync(deck, deckCards);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => 
            e.Message.Contains("This format does not allow sideboards") &&
            e.Type == ValidationErrorType.Sideboard);
    }

    [Fact]
    public async Task ValidateDeckWithCardsAsync_StandardTooLargeSideboard_ReturnsError()
    {
        // Arrange
        var deck = new Deck
        {
            Name = "Standard Deck",
            Format = DeckFormat.Standard,
            OwnerId = "user1",
            NumberOfCards = 75,
            DeckList = string.Empty,
            IsPublic = true
        };

        var deckCards = new List<DeckCard>
        {
            new()
            {
                ScryfallId = "oracle1",
                OracleId = "oracle1",
                Name = "Sideboard Card",
                SetCode = "LEA",
                DeckId = 1,
                MaindeckQuantity = 0,
                SideboardQuantity = 16, // Too many sideboard cards
                TypeLine = "Instant"
            }
        };

        // Act
        var result = await _validationService.ValidateDeckWithCardsAsync(deck, deckCards);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => 
            e.Message.Contains("Sideboard cannot contain more than 15 cards") &&
            e.Type == ValidationErrorType.Sideboard);
    }

    [Fact]
    public async Task ValidateDeckWithCardsAsync_ValidStandardDeck_ReturnsValid()
    {
        // Arrange
        var deck = new Deck
        {
            Name = "Standard Deck",
            Format = DeckFormat.Standard,
            OwnerId = "user1",
            NumberOfCards = 62,// Total: 4 + 4 + 52 + 2 = 62 cards
            DeckList = string.Empty,
            IsPublic = true
        };

        var deckCards = new List<DeckCard>
        {
            new()
            {
                ScryfallId = "oracle1",
                OracleId = "oracle1",
                Name = "Lightning Bolt",
                SetCode = "LEA",
                DeckId = 1,
                MaindeckQuantity = 4,
                SideboardQuantity = 0,
                TypeLine = "Instant"
            },
            new()
            {
                ScryfallId = "oracle2",
                OracleId = "oracle2",
                Name = "Counterspell",
                SetCode = "LEB",
                DeckId = 1,
                MaindeckQuantity = 4,
                SideboardQuantity = 2,
                TypeLine = "Instant"
            },
            new()
            {
                ScryfallId = "oracle3",
                OracleId = "oracle3",
                Name = "Forest",
                SetCode = "LEA",
                DeckId = 1,
                MaindeckQuantity = 52, // Basic lands can exceed 4 copies
                SideboardQuantity = 0,
                TypeLine = "Basic Land — Forest"
            }
        };

        // Act
        var result = await _validationService.ValidateDeckWithCardsAsync(deck, deckCards);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateDeckWithCardsAsync_DraftFormat_AllowsUnlimitedCopies()
    {
        // Arrange
        var deck = new Deck
        {
            Name = "Draft Deck",
            Format = DeckFormat.Limited,
            OwnerId = "user1",
            NumberOfCards = 40,
            DeckList = string.Empty,
            IsPublic = true
        };

        var deckCards = new List<DeckCard>
        {
            new()
            {
                ScryfallId = "oracle1",
                OracleId = "oracle1",
                Name = "Lightning Bolt",
                SetCode = "LEA",
                DeckId = 1,
                MaindeckQuantity = 40, // Should be allowed in draft
                SideboardQuantity = 0,
                TypeLine = "Instant"
            }
        };

        // Act
        var result = await _validationService.ValidateDeckWithCardsAsync(deck, deckCards);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }


    [Theory]
    [InlineData((DeckFormat)999, 60)]
    public void GetMinimumCardCount_UnknownFormat_ReturnsDefault(DeckFormat format, int expectedDefault)
    {
        // Act
        var result = _validationService.GetMinimumCardCount(format);

        // Assert
        result.Should().Be(expectedDefault);
    }

    [Fact]
    public async Task ValidateDeckWithCardsAsync_BasicLandsAllowUnlimitedCopies_ReturnsValid()
    {
        // Arrange
        var deck = new Deck
        {
            Name = "Standard Deck",
            Format = DeckFormat.Standard,
            OwnerId = "user1",
            NumberOfCards = 60,
            DeckList = string.Empty,
            IsPublic = true
        };

        var deckCards = new List<DeckCard>
        {
            new()
            {
                ScryfallId = "oracle1",
                OracleId = "oracle1",
                Name = "Forest",
                SetCode = "LEA",
                DeckId = 1,
                MaindeckQuantity = 20, // More than 4 basic lands should be allowed
                SideboardQuantity = 0,
                TypeLine = "Basic Land — Forest"
            },
            new()
            {
                ScryfallId = "oracle2",
                OracleId = "oracle2",
                Name = "Island",
                SetCode = "LEA",
                DeckId = 1,
                MaindeckQuantity = 20,
                SideboardQuantity = 0,
                TypeLine = "Basic Land — Island"
            },
            new()
            {
                ScryfallId = "oracle3",
                OracleId = "oracle3",
                Name = "Mountain",
                SetCode = "LEA",
                DeckId = 1,
                MaindeckQuantity = 20,
                SideboardQuantity = 0,
                TypeLine = "Basic Land — Mountain"
            }
        };

        // Act
        var result = await _validationService.ValidateDeckWithCardsAsync(deck, deckCards);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}

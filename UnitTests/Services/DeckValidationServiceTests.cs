using API.Services;
using Core.Interfaces;
using Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTests.Services;

public class DeckValidationServiceTests
{
    private readonly IDeckValidationService _validationService;

    public DeckValidationServiceTests()
    {
        _validationService = new DeckValidationService(NullLogger<DeckValidationService>.Instance);
    }

    [Theory]
    [InlineData("Standard", 60)]
    [InlineData("Modern", 60)]
    [InlineData("Legacy", 60)]
    [InlineData("Pioneer", 60)]
    [InlineData("Commander", 100)]
    [InlineData("Draft", 40)]
    [InlineData("Sealed", 40)]
    public void GetMinimumCardCount_ReturnsCorrectMinimum(string format, int expectedMinimum)
    {
        // Act
        var result = _validationService.GetMinimumCardCount(format);

        // Assert
        result.Should().Be(expectedMinimum);
    }

    [Theory]
    [InlineData("Standard", null)]
    [InlineData("Modern", null)]
    [InlineData("Legacy", null)]
    [InlineData("Pioneer", null)]
    [InlineData("Commander", 100)]
    [InlineData("Draft", null)]
    [InlineData("Sealed", null)]
    public void GetMaximumCardCount_ReturnsCorrectMaximum(string format, int? expectedMaximum)
    {
        // Act
        var result = _validationService.GetMaximumCardCount(format);

        // Assert
        result.Should().Be(expectedMaximum);
    }

    [Theory]
    [InlineData("Standard", true)]
    [InlineData("Modern", true)]
    [InlineData("Legacy", true)]
    [InlineData("Pioneer", true)]
    [InlineData("Commander", false)]
    [InlineData("Draft", true)]
    [InlineData("Sealed", true)]
    public void FormatAllowsSideboard_ReturnsCorrectValue(string format, bool allowsSideboard)
    {
        // Act
        var result = _validationService.FormatAllowsSideboard(format);

        // Assert
        result.Should().Be(allowsSideboard);
    }

    [Theory]
    [InlineData("Standard", 15)]
    [InlineData("Modern", 15)]
    [InlineData("Legacy", 15)]
    [InlineData("Pioneer", 15)]
    [InlineData("Commander", 0)]
    [InlineData("Draft", 0)] // No limit, but returns 0
    [InlineData("Sealed", 0)] // No limit, but returns 0
    public void GetMaximumSideboardSize_ReturnsCorrectSize(string format, int expectedSize)
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
            Format = "UnknownFormat",
            OwnerId = "user1",
            NumberOfCards = 60
        };

        // Act
        var result = await _validationService.ValidateDeckAsync(deck);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Message.Should().Contain("Unknown format: UnknownFormat");
        result.Errors[0].Type.Should().Be(ValidationErrorType.Format);
    }

    [Fact]
    public async Task ValidateDeckAsync_StandardDeckTooFewCards_ReturnsError()
    {
        // Arrange
        var deck = new Deck
        {
            Name = "Test Deck",
            Format = "Standard",
            OwnerId = "user1",
            NumberOfCards = 45
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
            Format = "Commander",
            OwnerId = "user1",
            NumberOfCards = 105
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
            Format = "Standard",
            OwnerId = "user1",
            NumberOfCards = 60
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
            Format = "Standard",
            OwnerId = "user1",
            NumberOfCards = 0
        };
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
            Format = "Standard",
            OwnerId = "user1",
            NumberOfCards = 65
        };

        var deckCards = new List<DeckCard>
        {
            new()
            {
                ScryfallId = "oracle1",
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
            Format = "Commander",
            OwnerId = "user1",
            NumberOfCards = 100
        };

        var deckCards = new List<DeckCard>
        {
            new()
            {
                ScryfallId = "oracle1",
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
            Format = "Commander",
            OwnerId = "user1",
            NumberOfCards = 100
        };

        var deckCards = new List<DeckCard>
        {
            new()
            {
                ScryfallId = "oracle1",
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
            Format = "Standard",
            OwnerId = "user1",
            NumberOfCards = 75
        };

        var deckCards = new List<DeckCard>
        {
            new()
            {
                ScryfallId = "oracle1",
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
            Format = "Standard",
            OwnerId = "user1",
            NumberOfCards = 62 // Total: 4 + 4 + 52 + 2 = 62 cards
        };

        var deckCards = new List<DeckCard>
        {
            new()
            {
                ScryfallId = "oracle1",
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
            Format = "Draft",
            OwnerId = "user1",
            NumberOfCards = 40
        };

        var deckCards = new List<DeckCard>
        {
            new()
            {
                ScryfallId = "oracle1",
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
    [InlineData("UnknownFormat", 60)]
    public void GetMinimumCardCount_UnknownFormat_ReturnsDefault(string format, int expectedDefault)
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
            Format = "Standard",
            OwnerId = "user1",
            NumberOfCards = 60
        };

        var deckCards = new List<DeckCard>
        {
            new()
            {
                ScryfallId = "oracle1",
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

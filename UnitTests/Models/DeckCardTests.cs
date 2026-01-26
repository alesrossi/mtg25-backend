using Core.Models;
using FluentAssertions;
using Core.Enums;

namespace UnitTests.Models;

public class DeckCardTests
{
    [Fact]
    public void DeckCard_WhenCreated_ShouldHaveRequiredProperties()
    {
        // Arrange & Act
        var deckCard = new DeckCard
        {
            DeckId = 1,
            ScryfallId = "12345678-1234-1234-1234-123456789012",
            Name = "Lightning Bolt",
            SetCode = "LEA",
            TypeLine = "Instant",
            MaindeckQuantity = 4,
            SideboardQuantity = 0
        };

        // Assert
        deckCard.DeckId.Should().Be(1);
        deckCard.ScryfallId.Should().Be("12345678-1234-1234-1234-123456789012");
        deckCard.Name.Should().Be("Lightning Bolt");
        deckCard.SetCode.Should().Be("LEA");
        deckCard.MaindeckQuantity.Should().Be(4);
        deckCard.SideboardQuantity.Should().Be(0);
    }

    [Fact]
    public void TotalQuantity_WhenMaindeckAndSideboardSet_ShouldReturnSum()
    {
        // Arrange
        var deckCard = new DeckCard
        {
            DeckId = 1,
            ScryfallId = "12345678-1234-1234-1234-123456789012",
            Name = "Lightning Bolt",
            SetCode = "LEA",
            TypeLine = "Instant",
            MaindeckQuantity = 3,
            SideboardQuantity = 2
        };

        // Act & Assert
        deckCard.TotalQuantity.Should().Be(5, "because maindeck (3) + sideboard (2) = 5");
    }

    [Fact]
    public void DeckCard_WithOptionalProperties_ShouldAllowNullValues()
    {
        // Arrange & Act
        var deckCard = new DeckCard
        {
            DeckId = 1,
            ScryfallId = "12345678-1234-1234-1234-123456789012",
            Name = "Lightning Bolt",
            SetCode = "LEA",
            TypeLine = "Instant",
            MaindeckQuantity = 4,
            SideboardQuantity = 0,
            SetName = null,
            ImageUrl = null,
            Rarity = null,
            CollectorNumber = null,
            OwnedCardId = null
        };

        // Assert
        deckCard.SetName.Should().BeNull();
        deckCard.ImageUrl.Should().BeNull();
        deckCard.Rarity.Should().BeNull();
        deckCard.CollectorNumber.Should().BeNull();
        deckCard.OwnedCardId.Should().BeNull();
        deckCard.OwnedCard.Should().BeNull();
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(4, 0, 4)]
    [InlineData(0, 3, 3)]
    [InlineData(2, 1, 3)]
    public void TotalQuantity_WithDifferentQuantities_ShouldCalculateCorrectly(
        int maindeckQuantity, int sideboardQuantity, int expectedTotal)
    {
        // Arrange
        var deckCard = new DeckCard
        {
            DeckId = 1,
            ScryfallId = "12345678-1234-1234-1234-123456789012",
            Name = "Test Card",
            SetCode = "TST",
            TypeLine = "Instant",
            MaindeckQuantity = maindeckQuantity,
            SideboardQuantity = sideboardQuantity
        };

        // Act & Assert
        deckCard.TotalQuantity.Should().Be(expectedTotal);
    }

    [Fact]
    public void DeckCard_WithOwnedCard_ShouldLinkCorrectly()
    {
        // Arrange
        var ownedCard = new Card
        {
            Id = 1,
            CollectionId = 1,
            ScryfallId = "12345678-1234-1234-1234-123456789012",
            Name = "Lightning Bolt",
            SetCode = "LEA",
            TypeLine = "Instant",
            Quantity = 1,
            PurchasePrice = 2.50,
            Language = Language.En,
            Condition = Condition.NearMint,
            IsFoil = false,
            PurchasePriceCurrency = Currency.Usd,
            ImageUrl = "https://example.com/card.jpg",
            BackImageUrl = null,
            SetName = "Alpha",
            CollectorNumber = "161",
            Rarity = "common",
            IsMisprint = false,
            IsAltered = false,
            ArtCrop = "https://example.com/card.jpg"
        };

        var deckCard = new DeckCard
        {
            DeckId = 1,
            ScryfallId = "12345678-1234-1234-1234-123456789012",
            Name = "Lightning Bolt",
            SetCode = "LEA",
            TypeLine = "Instant",
            MaindeckQuantity = 4,
            SideboardQuantity = 0,
            OwnedCardId = 1,
            OwnedCard = ownedCard
        };

        // Act & Assert
        deckCard.OwnedCardId.Should().Be(1);
        deckCard.OwnedCard.Should().NotBeNull();
        deckCard.OwnedCard!.Name.Should().Be("Lightning Bolt");
    }
}

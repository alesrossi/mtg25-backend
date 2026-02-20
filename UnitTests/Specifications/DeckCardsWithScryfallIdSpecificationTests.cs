using FluentAssertions;
using Core.Models;
using Core.Specifications;
using TestUtilities.Builders;
using Core.Enums;

namespace UnitTests.Specifications;

public class DeckCardsWithScryfallIdSpecificationTests
{
    public DeckCardsWithScryfallIdSpecificationTests()
    {
        var unused = new TestDataBuilder();
    }

    [Fact]
    public void DeckCardsWithOracleIdSpecification_WithOracleId_FiltersByOracleId()
    {
        // Arrange
        const string oracleId = "oracle-123";
        var spec = new DeckCardsWithScryfallIdSpecification(oracleId);

        var deckCard1 = new DeckCard
        {
            DeckId = 1,
            ScryfallId = oracleId,
            OracleId = oracleId,
            Name = "Lightning Bolt",
            SetCode = "LEA",
            TypeLine = "Instant",
            MaindeckQuantity = 4,
            SideboardQuantity = 0
        };

        var deckCard2 = new DeckCard
        {
            DeckId = 2,
            ScryfallId = "oracle-456",
            OracleId = "oracle-456",
            Name = "Counterspell",
            SetCode = "ICE",
            TypeLine = "Instant",
            MaindeckQuantity = 2,
            SideboardQuantity = 0
        };

        var deckCards = new List<DeckCard> { deckCard1, deckCard2 };

        // Act
        var filtered = deckCards.Where(spec.Criteria.Compile()).ToList();

        // Assert
        filtered.Should().HaveCount(1);
        filtered.First().ScryfallId.Should().Be(oracleId);
        filtered.First().Name.Should().Be("Lightning Bolt");
    }

    [Fact]
    public void DeckCardsWithOracleIdSpecification_WithOracleIdAndDeckId_FiltersByBoth()
    {
        // Arrange
        const string oracleId = "oracle-123";
        const int deckId = 1;
        var spec = new DeckCardsWithScryfallIdSpecification(oracleId, deckId);

        var matchingCard = new DeckCard
        {
            DeckId = deckId,
            ScryfallId = oracleId,
            OracleId = oracleId,
            Name = "Lightning Bolt",
            SetCode = "LEA",
            TypeLine = "Instant",
            MaindeckQuantity = 4,
            SideboardQuantity = 0
        };

        var wrongDeckCard = new DeckCard
        {
            DeckId = 2,
            ScryfallId = oracleId,
            OracleId = oracleId,
            Name = "Lightning Bolt",
            SetCode = "LEA",
            TypeLine = "Instant",
            MaindeckQuantity = 3,
            SideboardQuantity = 0
        };

        var wrongOracleCard = new DeckCard
        {
            DeckId = deckId,
            ScryfallId = "oracle-456",
            OracleId = "oracle-456",
            Name = "Counterspell",
            SetCode = "ICE",
            TypeLine = "Instant",
            MaindeckQuantity = 2,
            SideboardQuantity = 0
        };

        var deckCards = new List<DeckCard> { matchingCard, wrongDeckCard, wrongOracleCard };

        // Act
        var filtered = deckCards.Where(spec.Criteria.Compile()).ToList();

        // Assert
        filtered.Should().HaveCount(1);
        filtered.First().DeckId.Should().Be(deckId);
        filtered.First().ScryfallId.Should().Be(oracleId);
        filtered.First().Name.Should().Be("Lightning Bolt");
    }

    [Fact]
    public void DeckCardsWithOracleIdSpecification_WithOracleIdAndUserId_FiltersByUserOwnedDecks()
    {
        // Arrange
        const string oracleId = "oracle-123";
        const string userId = "user-123";
        var spec = new DeckCardsWithScryfallIdSpecification(oracleId, userId);

        var userDeckCard = new DeckCard
        {
            DeckId = 1,
            ScryfallId = oracleId,
            OracleId = oracleId,
            Name = "Lightning Bolt",
            SetCode = "LEA",
            TypeLine = "Instant",
            MaindeckQuantity = 4,
            SideboardQuantity = 0,
            Deck = new Deck { OwnerId = userId, Name = "User Deck", Format = DeckFormat.Standard, DeckList = string.Empty }
        };

        var otherUserDeckCard = new DeckCard
        {
            DeckId = 2,
            ScryfallId = oracleId,
            OracleId = oracleId,
            Name = "Lightning Bolt",
            SetCode = "LEA",
            TypeLine = "Instant",
            MaindeckQuantity = 3,
            SideboardQuantity = 0,
            Deck = new Deck { OwnerId = "other-user", Name = "Other Deck", Format = DeckFormat.Modern, DeckList = string.Empty }
        };

        var deckCards = new List<DeckCard> { userDeckCard, otherUserDeckCard };

        // Act
        var filtered = deckCards.Where(spec.Criteria.Compile()).ToList();

        // Assert
        filtered.Should().HaveCount(1);
        filtered.First().Deck.OwnerId.Should().Be(userId);
        filtered.First().Name.Should().Be("Lightning Bolt");
    }

    [Fact]
    public void DeckCardsWithOracleIdSpecification_IncludesDeckAndOwnedCard()
    {
        // Arrange
        const string oracleId = "oracle-123";
        var spec = new DeckCardsWithScryfallIdSpecification(oracleId);

        // Assert
        spec.Includes.Should().HaveCount(2);
        spec.Includes.Should().Contain(include => include.Body.ToString().Contains("Deck"));
        spec.Includes.Should().Contain(include => include.Body.ToString().Contains("OwnedCard"));
    }

    [Fact]
    public void DeckCardsWithOracleIdSpecification_OrdersByName()
    {
        // Arrange
        const string oracleId = "oracle-123";
        var spec = new DeckCardsWithScryfallIdSpecification(oracleId);

        // Assert
        spec.OrderBy.Should().NotBeNull();
        spec.OrderBy.Body.ToString().Should().Contain("Name");
    }

    [Fact]
    public void DeckCardsWithOracleIdSpecification_AllConstructors_IncludeSameNavigationProperties()
    {
        // Arrange
        const string oracleId = "oracle-123";
        const int deckId = 1;
        const string userId = "user-123";

        var spec1 = new DeckCardsWithScryfallIdSpecification(oracleId);
        var spec2 = new DeckCardsWithScryfallIdSpecification(oracleId, deckId);
        var spec3 = new DeckCardsWithScryfallIdSpecification(oracleId, userId);

        // Assert
        spec1.Includes.Should().HaveCount(2);
        spec2.Includes.Should().HaveCount(2);
        spec3.Includes.Should().HaveCount(2);

        foreach (var spec in new[] { spec1, spec2, spec3 })
        {
            spec.Includes.Should().Contain(include => include.Body.ToString().Contains("Deck"));
            spec.Includes.Should().Contain(include => include.Body.ToString().Contains("OwnedCard"));
            spec.OrderBy.Should().NotBeNull();
            spec.OrderBy.Body.ToString().Should().Contain("Name");
        }
    }
}

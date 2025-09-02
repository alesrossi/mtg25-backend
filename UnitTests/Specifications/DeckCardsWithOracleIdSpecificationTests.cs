using FluentAssertions;
using Core.Models;
using Core.Specifications;
using TestUtilities.Builders;

namespace UnitTests.Specifications;

public class DeckCardsWithOracleIdSpecificationTests
{
    private readonly TestDataBuilder _testDataBuilder;

    public DeckCardsWithOracleIdSpecificationTests()
    {
        _testDataBuilder = new TestDataBuilder();
    }

    [Fact]
    public void DeckCardsWithOracleIdSpecification_WithOracleId_FiltersByOracleId()
    {
        // Arrange
        var oracleId = "oracle-123";
        var spec = new DeckCardsWithOracleIdSpecification(oracleId);

        var deckCard1 = new DeckCard
        {
            DeckId = 1,
            OracleId = oracleId,
            Name = "Lightning Bolt",
            SetCode = "LEA",
            MaindeckQuantity = 4,
            SideboardQuantity = 0
        };

        var deckCard2 = new DeckCard
        {
            DeckId = 2,
            OracleId = "oracle-456",
            Name = "Counterspell",
            SetCode = "ICE",
            MaindeckQuantity = 2,
            SideboardQuantity = 0
        };

        var deckCards = new List<DeckCard> { deckCard1, deckCard2 };

        // Act
        var filtered = deckCards.Where(spec.Criteria.Compile()).ToList();

        // Assert
        filtered.Should().HaveCount(1);
        filtered.First().OracleId.Should().Be(oracleId);
        filtered.First().Name.Should().Be("Lightning Bolt");
    }

    [Fact]
    public void DeckCardsWithOracleIdSpecification_WithOracleIdAndDeckId_FiltersByBoth()
    {
        // Arrange
        var oracleId = "oracle-123";
        var deckId = 1;
        var spec = new DeckCardsWithOracleIdSpecification(oracleId, deckId);

        var matchingCard = new DeckCard
        {
            DeckId = deckId,
            OracleId = oracleId,
            Name = "Lightning Bolt",
            SetCode = "LEA",
            MaindeckQuantity = 4,
            SideboardQuantity = 0
        };

        var wrongDeckCard = new DeckCard
        {
            DeckId = 2,
            OracleId = oracleId,
            Name = "Lightning Bolt",
            SetCode = "LEA",
            MaindeckQuantity = 3,
            SideboardQuantity = 0
        };

        var wrongOracleCard = new DeckCard
        {
            DeckId = deckId,
            OracleId = "oracle-456",
            Name = "Counterspell",
            SetCode = "ICE",
            MaindeckQuantity = 2,
            SideboardQuantity = 0
        };

        var deckCards = new List<DeckCard> { matchingCard, wrongDeckCard, wrongOracleCard };

        // Act
        var filtered = deckCards.Where(spec.Criteria.Compile()).ToList();

        // Assert
        filtered.Should().HaveCount(1);
        filtered.First().DeckId.Should().Be(deckId);
        filtered.First().OracleId.Should().Be(oracleId);
        filtered.First().Name.Should().Be("Lightning Bolt");
    }

    [Fact]
    public void DeckCardsWithOracleIdSpecification_WithOracleIdAndUserId_FiltersByUserOwnedDecks()
    {
        // Arrange
        var oracleId = "oracle-123";
        var userId = "user-123";
        var spec = new DeckCardsWithOracleIdSpecification(oracleId, userId);

        var userDeckCard = new DeckCard
        {
            DeckId = 1,
            OracleId = oracleId,
            Name = "Lightning Bolt",
            SetCode = "LEA",
            MaindeckQuantity = 4,
            SideboardQuantity = 0,
            Deck = new Deck { OwnerId = userId, Name = "User Deck", Format = "Standard" }
        };

        var otherUserDeckCard = new DeckCard
        {
            DeckId = 2,
            OracleId = oracleId,
            Name = "Lightning Bolt",
            SetCode = "LEA",
            MaindeckQuantity = 3,
            SideboardQuantity = 0,
            Deck = new Deck { OwnerId = "other-user", Name = "Other Deck", Format = "Modern" }
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
        var oracleId = "oracle-123";
        var spec = new DeckCardsWithOracleIdSpecification(oracleId);

        // Assert
        spec.Includes.Should().HaveCount(2);
        spec.Includes.Should().Contain(include => include.Body.ToString().Contains("Deck"));
        spec.Includes.Should().Contain(include => include.Body.ToString().Contains("OwnedCard"));
    }

    [Fact]
    public void DeckCardsWithOracleIdSpecification_OrdersByName()
    {
        // Arrange
        var oracleId = "oracle-123";
        var spec = new DeckCardsWithOracleIdSpecification(oracleId);

        // Assert
        spec.OrderBy.Should().NotBeNull();
        spec.OrderBy!.Body.ToString().Should().Contain("Name");
    }

    [Fact]
    public void DeckCardsWithOracleIdSpecification_AllConstructors_IncludeSameNavigationProperties()
    {
        // Arrange
        var oracleId = "oracle-123";
        var deckId = 1;
        var userId = "user-123";

        var spec1 = new DeckCardsWithOracleIdSpecification(oracleId);
        var spec2 = new DeckCardsWithOracleIdSpecification(oracleId, deckId);
        var spec3 = new DeckCardsWithOracleIdSpecification(oracleId, userId);

        // Assert
        spec1.Includes.Should().HaveCount(2);
        spec2.Includes.Should().HaveCount(2);
        spec3.Includes.Should().HaveCount(2);

        foreach (var spec in new[] { spec1, spec2, spec3 })
        {
            spec.Includes.Should().Contain(include => include.Body.ToString().Contains("Deck"));
            spec.Includes.Should().Contain(include => include.Body.ToString().Contains("OwnedCard"));
            spec.OrderBy.Should().NotBeNull();
            spec.OrderBy!.Body.ToString().Should().Contain("Name");
        }
    }
}
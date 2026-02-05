using FluentAssertions;
using Core.Models;
using Core.Specifications;
using TestUtilities.Builders;

namespace UnitTests.Specifications;

public class DeckCardsWithDeckIdSpecificationTests
{
    public DeckCardsWithDeckIdSpecificationTests()
    {
        var unused = new TestDataBuilder();
    }

    [Fact]
    public void DeckCardsWithDeckIdSpecification_WithDeckId_FiltersByDeckId()
    {
        // Arrange
        const int deckId = 1;
        var spec = new DeckCardsWithDeckIdSpecification(deckId);

        var deckCard1 = new DeckCard
        {
            DeckId = deckId,
            ScryfallId = "oracle-1",
            OracleId = "oracle-1",
            Name = "Lightning Bolt",
            SetCode = "LEA",
            TypeLine = "Instant",
            MaindeckQuantity = 4,
            SideboardQuantity = 0
        };

        var deckCard2 = new DeckCard
        {
            DeckId = 2,
            ScryfallId = "oracle-2", 
            OracleId = "oracle-2",
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
        filtered.First().DeckId.Should().Be(deckId);
        filtered.First().Name.Should().Be("Lightning Bolt");
    }

    [Fact]
    public void DeckCardsWithDeckIdSpecification_WithMaindeckOnly_FiltersMaindeckCards()
    {
        // Arrange
        const int deckId = 1;
        var spec = new DeckCardsWithDeckIdSpecification(deckId, maindeckOnly: true);

        var maindeckCard = new DeckCard
        {
            DeckId = deckId,
            ScryfallId = "oracle-1",
            OracleId = "oracle-1",
            Name = "Lightning Bolt",
            SetCode = "LEA",
            TypeLine = "Instant",
            MaindeckQuantity = 4,
            SideboardQuantity = 0
        };

        var sideboardOnlyCard = new DeckCard
        {
            DeckId = deckId,
            ScryfallId = "oracle-2",
            OracleId = "oracle-2",
            Name = "Counterspell",
            SetCode = "ICE", 
            TypeLine = "Instant",
            MaindeckQuantity = 0,
            SideboardQuantity = 2
        };

        var deckCards = new List<DeckCard> { maindeckCard, sideboardOnlyCard };

        // Act
        var filtered = deckCards.Where(spec.Criteria.Compile()).ToList();

        // Assert
        filtered.Should().HaveCount(1);
        filtered.First().Name.Should().Be("Lightning Bolt");
        filtered.First().MaindeckQuantity.Should().BeGreaterThan(0);
    }

    [Fact]
    public void DeckCardsWithDeckIdSpecification_WithSideboardOnly_FiltersSideboardCards()
    {
        // Arrange
        const int deckId = 1;
        var spec = new DeckCardsWithDeckIdSpecification(deckId, maindeckOnly: false, sideboardOnly: true);

        var maindeckCard = new DeckCard
        {
            DeckId = deckId,
            ScryfallId = "oracle-1",
            OracleId = "oracle-1",
            Name = "Lightning Bolt",
            SetCode = "LEA",
            TypeLine = "Instant",
            MaindeckQuantity = 4,
            SideboardQuantity = 0
        };

        var sideboardCard = new DeckCard
        {
            DeckId = deckId,
            ScryfallId = "oracle-2",
            OracleId = "oracle-2",
            Name = "Counterspell",
            SetCode = "ICE",
            TypeLine = "Instant",
            MaindeckQuantity = 0,
            SideboardQuantity = 2
        };

        var bothCard = new DeckCard
        {
            DeckId = deckId,
            ScryfallId = "oracle-3",
            OracleId = "oracle-3",
            Name = "Brainstorm",
            SetCode = "ICE",
            TypeLine = "Instant",
            MaindeckQuantity = 3,
            SideboardQuantity = 1
        };

        var deckCards = new List<DeckCard> { maindeckCard, sideboardCard, bothCard };

        // Act
        var filtered = deckCards.Where(spec.Criteria.Compile()).ToList();

        // Assert
        filtered.Should().HaveCount(2);
        filtered.Should().Contain(c => c.Name == "Counterspell");
        filtered.Should().Contain(c => c.Name == "Brainstorm");
        filtered.Should().NotContain(c => c.Name == "Lightning Bolt");
    }

    [Fact]
    public void DeckCardsWithDeckIdSpecification_IncludesDeckAndOwnedCard()
    {
        // Arrange
        const int deckId = 1;
        var spec = new DeckCardsWithDeckIdSpecification(deckId);

        // Assert
        spec.Includes.Should().HaveCount(2);
        spec.Includes.Should().Contain(include => include.Body.ToString().Contains("Deck"));
        spec.Includes.Should().Contain(include => include.Body.ToString().Contains("OwnedCard"));
    }

    [Fact]
    public void DeckCardsWithDeckIdSpecification_OrdersByName()
    {
        // Arrange
        const int deckId = 1;
        var spec = new DeckCardsWithDeckIdSpecification(deckId);

        // Assert
        spec.OrderBy.Should().NotBeNull();
        spec.OrderBy!.Body.ToString().Should().Contain("Name");
    }

    [Fact]
    public void DeckCardsWithDeckIdSpecification_WithBothFilters_FiltersCorrectly()
    {
        // Arrange
        const int deckId = 1;
        var spec = new DeckCardsWithDeckIdSpecification(deckId, maindeckOnly: true, sideboardOnly: false);

        var maindeckOnly = new DeckCard
        {
            DeckId = deckId,
            ScryfallId = "oracle-1",
            OracleId = "oracle-1",
            Name = "Lightning Bolt",
            SetCode = "LEA",
            TypeLine = "Instant",
            MaindeckQuantity = 4,
            SideboardQuantity = 0
        };

        var sideboardOnly = new DeckCard
        {
            DeckId = deckId,
            ScryfallId = "oracle-2",
            OracleId = "oracle-2",
            Name = "Counterspell",
            SetCode = "ICE",
            TypeLine = "Instant",
            MaindeckQuantity = 0,
            SideboardQuantity = 2
        };

        var bothDecks = new DeckCard
        {
            DeckId = deckId,
            ScryfallId = "oracle-3",
            OracleId = "oracle-3",
            Name = "Brainstorm",
            SetCode = "ICE",
            TypeLine = "Instant",
            MaindeckQuantity = 3,
            SideboardQuantity = 1
        };

        var deckCards = new List<DeckCard> { maindeckOnly, sideboardOnly, bothDecks };

        // Act
        var filtered = deckCards.Where(spec.Criteria.Compile()).ToList();

        // Assert
        filtered.Should().HaveCount(2);
        filtered.Should().Contain(c => c.Name == "Lightning Bolt");
        filtered.Should().Contain(c => c.Name == "Brainstorm");
        filtered.Should().NotContain(c => c.Name == "Counterspell");
    }
}

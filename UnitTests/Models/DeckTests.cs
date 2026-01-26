using System;
using FluentAssertions;
using Core.Models;
using TestUtilities.Builders;
using Core.Enums;

namespace UnitTests.Models;

/// <summary>
/// Tests for Deck entity business logic and behavior.
/// Focuses on domain rules and entity state management.
/// </summary>
public class DeckTests
{
    private readonly TestDataBuilder _testDataBuilder;

    public DeckTests()
    {
        _testDataBuilder = new TestDataBuilder();
    }

    [Fact]
    public void Deck_WhenCreated_HasValidInitialState()
    {
        // Arrange & Act
        var ownerId = "test-user-id";
        var deck = _testDataBuilder.CreateDeck(ownerId);

        // Assert
        deck.Id.Should().Be(0, "because new deck entities are not persisted yet");
        deck.OwnerId.Should().Be(ownerId, "because deck should belong to the specified user");
        deck.Name.Should().NotBeNullOrEmpty("because deck name is required");
        Enum.IsDefined(typeof(DeckFormat), deck.Format).Should().BeTrue("because format is required");
        deck.NumberOfCards.Should().BeGreaterThanOrEqualTo(0, "because number of cards cannot be negative");
        deck.TotalPrice.Should().BeGreaterThanOrEqualTo(0, "because total price cannot be negative");
    }

    [Fact]
    public void Deck_BelongsToOwner_MaintainsOwnership()
    {
        // Arrange & Act
        var owner1 = "owner-1";
        var owner2 = "owner-2";

        var deck1 = _testDataBuilder.CreateDeck(owner1);
        deck1.Name = "Owner 1 Deck";

        var deck2 = _testDataBuilder.CreateDeck(owner2);
        deck2.Name = "Owner 2 Deck";

        // Assert
        deck1.OwnerId.Should().Be(owner1, "because deck1 belongs to owner1");
        deck2.OwnerId.Should().Be(owner2, "because deck2 belongs to owner2");
        deck1.OwnerId.Should().NotBe(deck2.OwnerId, "because decks belong to different owners");
    }

    [Fact]
    public void Deck_WithZeroCards_IsValidEmptyDeck()
    {
        // Arrange & Act
        var deck = _testDataBuilder.CreateDeck("user-id");
        deck.NumberOfCards = 0;
        deck.TotalPrice = 0.00;
        deck.Name = "Empty Deck";

        // Assert
        deck.NumberOfCards.Should().Be(0, "because this is an empty deck");
        deck.TotalPrice.Should().Be(0.00, "because an empty deck has no value");
        deck.Name.Should().Be("Empty Deck");
    }

    [Fact]
    public void Deck_InheritingFromBaseModel_HasBaseModelProperties()
    {
        // Arrange & Act
        var deck = _testDataBuilder.CreateDeck("user-id");

        // Assert
        deck.Should().BeAssignableTo<BaseModel>("because Deck inherits from BaseModel");
        deck.Id.Should().Be(0, "because BaseModel's identity key is assigned on persistence");
    }

    [Fact]
    public void Deck_WhenCreated_HasEmptyDeckCardsCollection()
    {
        // Arrange & Act
        var deck = _testDataBuilder.CreateDeck("user-id");

        // Assert
        deck.DeckCards.Should().NotBeNull("because DeckCards collection should be initialized");
        deck.DeckCards.Should().BeEmpty("because new deck should have no cards initially");
    }

    [Fact]
    public void Deck_DeckCardsCollection_CanAddDeckCards()
    {
        // Arrange
        var deck = _testDataBuilder.CreateDeck("user-id");
        var deckCard = new DeckCard
        {
            DeckId = deck.Id,
            ScryfallId = "12345678-1234-1234-1234-123456789012",
            Name = "Lightning Bolt",
            SetCode = "LEA",
            TypeLine = "Instant",
            MaindeckQuantity = 4,
            SideboardQuantity = 0
        };

        // Act
        deck.DeckCards.Add(deckCard);

        // Assert
        deck.DeckCards.Should().HaveCount(1, "because we added one deck card");
        deck.DeckCards.First().Name.Should().Be("Lightning Bolt");
        deck.DeckCards.First().DeckId.Should().Be(deck.Id);
    }
}

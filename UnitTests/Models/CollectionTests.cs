using FluentAssertions;
using Core.Models;
using TestUtilities.Builders;

namespace UnitTests.Models;

/// <summary>
/// Tests for Collection entity business logic and behavior.
/// Focuses on domain rules and entity state management.
/// </summary>
public class CollectionTests
{
    private readonly TestDataBuilder _testDataBuilder;

    public CollectionTests()
    {
        _testDataBuilder = new TestDataBuilder();
    }

    [Fact]
    public void Collection_WhenCreated_HasValidInitialState()
    {
        // Arrange & Act
        var userId = "test-user-id";
        var collection = _testDataBuilder.CreateCollection(userId);

        // Assert
        collection.Id.Should().BeGreaterThan(0, "because collections should have valid IDs");
        collection.OwnerId.Should().Be(userId, "because collection should belong to the specified user");
        collection.NumberOfCards.Should().Be(0, "because new collections start with no cards");
    }

    // [Theory]
    // [InlineData("")]
    // [InlineData("   ")]
    // [InlineData(null)]
    // public void Collection_WithInvalidName_ThrowsArgumentException(string invalidName)
    // {
    //     // Arrange
    //     var userId = "test-user-id";
    //
    //     // Act & Assert
    //     var act = () => {
    //         var collection = _testDataBuilder.CreateCollection(userId);
    //         collection.Name = invalidName;
    //     };
    //
    //     act.Should().Throw<ArgumentException>("because collection names cannot be empty or whitespace");
    // }

    // [Fact]
    // public void Collection_AddCard_IncreasesCardCount()
    // {
    //     // Arrange
    //     var collection = _testDataBuilder.CreateCollection("user-id");
    //     var card = _testDataBuilder.CreateCard("Lightning Bolt");
    //
    //     // Act
    //     collection.AddCard(card, quantity: 4);
    //
    //     // Assert
    //     collection.Cards.Should().ContainSingle("because we added one card type");
    //     collection.TotalCards.Should().Be(4, "because we added 4 copies of the card");
    //     collection.Cards.First().Quantity.Should().Be(4);
    // }
    //
    // [Fact]
    // public void Collection_AddSameCardTwice_UpdatesQuantity()
    // {
    //     // Arrange
    //     var collection = _testDataBuilder.CreateCollection("user-id");
    //     var card = _testDataBuilder.CreateCard("Lightning Bolt");
    //
    //     // Act
    //     collection.AddCard(card, quantity: 2);
    //     collection.AddCard(card, quantity: 3); // Add more of the same card
    //
    //     // Assert
    //     collection.Cards.Should().ContainSingle("because it's still the same card type");
    //     collection.Cards.First().Quantity.Should().Be(5, "because 2 + 3 = 5");
    //     collection.TotalCards.Should().Be(5);
    // }
    //
    // [Theory]
    // [InlineData(0)]
    // [InlineData(-1)]
    // [InlineData(-10)]
    // public void Collection_AddCardWithInvalidQuantity_ThrowsArgumentException(int invalidQuantity)
    // {
    //     // Arrange
    //     var collection = _testDataBuilder.CreateCollection("user-id");
    //     var card = _testDataBuilder.CreateCard();
    //
    //     // Act & Assert
    //     var act = () => collection.AddCard(card, invalidQuantity);
    //     act.Should().Throw<ArgumentException>("because quantity must be positive");
    // }
    //
    // [Fact]
    // public void Collection_RemoveCard_DecreasesQuantity()
    // {
    //     // Arrange
    //     var collection = _testDataBuilder.CreateCollection("user-id");
    //     var card = _testDataBuilder.CreateCard();
    //     collection.AddCard(card, 5);
    //
    //     // Act
    //     collection.RemoveCard(card, 2);
    //
    //     // Assert
    //     collection.Cards.First().Quantity.Should().Be(3, "because 5 - 2 = 3");
    //     collection.TotalCards.Should().Be(3);
    // }
    //
    // [Fact]
    // public void Collection_RemoveAllOfCard_RemovesCardCompletely()
    // {
    //     // Arrange
    //     var collection = _testDataBuilder.CreateCollection("user-id");
    //     var card1 = _testDataBuilder.CreateCard("Card1");
    //     var card2 = _testDataBuilder.CreateCard("Card2");
    //     
    //     collection.AddCard(card1, 3);
    //     collection.AddCard(card2, 2);
    //
    //     // Act
    //     collection.RemoveCard(card1, 3); // Remove all of card1
    //
    //     // Assert
    //     collection.Cards.Should().ContainSingle("because card1 should be completely removed");
    //     collection.Cards.Should().Contain(cc => cc.Card.Name == "Card2");
    //     collection.TotalCards.Should().Be(2, "because only card2 remains");
    // }
    //
    // [Fact]
    // public void Collection_CalculateTotalValue_SumsAllCardValues()
    // {
    //     // Arrange
    //     var collection = _testDataBuilder.CreateCollection("user-id");
    //     var expensiveCard = _testDataBuilder.CreateCard("Black Lotus", 15000.00m);
    //     var cheapCard = _testDataBuilder.CreateCard("Lightning Bolt", 2.50m);
    //
    //     collection.AddCard(expensiveCard, 1);
    //     collection.AddCard(cheapCard, 4);
    //
    //     // Act
    //     var totalValue = collection.CalculateTotalValue();
    //
    //     // Assert - 1 * 15000.00 + 4 * 2.50 = 15010.00
    //     totalValue.Should().Be(15010.00m, "because total should be sum of all card values");
    // }
    //
    // [Fact]
    // public void Collection_GetCardsByFormat_FiltersCorrectly()
    // {
    //     // Arrange
    //     var collection = _testDataBuilder.CreateCollection("user-id");
    //     
    //     var modernCard = _testDataBuilder.CreateCard("Modern Card");
    //     modernCard.LegalFormats = new[] { "Modern", "Legacy" };
    //     
    //     var legacyOnlyCard = _testDataBuilder.CreateCard("Legacy Card");
    //     legacyOnlyCard.LegalFormats = new[] { "Legacy", "Vintage" };
    //
    //     collection.AddCard(modernCard, 1);
    //     collection.AddCard(legacyOnlyCard, 1);
    //
    //     // Act
    //     var modernCards = collection.GetCardsByFormat("Modern");
    //
    //     // Assert
    //     modernCards.Should().ContainSingle("because only one card is Modern-legal");
    //     modernCards.First().Card.Name.Should().Be("Modern Card");
    // }
    //
    // [Fact]
    // public void Collection_Clone_CreatesIndependentCopy()
    // {
    //     // Arrange
    //     var original = _testDataBuilder.CreateCollection("user-id");
    //     var card = _testDataBuilder.CreateCard();
    //     original.AddCard(card, 3);
    //     original.Name = "Original Collection";
    //
    //     // Act
    //     var clone = original.Clone();
    //     clone.Name = "Cloned Collection";
    //     clone.AddCard(card, 2); // Should not affect original
    //
    //     // Assert
    //     clone.Should().NotBeSameAs(original, "because it should be a different instance");
    //     clone.Name.Should().Be("Cloned Collection");
    //     original.Name.Should().Be("Original Collection", "because original should be unchanged");
    //     
    //     // Verify card quantities are independent
    //     original.Cards.First().Quantity.Should().Be(3, "because original should be unchanged");
    //     clone.Cards.First().Quantity.Should().Be(5, "because clone should have 3 + 2 = 5");
    // }
}

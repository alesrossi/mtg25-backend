using FluentAssertions;
using Core.Enums;
using Core.Models;
using TestUtilities.Builders;

namespace UnitTests.Models;

/// <summary>
/// Tests for Card entity business logic and behavior.
/// Focuses on domain rules and entity state management.
/// </summary>
public class CardTests
{
    private readonly TestDataBuilder _testDataBuilder;

    public CardTests()
    {
        _testDataBuilder = new TestDataBuilder();
    }

    [Fact]
    public void Card_WhenCreated_HasValidInitialState()
    {
        // Arrange & Act
        const int collectionId = 1;
        var card = _testDataBuilder.CreateCard(collectionId);

        // Assert
        card.CollectionId.Should().Be(collectionId, "because card should belong to the specified collection");
        card.Name.Should().NotBeNullOrEmpty("because card name is required");
        card.ScryfallId.Should().NotBeNullOrEmpty("because oracle ID is required for card identification");
        card.Quantity.Should().BeGreaterThan(0, "because quantity must be positive");
        card.Language.Should().Be(Language.En, "because language is required");
        Enum.IsDefined(typeof(Currency), card.PurchasePriceCurrency).Should().BeTrue("because currency is required");
        card.ImageUrl.Should().NotBeNullOrEmpty("because image URL is required");
        card.SetCode.Should().NotBeNullOrEmpty("because set code is required");
        card.SetName.Should().NotBeNullOrEmpty("because set name is required");
        card.CollectorNumber.Should().NotBeNullOrEmpty("because collector number is required");
        card.Rarity.Should().NotBeNullOrEmpty("because rarity is required");
    }

    [Theory]
    [InlineData(Condition.Mint)]
    [InlineData(Condition.NearMint)]
    [InlineData(Condition.Excellent)]
    [InlineData(Condition.Good)]
    [InlineData(Condition.LightPlayed)]
    [InlineData(Condition.Played)]
    [InlineData(Condition.Poor)]
    public void Card_WithDifferentConditions_AcceptsAllValidConditions(Condition condition)
    {
        // Arrange & Act
        var card = _testDataBuilder.CreateCard(1);
        card.Condition = condition;

        // Assert
        card.Condition.Should().Be(condition, $"because {condition} is a valid card condition");
    }
    
    [Theory]
    [InlineData("Broken")]
    [InlineData("Old")]
    [InlineData("Wet")]
    public void Card_WithInvalidConditions_FailsParsing(string condition)
    {
        // Arrange & Act
        var result = Enum.TryParse(condition, out Condition _);

        // Assert
        result.Should().Be(false, $"because {condition} is not a valid card condition");
    }

    [Fact]
    public void Card_WithPurchasePrice_StoresCorrectValue()
    {
        // Arrange & Act
        var card = _testDataBuilder.CreateCard(1);
        card.PurchasePrice = 15.75;
        card.PurchasePriceCurrency = Currency.Usd;

        // Assert
        card.PurchasePrice.Should().Be(15.75, "because that's the purchase price we set");
        card.PurchasePriceCurrency.Should().Be(Currency.Usd, "because that's the currency we set");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(10)]
    [InlineData(100)]
    public void Card_WithDifferentQuantities_StoresCorrectQuantity(int quantity)
    {
        // Arrange & Act
        var card = _testDataBuilder.CreateCard(1);
        card.Quantity = quantity;

        // Assert
        card.Quantity.Should().Be(quantity, $"because we set the quantity to {quantity}");
    }

    [Fact]
    public void Card_WithImageUrl_StoresImageReference()
    {
        // Arrange & Act
        var card = _testDataBuilder.CreateCard(1);
        const string imageUrl = "https://cards.scryfall.io/normal/front/1/2/123456.jpg";
        card.ImageUrl = imageUrl;

        // Assert
        card.ImageUrl.Should().Be(imageUrl, "because that's the image URL we set");
    }

    [Fact]
    public void Card_InheritingFromBaseModel_HasBaseModelProperties()
    {
        // Arrange & Act
        var card = _testDataBuilder.CreateCard(1);

        // Assert
        card.Should().BeAssignableTo<BaseModel>("because Card inherits from BaseModel");
    }
}

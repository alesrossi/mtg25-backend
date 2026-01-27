using FluentAssertions;
using Core.Models;
using TestUtilities.Builders;

namespace UnitTests.Models;

public class TradeBinderTests
{
    private readonly TestDataBuilder _testDataBuilder = new();

    [Fact]
    public void TradeBinder_WhenCreated_HasExpectedDefaults()
    {
        const string ownerId = "binder-owner";

        var tradeBinder = _testDataBuilder.CreateTradeBinder(ownerId, isPublic: false);

        tradeBinder.Id.Should().Be(0, "because new trade binders have not been persisted yet");
        tradeBinder.OwnerId.Should().Be(ownerId);
        tradeBinder.IsPublic.Should().BeFalse();
        tradeBinder.BinderCards.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void TradeBinder_BinderCardsCollection_AllowsAddingCards()
    {
        var tradeBinder = _testDataBuilder.CreateTradeBinder("owner");
        var binderCard = _testDataBuilder.CreateBinderCard(tradeBinder.Id, name: "Lightning Bolt", quantityToTrade: 2);

        tradeBinder.BinderCards.Add(binderCard);

        tradeBinder.BinderCards.Should().HaveCount(1);
        tradeBinder.BinderCards.First().Name.Should().Be("Lightning Bolt");
    }
}

public class BinderCardTests
{
    private readonly TestDataBuilder _testDataBuilder = new();

    [Fact]
    public void BinderCard_WhenCreated_HasExpectedDefaults()
    {
        var tradeBinder = _testDataBuilder.CreateTradeBinder("owner");

        var binderCard = _testDataBuilder.CreateBinderCard(tradeBinder.Id, cardId: 123, name: "Counterspell");

        binderCard.Id.Should().Be(0, "because binder cards are not yet persisted");
        binderCard.TradeBinderId.Should().Be(tradeBinder.Id);
        binderCard.CardId.Should().Be(123);
        binderCard.Card.Should().BeNull("because card navigation is optional by default");
    }

    [Fact]
    public void BinderCard_CanReferenceLoadedCardWhenProvided()
    {
        var tradeBinder = _testDataBuilder.CreateTradeBinder("owner");
        var collection = _testDataBuilder.CreateCollection("owner");
        var card = _testDataBuilder.CreateCard(collection.Id, name: "Sol Ring");
        card.Id = 42;

        var binderCard = _testDataBuilder.CreateBinderCard(tradeBinder.Id, cardId: card.Id, name: card.Name, quantityToTrade: 1);
        binderCard.Card = card;

        binderCard.CardId.Should().Be(42);
        binderCard.Card.Should().NotBeNull();
        binderCard.Card!.Name.Should().Be("Sol Ring");
    }

    [Fact]
    public void BinderCard_InheritsFromBaseModel()
    {
        var binderCard = _testDataBuilder.CreateBinderCard(1, cardId: 5);

        binderCard.Should().BeAssignableTo<BaseModel>();
    }
}


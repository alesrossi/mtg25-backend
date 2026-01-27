using FluentAssertions;
using TestUtilities.Builders;
using TestUtilities.Database;

namespace UnitTests.Data;

public class MainContextTests
{
    [Fact]
    public void DeletingCard_CascadesToBinderCards()
    {
        using var context = InMemoryDbContextFactory.CreateMain();
        var builder = new TestDataBuilder();

        var collection = builder.CreateCollection("user-1");
        context.Collections.Add(collection);
        context.SaveChanges();

        var card = builder.CreateCard(collection.Id, name: "Counterspell");
        context.Cards.Add(card);

        var binder = builder.CreateTradeBinder("user-1");
        context.TradeBinders.Add(binder);
        context.SaveChanges();

        var binderCard = builder.CreateBinderCard(binder.Id, cardId: card.Id, name: card.Name, quantityToTrade: 1);
        context.BinderCards.Add(binderCard);
        context.SaveChanges();

        context.BinderCards.Count().Should().Be(1);

        context.Cards.Remove(card);
        context.SaveChanges();

        context.BinderCards.Should().BeEmpty();
    }

    [Fact]
    public void QueryingBinderCards_LoadsCardNavigation()
    {
        using var context = InMemoryDbContextFactory.CreateMain();
        var builder = new TestDataBuilder();

        var collection = builder.CreateCollection("user-2");
        context.Collections.Add(collection);
        context.SaveChanges();

        var card = builder.CreateCard(collection.Id, name: "Lightning Bolt");
        context.Cards.Add(card);

        var binder = builder.CreateTradeBinder("user-2");
        context.TradeBinders.Add(binder);
        context.SaveChanges();

        var binderCard = builder.CreateBinderCard(binder.Id, cardId: card.Id, name: card.Name, quantityToTrade: 2);
        context.BinderCards.Add(binderCard);
        context.SaveChanges();

        context.ChangeTracker.Clear();

        var reloaded = context.BinderCards.First(bc => bc.Id == binderCard.Id);

        reloaded.Card.Should().NotBeNull();
        reloaded.Card!.Id.Should().Be(card.Id);
        reloaded.Card.Name.Should().Be("Lightning Bolt");
    }
}

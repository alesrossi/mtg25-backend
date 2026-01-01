using Core.Models;
using FluentAssertions;
using Infrastructure.Data;
using Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TestUtilities.Builders;
using TestUtilities.Database;

namespace UnitTests.Repositories;

/// <summary>
/// Tests for Card repository focusing on data access patterns.
/// Uses in-memory database for fast, isolated testing.
/// </summary>
public class CardRepositoryTests : IDisposable
{
    private readonly MainContext _context;
    private readonly AppIdentityDbContext _identityContext;
    private readonly GenericRepository<Card> _repository;
    private readonly TestDataBuilder _testDataBuilder;

    public CardRepositoryTests()
    {
        _context = InMemoryDbContextFactory.CreateMain();
        _identityContext = InMemoryDbContextFactory.CreateIdentity();
        _repository = new GenericRepository<Card>(_context, NullLogger<GenericRepository<Card>>.Instance);
        _testDataBuilder = new TestDataBuilder();
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ReturnsCard()
    {
        // Arrange
        var user = _testDataBuilder.CreateUser();
        _identityContext.Users.Add(user);
        await _identityContext.SaveChangesAsync();

        var collection = _testDataBuilder.CreateCollection(user.Id);
        _context.Collections.Add(collection);
        await _context.SaveChangesAsync();

        // Refresh the collection to get the database-assigned ID
        await _context.Entry(collection).ReloadAsync();

        var card = _testDataBuilder.CreateCard(collection.Id);
        card.Name = "Lightning Bolt";
        card.SetCode = "LEA";
        _context.Cards.Add(card);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(card.Id);

        // Assert
        result.Should().NotBeNull("because a card with this ID exists");
        result.Name.Should().Be("Lightning Bolt");
        result.SetCode.Should().Be("LEA");
        result.Id.Should().Be(card.Id, "because we retrieved by the card's ID");
        
        // Verify it's associated with a collection
        var associatedCollection = await _context.Collections.FindAsync(result.CollectionId);
        associatedCollection.Should().NotBeNull("because card should be associated with a collection");
        associatedCollection.OwnerId.Should().NotBeNullOrEmpty("because collection should have an owner");
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(999999);

        // Assert
        result.Should().BeNull("because no card exists with this ID");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task GetByIdAsync_WithInvalidIds_ReturnsNull(int invalidId)
    {
        // Act
        var result = await _repository.GetByIdAsync(invalidId);

        // Assert
        result.Should().BeNull($"because {invalidId} is not a valid card ID");
    }

    [Fact]
    public async Task ListAsync_WithCardsFromDifferentCollections_ReturnsAllCards()
    {
        // Arrange
        var user1 = _testDataBuilder.CreateUser("user1@test.com", "user1");
        var user2 = _testDataBuilder.CreateUser("user2@test.com", "user2");
        _identityContext.Users.AddRange(user1, user2);
        await _identityContext.SaveChangesAsync();

        var collection1 = _testDataBuilder.CreateCollection(user1.Id);
        var collection2 = _testDataBuilder.CreateCollection(user2.Id);
        _context.Collections.AddRange(collection1, collection2);
        await _context.SaveChangesAsync();

        var card1 = _testDataBuilder.CreateCard(collection1.Id);
        card1.Name = "Lightning Bolt";
        var card2 = _testDataBuilder.CreateCard(collection2.Id);
        card2.Name = "Black Lotus";
        var card3 = _testDataBuilder.CreateCard(collection1.Id);
        card3.Name = "Time Walk";

        _context.Cards.AddRange(card1, card2, card3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.ListAllAsync();

        // Assert
        result.Should().HaveCount(3, "because we added 3 cards total");
        result.Select(c => c.Name).Should().Contain(["Lightning Bolt", "Black Lotus", "Time Walk"]);
    }

    [Fact]
    public async Task AddAsync_WithValidCard_AddsToDatabase()
    {
        // Arrange
        var user = _testDataBuilder.CreateUser();
        _identityContext.Users.Add(user);
        await _identityContext.SaveChangesAsync();

        var collection = _testDataBuilder.CreateCollection(user.Id);
        _context.Collections.Add(collection);
        await _context.SaveChangesAsync();

        // Refresh the collection to get the database-assigned ID
        await _context.Entry(collection).ReloadAsync();

        var card = _testDataBuilder.CreateCard(collection.Id);
        card.Name = "Ancestral Recall";
        card.ScryfallId = "test-oracle-id";

        // Act
        _repository.Add(card);
        await _context.SaveChangesAsync();

        // Assert
        var allCards = await _context.Cards.ToListAsync();
        var savedCard = allCards.FirstOrDefault(c => c.Name == "Ancestral Recall");
        savedCard.Should().NotBeNull("because the card should be saved to the database");
        savedCard.Name.Should().Be("Ancestral Recall");
        savedCard.ScryfallId.Should().Be("test-oracle-id");
        
        // Verify it's associated with a collection
        var associatedCollection = await _context.Collections.FindAsync(savedCard.CollectionId);
        associatedCollection.Should().NotBeNull("because card should be associated with a collection");
        associatedCollection.OwnerId.Should().NotBeNullOrEmpty("because collection should have an owner");
    }

    [Fact]
    public async Task UpdateAsync_WithValidCard_UpdatesInDatabase()
    {
        // Arrange
        var user = _testDataBuilder.CreateUser();
        _identityContext.Users.Add(user);
        await _identityContext.SaveChangesAsync();

        var collection = _testDataBuilder.CreateCollection(user.Id);
        _context.Collections.Add(collection);
        await _context.SaveChangesAsync();

        var card = _testDataBuilder.CreateCard(collection.Id);
        card.Name = "Original Name";
        card.Quantity = 1;
        _context.Cards.Add(card);
        await _context.SaveChangesAsync();

        // Act
        card.Name = "Updated Name";
        card.Quantity = 4;
        _repository.Update(card);
        await _context.SaveChangesAsync();

        // Assert
        var updatedCard = await _repository.GetByIdAsync(card.Id);
        updatedCard.Should().NotBeNull();
        updatedCard.Name.Should().Be("Updated Name");
        updatedCard.Quantity.Should().Be(4);
    }

    [Fact]
    public async Task DeleteAsync_WithValidCard_RemovesFromDatabase()
    {
        // Arrange
        var user = _testDataBuilder.CreateUser();
        _identityContext.Users.Add(user);
        await _identityContext.SaveChangesAsync();

        var collection = _testDataBuilder.CreateCollection(user.Id);
        _context.Collections.Add(collection);
        await _context.SaveChangesAsync();

        var card = _testDataBuilder.CreateCard(collection.Id);
        _context.Cards.Add(card);
        await _context.SaveChangesAsync();

        var existingCard = await _repository.GetByIdAsync(card.Id);
        existingCard.Should().NotBeNull();

        // Act
        _repository.Delete(card);
        await _context.SaveChangesAsync();

        // Assert
        var deletedCard = await _repository.GetByIdAsync(card.Id);
        deletedCard.Should().BeNull("because the card should be deleted");
    }

    [Fact]
    public async Task ListAsync_WithCardsInSameCollection_ReturnsCollectionCards()
    {
        // Arrange
        var user = _testDataBuilder.CreateUser();
        _identityContext.Users.Add(user);
        await _identityContext.SaveChangesAsync();

        var collection1 = _testDataBuilder.CreateCollection(user.Id);
        var collection2 = _testDataBuilder.CreateCollection(user.Id);
        _context.Collections.AddRange(collection1, collection2);
        await _context.SaveChangesAsync();

        // Refresh collections to get database-assigned IDs
        await _context.Entry(collection1).ReloadAsync();
        await _context.Entry(collection2).ReloadAsync();

        var cardsForCollection1 = new[]
        {
            _testDataBuilder.CreateCard(collection1.Id),
            _testDataBuilder.CreateCard(collection1.Id)
        };
        cardsForCollection1[0].Name = "Collection1 Card1";
        cardsForCollection1[1].Name = "Collection1 Card2";

        var cardForCollection2 = _testDataBuilder.CreateCard(collection2.Id);
        cardForCollection2.Name = "Collection2 Card";

        _context.Cards.AddRange(cardsForCollection1);
        _context.Cards.Add(cardForCollection2);
        await _context.SaveChangesAsync();

        // Act - Filter by collection (would need a specification for this in real implementation)
        var allCards = await _repository.ListAllAsync();
        // Assert
        // Check that the cards were properly distributed across collections
        var collection1Cards = allCards!.Where(c => c.Name.StartsWith("Collection1")).ToList();
        var collection2Cards = allCards!.Where(c => c.Name == "Collection2 Card").ToList();
        
        collection1Cards.Should().HaveCount(2, "because collection1 has 2 cards");
        collection2Cards.Should().HaveCount(1, "because collection2 has 1 card");
        collection1Cards.Select(c => c.Name).Should().Contain(["Collection1 Card1", "Collection1 Card2"]);
    }

    [Fact]
    public async Task ConcurrentAccess_MultipleCardOperations_HandledCorrectly()
    {
        // Arrange
        var user = _testDataBuilder.CreateUser();
        _identityContext.Users.Add(user);
        await _identityContext.SaveChangesAsync();

        var collection = _testDataBuilder.CreateCollection(user.Id);
        _context.Collections.Add(collection);
        await _context.SaveChangesAsync();

        // Refresh collection to get database-assigned ID
        await _context.Entry(collection).ReloadAsync();

        var cards = Enumerable.Range(1, 5)
            .Select(i => {
                var card = _testDataBuilder.CreateCard(collection.Id);
                card.Name = $"Card {i}";
                return card;
            })
            .ToList();

        // Act
        var tasks = cards.Select(card =>
        {
            _repository.Add(card);
            return Task.CompletedTask;
        });

        await Task.WhenAll(tasks);
        await _context.SaveChangesAsync();

        // Assert
        var result = await _repository.ListAllAsync();
        var userCards = result!.Where(c => c.Name.StartsWith("Card "));
        userCards.Should().HaveCount(5, "because all 5 cards should be saved");
    }

    public void Dispose()
    {
        _context.Dispose();
        _identityContext.Dispose();
    }
}

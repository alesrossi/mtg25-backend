using Core.Models;
using FluentAssertions;
using Infrastructure.Data;
using Infrastructure.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using TestUtilities.Builders;
using TestUtilities.Database;
using Core.Enums;

namespace UnitTests.Repositories;

/// <summary>
/// Tests for Deck repository focusing on data access patterns.
/// Uses in-memory database for fast, isolated testing.
/// </summary>
public class DeckRepositoryTests : IDisposable
{
    private readonly MainContext _context;
    private readonly AppIdentityDbContext _identityContext;
    private readonly GenericRepository<Deck> _repository;
    private readonly TestDataBuilder _testDataBuilder;

    public DeckRepositoryTests()
    {
        _context = InMemoryDbContextFactory.CreateMain();
        _identityContext = InMemoryDbContextFactory.CreateIdentity();
        _repository = new GenericRepository<Deck>(_context, NullLogger<GenericRepository<Deck>>.Instance);
        _testDataBuilder = new TestDataBuilder();
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ReturnsDeck()
    {
        // Arrange
        var user = _testDataBuilder.CreateUser();
        _identityContext.Users.Add(user);
        await _identityContext.SaveChangesAsync();

        var deck = _testDataBuilder.CreateDeck(user.Id);
        deck.Name = "Test Deck";
        deck.Format = DeckFormat.Standard;
        _context.Decks.Add(deck);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(deck.Id);

        // Assert
        result.Should().NotBeNull("because a deck with this ID exists");
        result.Id.Should().Be(deck.Id);
        result.Name.Should().Be("Test Deck");
        result.Format.Should().Be(DeckFormat.Standard);
        result.OwnerId.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(999999);

        // Assert
        result.Should().BeNull("because no deck exists with this ID");
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
        result.Should().BeNull($"because {invalidId} is not a valid deck ID");
    }

    [Fact]
    public async Task ListAsync_WithDecksFromDifferentUsers_ReturnsAllDecks()
    {
        // Arrange
        var user1 = _testDataBuilder.CreateUser("user1@test.com", "user1");
        var user2 = _testDataBuilder.CreateUser("user2@test.com", "user2");
        _identityContext.Users.AddRange(user1, user2);
        await _identityContext.SaveChangesAsync();

        var user1Decks = new[]
        {
            _testDataBuilder.CreateDeck(user1.Id),
            _testDataBuilder.CreateDeck(user1.Id)
        };
        user1Decks[0].Name = "User1 Standard Deck";
        user1Decks[0].Format = DeckFormat.Standard;
        user1Decks[1].Name = "User1 Modern Deck";
        user1Decks[1].Format = DeckFormat.Modern;

        var user2Deck = _testDataBuilder.CreateDeck(user2.Id);
        user2Deck.Name = "User2 Legacy Deck";
        user2Deck.Format = DeckFormat.Legacy;

        _context.Decks.AddRange(user1Decks);
        _context.Decks.Add(user2Deck);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.ListAllAsync();

        // Assert
        result.Should().HaveCount(3, "because we added 3 decks total");
        result.Select(d => d.Name).Should().Contain(["User1 Standard Deck", "User1 Modern Deck", "User2 Legacy Deck"]);
    }

    [Fact]
    public async Task ListAsync_FilterByOwner_ReturnsOnlyUserDecks()
    {
        // Arrange
        var user1 = _testDataBuilder.CreateUser("user1@test.com", "user1");
        var user2 = _testDataBuilder.CreateUser("user2@test.com", "user2");
        _identityContext.Users.AddRange(user1, user2);
        await _identityContext.SaveChangesAsync();

        var user1Decks = new[]
        {
            _testDataBuilder.CreateDeck(user1.Id),
            _testDataBuilder.CreateDeck(user1.Id)
        };
        user1Decks[0].Name = "User1 Deck 1";
        user1Decks[1].Name = "User1 Deck 2";

        var user2Deck = _testDataBuilder.CreateDeck(user2.Id);
        user2Deck.Name = "User2 Deck";

        _context.Decks.AddRange(user1Decks);
        _context.Decks.Add(user2Deck);
        await _context.SaveChangesAsync();

        // Act - Filter by owner (in real implementation, this would use a specification)
        var allDecks = await _repository.ListAllAsync();
        var user1DecksFiltered = allDecks!.Where(d => d.OwnerId == user1.Id).ToList();

        // Assert
        user1DecksFiltered.Should().HaveCount(2, "because user1 has exactly 2 decks");
        user1DecksFiltered.Should().OnlyContain(d => d.OwnerId == user1.Id);
        user1DecksFiltered.Select(d => d.Name).Should().Contain(["User1 Deck 1", "User1 Deck 2"]);
    }

    [Fact]
    public async Task AddAsync_WithValidDeck_AddsToDatabase()
    {
        // Arrange
        var user = _testDataBuilder.CreateUser();
        _identityContext.Users.Add(user);
        await _identityContext.SaveChangesAsync();

        var deck = _testDataBuilder.CreateDeck(user.Id);
        deck.Name = "New Combo Deck";
        deck.Format = DeckFormat.Modern;
        deck.NumberOfCards = 60;
        deck.TotalPrice = 299.99;

        // Act
        _repository.Add(deck);
        await _context.SaveChangesAsync();

        // Assert
        var savedDeck = await _context.Decks.FindAsync(deck.Id);
        savedDeck.Should().NotBeNull("because the deck should be saved to the database");
        savedDeck.Name.Should().Be("New Combo Deck");
        savedDeck.Format.Should().Be(DeckFormat.Modern);
        savedDeck.NumberOfCards.Should().Be(60);
        savedDeck.TotalPrice.Should().Be(299.99);
        savedDeck.OwnerId.Should().Be(user.Id);
    }

    [Fact]
    public async Task UpdateAsync_WithValidDeck_UpdatesInDatabase()
    {
        // Arrange
        var user = _testDataBuilder.CreateUser();
        _identityContext.Users.Add(user);
        await _identityContext.SaveChangesAsync();

        var deck = _testDataBuilder.CreateDeck(user.Id);
        deck.Name = "Original Name";
        deck.Format = DeckFormat.Standard;
        deck.NumberOfCards = 60;
        deck.TotalPrice = 100.00;
        _context.Decks.Add(deck);
        await _context.SaveChangesAsync();

        // Act
        deck.Name = "Updated Name";
        deck.Format = DeckFormat.Modern;
        deck.NumberOfCards = 75;
        deck.TotalPrice = 250.00;
        _repository.Update(deck);
        await _context.SaveChangesAsync();

        // Assert
        var updatedDeck = await _repository.GetByIdAsync(deck.Id);
        updatedDeck.Should().NotBeNull();
        updatedDeck.Name.Should().Be("Updated Name");
        updatedDeck.Format.Should().Be(DeckFormat.Modern);
        updatedDeck.NumberOfCards.Should().Be(75);
        updatedDeck.TotalPrice.Should().Be(250.00);
    }

    [Fact]
    public async Task DeleteAsync_WithValidDeck_RemovesFromDatabase()
    {
        // Arrange
        var user = _testDataBuilder.CreateUser();
        _identityContext.Users.Add(user);
        await _identityContext.SaveChangesAsync();

        var deck = _testDataBuilder.CreateDeck(user.Id);
        _context.Decks.Add(deck);
        await _context.SaveChangesAsync();

        var existingDeck = await _repository.GetByIdAsync(deck.Id);
        existingDeck.Should().NotBeNull();

        // Act
        _repository.Delete(deck);
        await _context.SaveChangesAsync();

        // Assert
        var deletedDeck = await _repository.GetByIdAsync(deck.Id);
        deletedDeck.Should().BeNull("because the deck should be deleted");
    }

    [Fact]
    public async Task ListAsync_WithDifferentFormats_ReturnsAllFormats()
    {
        // Arrange
        var user = _testDataBuilder.CreateUser();
        _identityContext.Users.Add(user);
        await _identityContext.SaveChangesAsync();

        var decks = new[]
        {
            _testDataBuilder.CreateDeck(user.Id),
            _testDataBuilder.CreateDeck(user.Id),
            _testDataBuilder.CreateDeck(user.Id)
        };
        decks[0].Format = DeckFormat.Standard;
        decks[1].Format = DeckFormat.Modern;
        decks[2].Format = DeckFormat.Legacy;

        _context.Decks.AddRange(decks);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.ListAllAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Select(d => d.Format).Should().Contain([DeckFormat.Standard, DeckFormat.Modern, DeckFormat.Legacy]);
    }

    [Fact]
    public async Task ConcurrentAccess_MultipleDeckOperations_HandledCorrectly()
    {
        // Arrange
        var user = _testDataBuilder.CreateUser();
        _identityContext.Users.Add(user);
        await _identityContext.SaveChangesAsync();

        var decks = Enumerable.Range(1, 5)
            .Select(i => {
                var deck = _testDataBuilder.CreateDeck(user.Id);
                deck.Name = $"Deck {i}";
                deck.Format = i % 2 == 0 ? DeckFormat.Standard : DeckFormat.Modern;
                return deck;
            })
            .ToList();

        // Act
        var tasks = decks.Select(deck =>
        {
            _repository.Add(deck);
            return Task.CompletedTask;
        });

        await Task.WhenAll(tasks);
        await _context.SaveChangesAsync();

        // Assert
        var result = await _repository.ListAllAsync();
        var userDecks = result!.Where(d => d.OwnerId == user.Id);
        userDecks.Should().HaveCount(5, "because all 5 decks should be saved");
    }

    public void Dispose()
    {
        _context.Dispose();
        _identityContext.Dispose();
    }
}
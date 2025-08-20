using Core.Models;
using Core.Specifications;
using FluentAssertions;
using Infrastructure.Data;
using Infrastructure.Identity;
using TestUtilities.Builders;
using TestUtilities.Database;

namespace UnitTests.Repositories;

/// <summary>
/// Tests for CollectionRepository focusing on data access patterns.
/// Uses in-memory database for fast, isolated testing.
/// </summary>
public class CollectionRepositoryTests : IDisposable
{
    private readonly MainContext _context;
    private readonly AppIdentityDbContext _identityContext;
    private readonly GenericRepository<Collection> _repository;
    private readonly TestDataBuilder _testDataBuilder;

    public CollectionRepositoryTests()
    {
        // Each test gets a fresh database context
        _context = InMemoryDbContextFactory.CreateMain();
        _identityContext = InMemoryDbContextFactory.CreateIdentity();
        _repository = new GenericRepository<Collection>(_context);
        _testDataBuilder = new TestDataBuilder();
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ReturnsCollection()
    {
        // Arrange - Set up test data
        var user = _testDataBuilder.CreateUser();
        _identityContext.Users.Add(user);
        await _context.SaveChangesAsync();

        var collection = _testDataBuilder.CreateCollection(user.Id);
        collection.Name = "Test Collection"; // Specific value for assertion
        _context.Collections.Add(collection);
        await _context.SaveChangesAsync();

        // Act - Execute the method under test
        var result = await _repository.GetByIdAsync(collection.Id);

        // Assert - Verify the results with detailed assertions
        result.Should().NotBeNull("because a collection with this ID exists");
        result!.Id.Should().Be(collection.Id, "because we requested this specific collection");
        result.Name.Should().Be("Test Collection", "because that's the name we set");
        result.OwnerId.Should().Be(user.Id, "because the collection belongs to this user");
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange - Empty database

        // Act
        var result = await _repository.GetByIdAsync(999999);

        // Assert
        result.Should().BeNull("because no collection exists with this ID");
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
        result.Should().BeNull($"because {invalidId} is not a valid collection ID");
    }

    [Fact]
    public async Task GetByUserIdAsync_WithValidUserId_ReturnsOnlyUserCollections()
    {
        // Arrange - Create multiple users with collections
        var user1 = _testDataBuilder.CreateUser("user1@test.com", "user1");
        var user2 = _testDataBuilder.CreateUser("user2@test.com", "user2");
        _identityContext.Users.AddRange(user1, user2);
        await _context.SaveChangesAsync();

        // Create collections for user1
        var user1Collections = new[]
        {
            _testDataBuilder.CreateCollection(user1.Id),
            _testDataBuilder.CreateCollection(user1.Id)
        };
        user1Collections[0].Name = "User1 Collection 1";
        user1Collections[1].Name = "User1 Collection 2";

        // Create collection for user2 (should not be returned)
        var user2Collection = _testDataBuilder.CreateCollection(user2.Id);
        user2Collection.Name = "User2 Collection";

        _context.Collections.AddRange(user1Collections);
        _context.Collections.Add(user2Collection);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.ListAsync(new CollectionWithOwnerSpecification(user1.Id));

        // Assert
        result.Should().HaveCount(2, "because user1 has exactly 2 collections");
        result.Should().OnlyContain(c => c.OwnerId == user1.Id, 
            "because we only want collections belonging to user1");
        result.Select(c => c.Name).Should().Contain(new[] { "User1 Collection 1", "User1 Collection 2" },
            "because these are the collections we created for user1");
    }

    [Fact]
    public async Task GetByUserIdAsync_WithNoCollections_ReturnsEmptyList()
    {
        // Arrange - Create user with no collections
        var user = _testDataBuilder.CreateUser();
        _identityContext.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.ListAsync(new CollectionWithOwnerSpecification(user.Id));

        // Assert
        result.Should().BeEmpty("because this user has no collections");
        result.Should().NotBeNull("because the method should return an empty list, not null");
    }

    [Fact]
    public async Task AddAsync_WithValidCollection_AddsToDatabase()
    {
        // Arrange
        var user = _testDataBuilder.CreateUser();
        _identityContext.Users.Add(user);
        await _context.SaveChangesAsync();

        var collection = _testDataBuilder.CreateCollection(user.Id);
        collection.Name = "New Collection";

        // Act
        _repository.Add(collection);
        await _context.SaveChangesAsync(); // Simulate UnitOfWork.SaveChangesAsync()

        // Assert - Verify the collection was actually saved
        var savedCollection = await _context.Collections.FindAsync(collection.Id);
        savedCollection.Should().NotBeNull("because the collection should be saved to the database");
        savedCollection!.Name.Should().Be("New Collection");
        savedCollection.OwnerId.Should().Be(user.Id);

        // Verify it's included in user collections
        var result = await _repository.ListAsync(new CollectionWithOwnerSpecification(user.Id));
        result.Should().ContainSingle(c => c.Id == collection.Id);
    }

    [Fact]
    public async Task UpdateAsync_WithValidCollection_UpdatesInDatabase()
    {
        // Arrange - Create and save initial collection
        var user = _testDataBuilder.CreateUser();
        _identityContext.Users.Add(user);
        await _context.SaveChangesAsync();

        var collection = _testDataBuilder.CreateCollection(user.Id);
        collection.Name = "Original Name";
        _context.Collections.Add(collection);
        await _context.SaveChangesAsync();

        // Act - Update the collection
        collection.Name = "Updated Name";
        
        _repository.Update(collection);
        await _context.SaveChangesAsync();

        // Assert - Verify changes were saved
        var updatedCollection = await _repository.GetByIdAsync(collection.Id);
        updatedCollection.Should().NotBeNull();
        updatedCollection!.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task DeleteAsync_WithValidCollection_RemovesFromDatabase()
    {
        // Arrange
        var user = _testDataBuilder.CreateUser();
        _identityContext.Users.Add(user);
        await _context.SaveChangesAsync();

        var collection = _testDataBuilder.CreateCollection(user.Id);
        _context.Collections.Add(collection);
        await _context.SaveChangesAsync();

        // Verify it exists
        var existingCollection = await _repository.GetByIdAsync(collection.Id);
        existingCollection.Should().NotBeNull();

        // Act
        _repository.Delete(collection);
        await _context.SaveChangesAsync();

        // Assert
        var deletedCollection = await _repository.GetByIdAsync(collection.Id);
        deletedCollection.Should().BeNull("because the collection should be deleted");
    }

    /// <summary>
    /// Tests concurrent access scenarios that might occur in production.
    /// </summary>
    [Fact]
    public async Task ConcurrentAccess_MultipleSaveOperations_HandledCorrectly()
    {
        // Arrange
        var user = _testDataBuilder.CreateUser();
        _identityContext.Users.Add(user);
        await _context.SaveChangesAsync();

        var collections = Enumerable.Range(1, 10)
            .Select(i => _testDataBuilder.CreateCollection(user.Id))
            .ToList();

        // Act - Simulate concurrent additions
        var tasks = collections.Select(async collection =>
        { 
            _repository.Add(collection);
        });

        await Task.WhenAll(tasks);
        await _context.SaveChangesAsync();

        // Assert
        var result = await _repository.ListAsync(new CollectionWithOwnerSpecification(user.Id));
        result.Should().HaveCount(10, "because all 10 collections should be saved");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

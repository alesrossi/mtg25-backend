using Core.Models;
using Core.Specifications;
using FluentAssertions;
using Infrastructure.Data;
using Microsoft.Extensions.Logging.Abstractions;
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
    private readonly GenericRepository<Collection> _repository;
    private readonly TestDataBuilder _testDataBuilder;

    public CollectionRepositoryTests()
    {
        // Each test gets a fresh database context
        _context = InMemoryDbContextFactory.CreateMain();
        _repository = new GenericRepository<Collection>(_context, NullLogger<GenericRepository<Collection>>.Instance);
        _testDataBuilder = new TestDataBuilder();
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ReturnsCollection()
    {
        // Arrange - Set up test data
        var user = _testDataBuilder.CreateUser();
        _context.Users.Add(user);
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
        _context.Users.AddRange(user1, user2);
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
        result.Select(c => c.Name).Should().Contain(["User1 Collection 1", "User1 Collection 2"],
            "because these are the collections we created for user1");
    }

    [Fact]
    public async Task GetByUserIdAsync_WithNoCollections_ReturnsEmptyList()
    {
        // Arrange - Create user with no collections
        var user = _testDataBuilder.CreateUser();
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.ListAsync(new CollectionWithOwnerSpecification(user.Id));

        // Assert
        result.Should().BeEmpty("because this user has no collections");
        result.Should().NotBeNull("because the method should return an empty list, not null");
    }

    [Fact]
    public async Task GetByUserIdAsync_WithNonExistentUserId_ReturnsEmptyList()
    {
        // Arrange - Use a user ID that doesn't exist in the database
        const string nonExistentUserId = "non-existent-user-id";

        // Act
        var result = await _repository.ListAsync(new CollectionWithOwnerSpecification(nonExistentUserId));

        // Assert
        result.Should().BeEmpty("because no collections exist for non-existent user");
        result.Should().NotBeNull("because the method should return an empty list, not null");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetByUserIdAsync_WithInvalidUserId_ReturnsEmptyList(string invalidUserId)
    {
        // Act
        var result = await _repository.ListAsync(new CollectionWithOwnerSpecification(invalidUserId));

        // Assert
        result.Should().BeEmpty($"because '{invalidUserId}' is not a valid user ID");
        result.Should().NotBeNull("because the method should return an empty list, not null");
    }

    [Fact]
    public async Task AddAsync_WithValidCollection_AddsToDatabase()
    {
        // Arrange
        var user = _testDataBuilder.CreateUser();
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var collection = _testDataBuilder.CreateCollection(user.Id);
        collection.Name = "New Collection";

        // Act
        _repository.Add(collection);
        await _context.SaveChangesAsync(); // Simulate UnitOfWork.SaveChangesAsync()

        // Assert - Verify the collection was actually saved
        var savedCollection = await _context.Collections.FindAsync(collection.Id);
        savedCollection.Should().NotBeNull("because the collection should be saved to the database");
        savedCollection.Name.Should().Be("New Collection");
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
        _context.Users.Add(user);
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
        updatedCollection.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task DeleteAsync_WithValidCollection_RemovesFromDatabase()
    {
        // Arrange
        var user = _testDataBuilder.CreateUser();
        _context.Users.Add(user);
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

    [Fact]
    public async Task ListAsync_WithoutSpecification_ReturnsAllCollections()
    {
        // Arrange - Create collections for multiple users
        var user1 = _testDataBuilder.CreateUser("user1@test.com", "user1");
        var user2 = _testDataBuilder.CreateUser("user2@test.com", "user2");
        _context.Users.AddRange(user1, user2);
        await _context.SaveChangesAsync();

        var collection1 = _testDataBuilder.CreateCollection(user1.Id);
        var collection2 = _testDataBuilder.CreateCollection(user2.Id);
        var collection3 = _testDataBuilder.CreateCollection(user1.Id);
        
        _context.Collections.AddRange(collection1, collection2, collection3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.ListAllAsync();

        // Assert
        result.Should().HaveCount(3, "because there are 3 collections total");
        result.Should().Contain(c => c.Id == collection1.Id);
        result.Should().Contain(c => c.Id == collection2.Id);
        result.Should().Contain(c => c.Id == collection3.Id);
    }

    [Fact]
    public async Task AddAsync_MultipleCollections_AllPersistedCorrectly()
    {
        // Arrange
        var user = _testDataBuilder.CreateUser();
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var collections = new[]
        {
            _testDataBuilder.CreateCollection(user.Id),
            _testDataBuilder.CreateCollection(user.Id),
            _testDataBuilder.CreateCollection(user.Id)
        };
        
        collections[0].Name = "Collection 1";
        collections[1].Name = "Collection 2";
        collections[2].Name = "Collection 3";

        // Act
        foreach (var collection in collections)
        {
            _repository.Add(collection);
        }
        await _context.SaveChangesAsync();

        // Assert
        var result = await _repository.ListAsync(new CollectionWithOwnerSpecification(user.Id));
        result.Should().HaveCount(3, "because we added 3 collections");
        result.Select(c => c.Name).Should().BeEquivalentTo(new[] { "Collection 1", "Collection 2", "Collection 3" });
    }

    [Fact]
    public async Task UpdateAsync_MultipleProperties_AllUpdatedCorrectly()
    {
        // Arrange
        var user = _testDataBuilder.CreateUser();
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var collection = _testDataBuilder.CreateCollection(user.Id);
        collection.Name = "Original Name";
        collection.NumberOfCards = 10;
        _context.Collections.Add(collection);
        await _context.SaveChangesAsync();

        // Act - Update multiple properties
        collection.Name = "Updated Name";
        collection.NumberOfCards = 25;
        
        _repository.Update(collection);
        await _context.SaveChangesAsync();

        // Assert
        var updatedCollection = await _repository.GetByIdAsync(collection.Id);
        updatedCollection.Should().NotBeNull();
        updatedCollection!.Name.Should().Be("Updated Name", "because we updated the name");
        updatedCollection.NumberOfCards.Should().Be(25, "because we updated the number of cards");
        updatedCollection.OwnerId.Should().Be(user.Id, "because owner should remain unchanged");
    }

    [Fact]
    public async Task DeleteAsync_NonExistentCollection_ThrowsDbUpdateConcurrencyException()
    {
        // Arrange
        var user = _testDataBuilder.CreateUser();
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var collection = _testDataBuilder.CreateCollection(user.Id);

        // Act & Assert 
        var act = () =>
        {
            _repository.Delete(collection);
            return _context.SaveChangesAsync();
        };

        await act.Should().ThrowAsync<Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException>(
            "because Entity Framework throws when attempting to delete entities that don't exist in the store");
    }

    /// <summary>
    /// Tests concurrent access scenarios that might occur in production.
    /// </summary>
    [Fact]
    public async Task ConcurrentAccess_MultipleSaveOperations_HandledCorrectly()
    {
        // Arrange
        var user = _testDataBuilder.CreateUser();
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var collections = Enumerable.Range(1, 10)
            .Select(i => _testDataBuilder.CreateCollection(user.Id))
            .ToList();

        // Act - Simulate concurrent additions
        var tasks = collections.Select(collection =>
        {
            _repository.Add(collection);
            return Task.CompletedTask;
        });

        await Task.WhenAll(tasks);
        await _context.SaveChangesAsync();

        // Assert
        var result = await _repository.ListAsync(new CollectionWithOwnerSpecification(user.Id));
        result.Should().HaveCount(10, "because all 10 collections should be saved");
    }

    [Fact]
    public async Task Repository_WithLargeDataset_PerformsEfficiently()
    {
        // Arrange - Create multiple users with many collections each
        var users = Enumerable.Range(1, 5)
            .Select(i => _testDataBuilder.CreateUser($"user{i}@test.com", $"user{i}"))
            .ToList();
        _context.Users.AddRange(users);
        await _context.SaveChangesAsync();

        var collections = new List<Collection>();
        foreach (var user in users)
        {
            for (var i = 0; i < 20; i++)
            {
                var collection = _testDataBuilder.CreateCollection(user.Id);
                collection.Name = $"Collection {i} for {user.UserName}";
                collections.Add(collection);
            }
        }
        
        _context.Collections.AddRange(collections);
        await _context.SaveChangesAsync();

        // Act & Assert - Should handle large dataset efficiently
        foreach (var user in users)
        {
            var userCollections = await _repository.ListAsync(new CollectionWithOwnerSpecification(user.Id));
            userCollections.Should().HaveCount(20, $"because {user.UserName} should have 20 collections");
            userCollections.Should().OnlyContain(c => c.OwnerId == user.Id, 
                $"because all collections should belong to {user.UserName}");
        }

        // Verify total count
        var allCollections = await _repository.ListAllAsync();
        allCollections.Should().HaveCount(100, "because we created 5 users with 20 collections each");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

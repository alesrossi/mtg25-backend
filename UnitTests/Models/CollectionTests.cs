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
        const string userId = "test-user-id";
        var collection = _testDataBuilder.CreateCollection(userId);

        // Assert
        collection.OwnerId.Should().Be(userId, "because collection should belong to the specified user");
        collection.NumberOfCards.Should().Be(0, "because new collections start with no cards");
        collection.Name.Should().NotBeNullOrEmpty("because collection name is required");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(100)]
    public void Collection_WithValidNumberOfCards_SetsCorrectly(int numberOfCards)
    {
        // Arrange
        const string userId = "test-user-id";
        var collection = _testDataBuilder.CreateCollection(userId);

        // Act
        collection.NumberOfCards = numberOfCards;

        // Assert
        collection.NumberOfCards.Should().Be(numberOfCards, "because number of cards should be set correctly");
    }

    [Fact]
    public void Collection_WithDifferentOwnerIds_CreatesDistinctCollections()
    {
        // Arrange
        const string userId1 = "user-1";
        const string userId2 = "user-2";

        // Act
        var collection1 = _testDataBuilder.CreateCollection(userId1);
        var collection2 = _testDataBuilder.CreateCollection(userId2);

        // Assert
        collection1.OwnerId.Should().Be(userId1, "because first collection belongs to user-1");
        collection2.OwnerId.Should().Be(userId2, "because second collection belongs to user-2");
    }

    [Fact]
    public void Collection_Properties_CanBeModifiedAfterCreation()
    {
        // Arrange
        const string userId = "test-user-id";
        var collection = _testDataBuilder.CreateCollection(userId);
        var originalName = collection.Name;
        var originalNumberOfCards = collection.NumberOfCards;

        // Act
        collection.Name = "Modified Collection Name";
        collection.NumberOfCards = 42;

        // Assert
        collection.Name.Should().Be("Modified Collection Name", "because name should be updateable");
        collection.NumberOfCards.Should().Be(42, "because number of cards should be updateable");
        collection.OwnerId.Should().Be(userId, "because owner ID should remain unchanged");
        collection.Name.Should().NotBe(originalName, "because we changed the name");
        collection.NumberOfCards.Should().NotBe(originalNumberOfCards, "because we changed the number of cards");
    }
}

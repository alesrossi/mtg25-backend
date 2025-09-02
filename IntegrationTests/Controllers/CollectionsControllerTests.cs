using System.Net;
using System.Text;
using System.Text.Json;
using API.Dtos.Cards;
using API.Dtos.Collections;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Core.Models;
using Core.Models.Identity;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using TestUtilities.Builders;

namespace IntegrationTests.Controllers;

/// <summary>
/// Integration tests for Collection endpoints.
/// Tests the complete request/response cycle including authentication,
/// database interactions, and business logic.
/// </summary>
[Collection("Integration Tests")]
public class CollectionsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly TestDataBuilder _testDataBuilder;

    public CollectionsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _testDataBuilder = new TestDataBuilder();
    }

    [Fact]
    public async Task GetCollections_WithAuthenticatedUser_ReturnsUserCollections()
    {
        // Arrange
        var user = await CreateTestUserAsync("testuser@example.com", "testuser");
        var collections = await CreateTestCollectionsForUserAsync(user.Id, 3);
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync("/api/collections");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "because authenticated users should be able to access their collections");

        var responseContent = await response.Content.ReadAsStringAsync();
        var returnedCollections = JsonSerializer.Deserialize<List<CollectionDto>>(
            responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        returnedCollections.Should().NotBeNull();
        returnedCollections!.Should().HaveCount(3, "because we created 3 collections for this user");
        returnedCollections.Should().Contain(c => c.Name.Contains("Collection"));
    }

    [Fact]
    public async Task GetCollections_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        using var client = _factory.CreateClient(); // No authentication

        // Act
        var response = await client.GetAsync("/api/collections");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "because unauthenticated requests should be rejected");
    }

    [Fact]
    public async Task GetCollections_WithEmptyCollections_ReturnsEmptyArray()
    {
        // Arrange
        var user = await CreateTestUserAsync("emptyuser@example.com", "emptyuser");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync("/api/collections");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        var collections = JsonSerializer.Deserialize<List<Collection>>(
            responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        collections.Should().NotBeNull();
        collections!.Should().BeEmpty("because this user has no collections");
    }

    [Fact]
    public async Task CreateCollection_WithValidData_CreatesCollection()
    {
        // Arrange
        var user = await CreateTestUserAsync("creator@example.com", "creator");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var createRequest = new NewCollectionDto
        {
            Name = "My New Collection",
            Color = "Red"
        };

        var json = JsonSerializer.Serialize(createRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/collections", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "because valid collection data should create a new collection");

        // Verify response contains created collection
        var responseContent = await response.Content.ReadAsStringAsync();
        var createdCollection = JsonSerializer.Deserialize<Collection>(
            responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        createdCollection.Should().NotBeNull();
        createdCollection!.Name.Should().Be(createRequest.Name);
        createdCollection.OwnerId.Should().Be(user.Id);
        createdCollection.Id.Should().BeGreaterThan(0);
        

        // Verify collection was actually saved to database
        await VerifyCollectionExistsInDatabase(createdCollection.Id, user.Id);
    }

    [Theory]
    [InlineData("", "Red")]
    [InlineData(" ", "Blue")]
    [InlineData(null, "Green")]
    public async Task CreateCollection_WithInvalidName_ReturnsBadRequest(string? invalidName, string color)
    {
        // Arrange
        var user = await CreateTestUserAsync("invaliduser@example.com", "invaliduser");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var createRequest = new NewCollectionDto
        {
            Name = invalidName!,
            Color = color
        };

        var json = JsonSerializer.Serialize(createRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/collections", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "because collection names cannot be empty or whitespace");
    }

    [Fact]
    public async Task CreateCollection_WithExtremelyLongName_ReturnsBadRequest()
    {
        // Arrange
        var user = await CreateTestUserAsync("longname@example.com", "longnameaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var createRequest = new NewCollectionDto
        {
            Name = new string('A', 1000), // Extremely long name
            Color = "Red"
        };

        var json = JsonSerializer.Serialize(createRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/collections", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "because collection names should have reasonable length limits");
    }

    [Fact]
    public async Task GetCollection_WithValidId_ReturnsCollection()
    {
        // Arrange
        var user = await CreateTestUserAsync("getter@example.com", "getter");
        var collection = await CreateTestCollectionAsync(user.Id, "Test Collection");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync($"/api/collections/{collection.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var responseContent = await response.Content.ReadAsStringAsync();
        var returnedCollection = JsonSerializer.Deserialize<Collection>(
            responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        returnedCollection.Should().NotBeNull();
        returnedCollection!.Id.Should().Be(collection.Id);
        returnedCollection.Name.Should().Be("Test Collection");
        returnedCollection.OwnerId.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetCollection_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var user = await CreateTestUserAsync("notfound@example.com", "notfound");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync("/api/collections/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "because no collection exists with this ID");
    }

    [Fact]
    public async Task GetCollection_WithOtherUserCollection_ReturnsForbidden()
    {
        // Arrange
        var user1 = await CreateTestUserAsync("user1@example.com", "user1");
        var user2 = await CreateTestUserAsync("user2@example.com", "user2");
        var user2Collection = await CreateTestCollectionAsync(user2.Id, "User2's Collection");

        // Try to access user2's collection as user1
        using var client = _factory.CreateClientWithUser(user1.Id, user1.UserName!, user1.Email!);

        // Act
        var response = await client.GetAsync($"/api/collections/{user2Collection.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "because users should not be able to access other users' collections");
    }
    
    #region Helper Methods

    private async Task<AppUser> CreateTestUserAsync(string baseEmail, string baseUserName)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        // Create unique identifiers for this test run
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var uniqueEmail = $"{baseEmail.Split('@')[0]}_{uniqueId}@{baseEmail.Split('@')[1]}";
        var uniqueUserName = $"{baseUserName}_{uniqueId}";

        var user = _testDataBuilder.CreateUser(uniqueEmail, uniqueUserName);
        var result = await userManager.CreateAsync(user);

        return !result.Succeeded ? throw new InvalidOperationException($"Failed to create test user: {string.Join(", ", result.Errors.Select(e => e.Description))}") : user;
    }




    private async Task<List<Collection>> CreateTestCollectionsForUserAsync(string userId, int count)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MainContext>();
        
        var collections = Enumerable.Range(1, count)
            .Select(i =>
            {
                var collection = _testDataBuilder.CreateCollection(userId);
                collection.Name = $"Test Collection {i}";
                collection.Color = $"Color {i}";
                return collection;
            })
            .ToList();
            
        dbContext.Collections.AddRange(collections);
        await dbContext.SaveChangesAsync();
        
        return collections;
    }

    private async Task<Collection> CreateTestCollectionAsync(string userId, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MainContext>();
        
        var collection = _testDataBuilder.CreateCollection(userId);
        collection.Name = name;
        collection.Color = "Red";
        
        dbContext.Collections.Add(collection);
        await dbContext.SaveChangesAsync();
        
        return collection;
    }

    private async Task VerifyCollectionExistsInDatabase(int collectionId, string expectedUserId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MainContext>();
        
        var collection = await dbContext.Collections.FindAsync(collectionId);
        collection.Should().NotBeNull($"because collection {collectionId} should exist in database");
        collection!.OwnerId.Should().Be(expectedUserId, "because collection should belong to the expected user");
    }

    private async Task VerifyCollectionInDatabase(int collectionId, string expectedName, string expectedColor)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MainContext>();
        
        var collection = await dbContext.Collections.FindAsync(collectionId);
        collection.Should().NotBeNull();
        collection!.Name.Should().Be(expectedName);
        collection.Color.Should().Be(expectedColor);
    }

    private async Task VerifyCollectionDeletedFromDatabase(int collectionId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MainContext>();
        
        var collection = await dbContext.Collections.FindAsync(collectionId);
        collection.Should().BeNull($"because collection {collectionId} should be deleted from database");
    }

    private async Task<List<Card>> CreateTestCardsForCollectionAsync(int collectionId, int count = 5)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MainContext>();
        
        var cards = new List<Card>();
        
        for (int i = 1; i <= count; i++)
        {
            var card = _testDataBuilder.CreateCard(collectionId);
            card.Name = $"Test Card {i}";
            card.SetName = i <= 2 ? "Alpha" : "Beta";
            card.SetCode = i <= 2 ? "LEA" : "LEB";
            card.Rarity = i % 2 == 0 ? "rare" : "common";
            card.Condition = (Condition)(i % 3);
            card.IsFoil = i % 2 == 0;
            card.PurchasePrice = i * 10.0;
            card.Quantity = i;
            cards.Add(card);
        }
        
        dbContext.Cards.AddRange(cards);
        await dbContext.SaveChangesAsync();
        
        return cards;
    }

    #endregion

    #region Card Filtering Tests

    [Fact]
    public async Task GetCardsFromCollection_WithoutFilters_ReturnsAllCards()
    {
        // Arrange
        var user = await CreateTestUserAsync("cardsuser@example.com", "cardsuser");
        var collection = await CreateTestCollectionAsync(user.Id, "Cards Test Collection");
        await CreateTestCardsForCollectionAsync(collection.Id);
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync($"/api/collections/{collection.Id}/cards?pageIndex=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<API.Helpers.Pagination<Card>>(
            responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        result.Should().NotBeNull();
        result!.Data.Should().HaveCount(5, "because we created 5 test cards");
    }

    [Fact]
    public async Task GetCardsFromCollection_WithSearchFilter_ReturnsMatchingCards()
    {
        // Arrange
        var user = await CreateTestUserAsync("searchuser@example.com", "searchuser");
        var collection = await CreateTestCollectionAsync(user.Id, "Search Test Collection");
        await CreateTestCardsForCollectionAsync(collection.Id);
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync($"/api/collections/{collection.Id}/cards?search=Test%20Card%201&pageIndex=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<API.Helpers.Pagination<Card>>(
            responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        result.Should().NotBeNull();
        result!.Data.Should().HaveCount(1);
        result.Data!.First().Name.Should().Be("Test Card 1");
    }

    [Fact]
    public async Task GetCardsFromCollection_WithSetCodeFilter_ReturnsMatchingCards()
    {
        // Arrange
        var user = await CreateTestUserAsync("setcodeuser@example.com", "setcodeuser");
        var collection = await CreateTestCollectionAsync(user.Id, "SetCode Test Collection");
        await CreateTestCardsForCollectionAsync(collection.Id);
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync($"/api/collections/{collection.Id}/cards?setCode=LEA&pageIndex=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<API.Helpers.Pagination<Card>>(
            responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        result.Should().NotBeNull();
        result!.Data.Should().HaveCount(2, "because 2 cards have LEA set code");
        result.Data!.Should().OnlyContain(c => c.SetCode == "LEA");
    }

    [Fact]
    public async Task GetCardsFromCollection_WithPriceSorting_ReturnsSortedCards()
    {
        // Arrange
        var user = await CreateTestUserAsync("sortuser@example.com", "sortuser");
        var collection = await CreateTestCollectionAsync(user.Id, "Sort Test Collection");
        await CreateTestCardsForCollectionAsync(collection.Id);
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync($"/api/collections/{collection.Id}/cards?sort=priceAsc&pageIndex=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<API.Helpers.Pagination<Card>>(
            responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        result.Should().NotBeNull();
        result!.Data.Should().NotBeEmpty();
        
        var prices = result.Data!.Select(c => c.PurchasePrice).ToList();
        prices.Should().BeInAscendingOrder("because sort=priceAsc was specified");
    }

    [Fact]
    public async Task GetCardsFromCollection_WithGroupBySetName_ReturnsGroupedCards()
    {
        // Arrange
        var user = await CreateTestUserAsync("groupuser@example.com", "groupuser");
        var collection = await CreateTestCollectionAsync(user.Id, "Group Test Collection");
        await CreateTestCardsForCollectionAsync(collection.Id);
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync($"/api/collections/{collection.Id}/cards?groupBy=setname&pageIndex=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<GroupedCardsPaginationDto>(
            responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        result.Should().NotBeNull();
        result!.Groups.Should().HaveCount(2, "because there are 2 different sets (Alpha/Beta)");
        result.TotalCards.Should().Be(5, "because there are 5 total cards");
        result.TotalGroups.Should().Be(2, "because there are 2 different sets");
    }

    #endregion
}

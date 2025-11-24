using System.Collections.Generic;
using System.Net;
using System.Net.Http;
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
using TestUtilities.Authentication;
using TestUtilities.Builders;
using System.Linq;

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
    public async Task MassDeleteCardsFromCollection_UpdatesNumberOfCards()
    {
        var user = await CreateTestUserAsync("massdelete@example.com", "massdelete");
        var collection = await CreateTestCollectionAsync(user.Id, "Mass Delete Collection");
        var cards = await CreateTestCardsForCollectionAsync(collection.Id, 3);
        var initialTotal = cards.Sum(card => card.Quantity);
        var cardsToRemove = cards.Take(2).ToList();
        var removedTotal = cardsToRemove.Sum(card => card.Quantity);

        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/collections/{collection.Id}/mass-delete")
        {
            Content = new StringContent(JsonSerializer.Serialize(cardsToRemove.Select(card => card.Id)), Encoding.UTF8, "application/json")
        };

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadAsStringAsync();
        var deletedCount = JsonSerializer.Deserialize<int>(payload);
        deletedCount.Should().Be(cardsToRemove.Count);

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<MainContext>();
        var updatedCollection = await verificationContext.Collections.FindAsync(collection.Id);

        updatedCollection.Should().NotBeNull();
        updatedCollection!.NumberOfCards.Should().Be(initialTotal - removedTotal);
    }

    [Fact]
    public async Task MassDeleteCardsFromCollection_WithEmptyRequest_ReturnsBadRequest()
    {
        var user = await CreateTestUserAsync("massdelete-empty@example.com", "massdelete_empty");
        var collection = await CreateTestCollectionAsync(user.Id, "Mass Delete Empty Collection");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/collections/{collection.Id}/mass-delete")
        {
            Content = new StringContent("[]", Encoding.UTF8, "application/json")
        };

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MassDeleteCardsFromCollection_WithNonOwner_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("massdelete-owner@example.com", "massdelete_owner");
        var intruder = await CreateTestUserAsync("massdelete-intruder@example.com", "massdelete_intruder");
        var collection = await CreateTestCollectionAsync(owner.Id, "Owner Collection");
        var cards = await CreateTestCardsForCollectionAsync(collection.Id, 2);

        using var client = _factory.CreateClientWithUser(intruder.Id, intruder.UserName!, intruder.Email!);
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/collections/{collection.Id}/mass-delete")
        {
            Content = new StringContent(JsonSerializer.Serialize(cards.Select(c => c.Id)), Encoding.UTF8, "application/json")
        };

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MassDeleteCardsFromCollection_WithInvalidCollection_ReturnsNotFound()
    {
        var user = await CreateTestUserAsync("massdelete-missing@example.com", "massdelete_missing");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/collections/{int.MaxValue}/mass-delete")
        {
            Content = new StringContent(JsonSerializer.Serialize(new[] { 1, 2 }), Encoding.UTF8, "application/json")
        };

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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

    [Fact]
    public async Task CreateCollection_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var createRequest = new NewCollectionDto
        {
            Name = "Unauthorized Collection",
            Color = "Blue"
        };

        var response = await client.PostAsync("/api/collections",
            new StringContent(JsonSerializer.Serialize(createRequest), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
    public async Task UpdateCollection_WithValidData_ReturnsUpdatedCollection()
    {
        var owner = await CreateTestUserAsync("collection-update-owner@example.com", "collection_update_owner");
        var collection = await CreateTestCollectionAsync(owner.Id, "Original Name");
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var updateRequest = new NewCollectionDto
        {
            Name = "Updated Name",
            Color = "Blue"
        };

        var response = await client.PutAsync($"/api/collections/{collection.Id}",
            new StringContent(JsonSerializer.Serialize(updateRequest), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadAsStringAsync();
        var updatedCollection = JsonSerializer.Deserialize<Collection>(payload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        updatedCollection.Should().NotBeNull();
        updatedCollection!.Name.Should().Be(updateRequest.Name);
        updatedCollection.Color.Should().Be(updateRequest.Color);
    }

    [Fact]
    public async Task UpdateCollection_WithInvalidData_ReturnsBadRequest()
    {
        var owner = await CreateTestUserAsync("collection-update-invalid@example.com", "collection_update_invalid");
        var collection = await CreateTestCollectionAsync(owner.Id, "Original Name");
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var updateRequest = new NewCollectionDto
        {
            Name = string.Empty,
            Color = "Green"
        };

        var response = await client.PutAsync($"/api/collections/{collection.Id}",
            new StringContent(JsonSerializer.Serialize(updateRequest), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateCollection_WithUnauthorizedUser_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("collection-update-owner2@example.com", "collection_update_owner2");
        var intruder = await CreateTestUserAsync("collection-update-intruder@example.com", "collection_update_intruder");
        var collection = await CreateTestCollectionAsync(owner.Id, "Owner Collection");
        using var client = _factory.CreateClientWithUser(intruder.Id, intruder.UserName!, intruder.Email!);

        var updateRequest = new NewCollectionDto { Name = "Hacked", Color = "Black" };
        var response = await client.PutAsync($"/api/collections/{collection.Id}",
            new StringContent(JsonSerializer.Serialize(updateRequest), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateCollection_WithInvalidId_ReturnsNotFound()
    {
        var owner = await CreateTestUserAsync("collection-update-missing@example.com", "collection_update_missing");
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var updateRequest = new NewCollectionDto { Name = "Missing", Color = "Purple" };
        var response = await client.PutAsync($"/api/collections/{int.MaxValue}",
            new StringContent(JsonSerializer.Serialize(updateRequest), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateCollection_WithoutAuthentication_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("collection-update-noauth@example.com", "collection_update_noauth");
        var collection = await CreateTestCollectionAsync(owner.Id, "Original Name");
        using var client = _factory.CreateClient();

        var updateRequest = new NewCollectionDto { Name = "No Auth", Color = "White" };
        var response = await client.PutAsync($"/api/collections/{collection.Id}",
            new StringContent(JsonSerializer.Serialize(updateRequest), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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

    [Fact]
    public async Task DeleteCollection_WithValidId_RemovesCollection()
    {
        var owner = await CreateTestUserAsync("collection-delete-owner@example.com", "collection_delete_owner");
        var collection = await CreateTestCollectionAsync(owner.Id, "Delete Me");
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var response = await client.DeleteAsync($"/api/collections/{collection.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await VerifyCollectionDeletedFromDatabase(collection.Id);
    }

    [Fact]
    public async Task DeleteCollection_WithNonOwner_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("collection-delete-owner2@example.com", "collection_delete_owner2");
        var intruder = await CreateTestUserAsync("collection-delete-intruder@example.com", "collection_delete_intruder");
        var collection = await CreateTestCollectionAsync(owner.Id, "Keep Out");

        using var client = _factory.CreateClientWithUser(intruder.Id, intruder.UserName!, intruder.Email!);
        var response = await client.DeleteAsync($"/api/collections/{collection.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await VerifyCollectionExistsInDatabase(collection.Id, owner.Id);
    }

    [Fact]
    public async Task DeleteCollection_WithInvalidId_ReturnsNotFound()
    {
        var owner = await CreateTestUserAsync("collection-delete-missing@example.com", "collection_delete_missing");
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var response = await client.DeleteAsync($"/api/collections/{int.MaxValue}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteCollection_WithoutAuthentication_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("collection-delete-noauth@example.com", "collection_delete_noauth");
        var collection = await CreateTestCollectionAsync(owner.Id, "NoAuth Delete");

        using var client = _factory.CreateClient();
        var response = await client.DeleteAsync($"/api/collections/{collection.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
    
    #region Helper Methods

    private Task<AppUser> CreateTestUserAsync(string baseEmail, string baseUserName) =>
        TestUserFactory.CreateAsync(_factory.Services, _testDataBuilder, baseEmail, baseUserName);




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
        
        var collection = await dbContext.Collections.FindAsync(collectionId) ??
            throw new InvalidOperationException($"Collection {collectionId} not found for test setup.");

        dbContext.Cards.AddRange(cards);
        collection.NumberOfCards += cards.Sum(card => card.Quantity);
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
    public async Task GetCardsFromCollection_WithInvalidCollectionId_ReturnsNotFound()
    {
        var user = await CreateTestUserAsync("collectioncards-missing@example.com", "collectioncards_missing");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var response = await client.GetAsync($"/api/collections/{int.MaxValue}/cards?pageIndex=1&pageSize=5");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetCardsFromCollection_WithOtherUser_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("collectioncards-owner@example.com", "collectioncards_owner");
        var intruder = await CreateTestUserAsync("collectioncards-intruder@example.com", "collectioncards_intruder");
        var collection = await CreateTestCollectionAsync(owner.Id, "Owner Cards Collection");
        await CreateTestCardsForCollectionAsync(collection.Id);

        using var client = _factory.CreateClientWithUser(intruder.Id, intruder.UserName!, intruder.Email!);
        var response = await client.GetAsync($"/api/collections/{collection.Id}/cards?pageIndex=1&pageSize=5");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCardsFromCollection_WithoutAuthentication_ReturnsUnauthorized()
    {
        var user = await CreateTestUserAsync("collectioncards-noauth@example.com", "collectioncards_noauth");
        var collection = await CreateTestCollectionAsync(user.Id, "NoAuth Cards Collection");
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/collections/{collection.Id}/cards?pageIndex=1&pageSize=5");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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

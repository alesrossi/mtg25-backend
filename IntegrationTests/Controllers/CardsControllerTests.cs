using System.Net;
using System.Text;
using System.Text.Json;
using API.Dtos.Cards;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Core.Models;
using Core.Models.Identity;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using TestUtilities.Builders;

namespace IntegrationTests.Controllers;

/// <summary>
/// Integration tests for Card endpoints.
/// Tests the complete request/response cycle including authentication,
/// database interactions, and business logic.
/// </summary>
[Collection("Integration Tests")]
public class CardsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly TestDataBuilder _testDataBuilder;

    public CardsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _testDataBuilder = new TestDataBuilder();
    }

    [Fact]
    public async Task GetCard_WithValidId_ReturnsCard()
    {
        // Arrange
        var user = await CreateTestUserAsync("cardget@example.com", "cardget");
        var collection = await CreateTestCollectionAsync(user.Id, "Test Collection");
        var card = await CreateTestCardAsync(collection.Id, "Lightning Bolt");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync($"/api/cards/{card.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "because authenticated users should be able to access their own cards");

        var responseContent = await response.Content.ReadAsStringAsync();
        var returnedCard = JsonSerializer.Deserialize<Card>(
            responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        returnedCard.Should().NotBeNull();
        returnedCard!.Id.Should().Be(card.Id);
        returnedCard.Name.Should().Be("Lightning Bolt");
        returnedCard.CollectionId.Should().Be(collection.Id);
    }

    [Fact]
    public async Task GetCard_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var user = await CreateTestUserAsync("notfound@example.com", "notfound");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync("/api/cards/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "because no card exists with this ID");
    }

    [Fact]
    public async Task GetCard_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        using var client = _factory.CreateClient(); // No authentication

        // Act
        var response = await client.GetAsync("/api/cards/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "because unauthenticated requests should be rejected");
    }

    [Fact]
    public async Task GetCard_WithOtherUserCard_ReturnsUnauthorized()
    {
        // Arrange
        var user1 = await CreateTestUserAsync("user1@example.com", "user1");
        var user2 = await CreateTestUserAsync("user2@example.com", "user2");
        var user2Collection = await CreateTestCollectionAsync(user2.Id, "User2's Collection");
        var user2Card = await CreateTestCardAsync(user2Collection.Id, "User2's Card");

        // Try to access user2's card as user1
        using var client = _factory.CreateClientWithUser(user1.Id, user1.UserName!, user1.Email!);

        // Act
        var response = await client.GetAsync($"/api/cards/{user2Card.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "because users should not be able to access other users' cards");
    }

    [Fact]
    public async Task DeleteCard_WithValidId_DeletesCard()
    {
        // Arrange
        var user = await CreateTestUserAsync("deleter@example.com", "deleter");
        var collection = await CreateTestCollectionAsync(user.Id, "Delete Collection");
        var card = await CreateTestCardAsync(collection.Id, "To Be Deleted");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.DeleteAsync($"/api/cards/{card.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "because authenticated users should be able to delete their own cards");

        // Verify card was actually deleted
        await VerifyCardDeletedFromDatabase(card.Id);

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<MainContext>();
        var updatedCollection = await verificationContext.Collections.FindAsync(collection.Id);

        updatedCollection.Should().NotBeNull();
        updatedCollection!.NumberOfCards.Should().Be(0);
    }

    [Fact]
    public async Task DeleteCard_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var user = await CreateTestUserAsync("deletebad@example.com", "deletebad");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.DeleteAsync("/api/cards/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "because no card exists with this ID");
    }

    [Fact]
    public async Task DeleteCard_WithOtherUserCard_ReturnsUnauthorized()
    {
        // Arrange
        var user1 = await CreateTestUserAsync("deluser1@example.com", "deluser1");
        var user2 = await CreateTestUserAsync("deluser2@example.com", "deluser2");
        var user2Collection = await CreateTestCollectionAsync(user2.Id, "User2's Collection");
        var user2Card = await CreateTestCardAsync(user2Collection.Id, "User2's Card");

        // Try to delete user2's card as user1
        using var client = _factory.CreateClientWithUser(user1.Id, user1.UserName!, user1.Email!);

        // Act
        var response = await client.DeleteAsync($"/api/cards/{user2Card.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "because users should not be able to delete other users' cards");

        // Verify card still exists
        await VerifyCardExistsInDatabase(user2Card.Id);
    }

    [Fact]
    public async Task SearchCards_WithValidQuery_ReturnsMatchingCards()
    {
        // Arrange
        var user = await CreateTestUserAsync("searcher@example.com", "searcher");
        var collection = await CreateTestCollectionAsync(user.Id, "Search Collection");
        await CreateTestCardAsync(collection.Id, "Lightning Bolt");
        await CreateTestCardAsync(collection.Id, "Lightning Strike");
        await CreateTestCardAsync(collection.Id, "Black Lotus");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync("/api/cards/search/Lightning");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "because authenticated users should be able to search cards");

        var responseContent = await response.Content.ReadAsStringAsync();
        var cards = JsonSerializer.Deserialize<List<MinimalCardDto>>(
            responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        cards.Should().NotBeNull();
        cards.Should().HaveCountGreaterThan(0, "because there should be cards matching 'Lightning'");
    }

    [Fact]
    public async Task AddNewCard_WithValidData_CreatesCard()
    {
        // Arrange
        var user = await CreateTestUserAsync("cardadder@example.com", "cardadder");
        var collection = await CreateTestCollectionAsync(user.Id, "Add Collection");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var cardRequest = new
        {
            OracleId = "97398ad2-675b-4a34-aab7-935dd6714f1c",
            CollectionId = collection.Id,
            Quantity = 2,
            Language = "en",
            Version = "dmc",
            Condition = "NearMint",
            IsFoil = false,
            PurchasePrice = 1.50,
            PurchasePriceCurrency = "USD",
            IsMisprint = false,
            IsAltered = false
        };

        var json = JsonSerializer.Serialize(cardRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/cards", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, "because valid card data should create a new card");

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<MainContext>();
        var updatedCollection = await verificationContext.Collections.FindAsync(collection.Id);

        updatedCollection.Should().NotBeNull();
        updatedCollection!.NumberOfCards.Should().Be(cardRequest.Quantity);
    }
    
    // [Fact]
    // public async Task AddCardList_WithValidCardNames_ReturnsOracleData()
    // {
    //     // Arrange
    //     var user = await CreateTestUserAsync("listadder@example.com", "listadder");
    //     using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);
    //
    //     var cardListRequest = new CardListDto
    //     {
    //         CardNames = new List<string> { "Lightning Bolt", "Black Lotus", "Ancestral Recall" }
    //     };
    //
    //     var json = JsonSerializer.Serialize(cardListRequest);
    //     var content = new StringContent(json, Encoding.UTF8, "application/json");
    //
    //     // Act
    //     var response = await client.PostAsync("/api/cards/card-list", content);
    //
    //     // Assert
    //     response.StatusCode.Should().Be(HttpStatusCode.OK,
    //         "because valid card names should return oracle data");
    //
    //     var responseContent = await response.Content.ReadAsStringAsync();
    //     var oracleCards = JsonSerializer.Deserialize<LinkedList<OracleCardDto>>(
    //         responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    //
    //     oracleCards.Should().NotBeNull();
    //     oracleCards!.Should().HaveCount(3, "because we requested 3 cards");
    // }

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

    private async Task<Collection> CreateTestCollectionAsync(string userId, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MainContext>();
        
        // Create collection directly without AutoFixture to avoid random data generation
        var collection = new Collection
        {
            OwnerId = userId,
            Name = name,
            Color = "Red",
            NumberOfCards = 0,
            TotalPrice = 0.0
        };
        
        dbContext.Collections.Add(collection);
        await dbContext.SaveChangesAsync();
        
        return collection;
    }

    private async Task<Card> CreateTestCardAsync(int collectionId, string name, int quantity = 1)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MainContext>();
        
        // Create card directly without AutoFixture to avoid random data generation
        var card = new Card
        {
            CollectionId = collectionId,
            Name = name,
            OracleId = Guid.NewGuid()
                .ToString(),
            Quantity = quantity,
            Language = "English",
            Version = "Original",
            Condition = Condition.NearMint,
            IsFoil = false,
            PurchasePrice = 1.0,
            PurchasePriceCurrency = "USD",
            ImageUrl = "https://example.com/card.jpg",
            BackImageUrl = null,
            SetCode = "TST",
            SetName = "Test Set",
            CollectorNumber = "001",
            Rarity = "Common",
            IsMisprint = false,
            IsAltered = false,
            ArtCrop = "https://example.com/card.jpg"
        };
        
        var collection = await dbContext.Collections.FindAsync(collectionId) ??
            throw new InvalidOperationException($"Collection {collectionId} not found for test setup.");

        dbContext.Cards.Add(card);
        collection.NumberOfCards += card.Quantity;
        await dbContext.SaveChangesAsync();
        
        return card;
    }

    private async Task VerifyCardExistsInDatabase(int cardId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MainContext>();
        
        var card = await dbContext.Cards.FindAsync(cardId);
        card.Should().NotBeNull($"because card {cardId} should exist in database");
    }

    private async Task VerifyCardDeletedFromDatabase(int cardId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MainContext>();
        
        var card = await dbContext.Cards.FindAsync(cardId);
        card.Should().BeNull($"because card {cardId} should be deleted from database");
    }

    #endregion
}

using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.Dtos;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Core.Models;
using Core.Models.Identity;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using TestUtilities.Builders;

namespace IntegrationTests.Controllers;

[Collection("Integration Tests")]
public class DeckCardsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly TestDataBuilder _testDataBuilder;

    public DeckCardsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _testDataBuilder = new TestDataBuilder();
    }

    [Fact]
    public async Task GetDeckCards_WithAuthenticatedUser_ReturnsDeckCards()
    {
        // Arrange
        var user = await CreateTestUserAsync("testuser@example.com", "testuser");
        var deck = await CreateTestDeckForUserAsync(user.Id);
        var deckCards = await CreateTestDeckCardsForDeckAsync(deck.Id, 3);
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync($"/api/decks/{deck.Id}/cards");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        
        // Debug: Check actual response
        Console.WriteLine($"Response: {responseContent}");
        
        var returnedDeckCards = JsonSerializer.Deserialize<List<DeckCardDto>>(
            responseContent, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase, 
                PropertyNameCaseInsensitive = true,
                ReferenceHandler = ReferenceHandler.Preserve
            });

        returnedDeckCards.Should().HaveCount(3);
        returnedDeckCards!.All(dc => dc.DeckId == deck.Id).Should().BeTrue();
    }

    [Fact]
    public async Task GetDeckCards_WithMaindeckOnly_ReturnsOnlyMaindeckCards()
    {
        // Arrange
        var user = await CreateTestUserAsync("testuser@example.com", "testuser");
        var deck = await CreateTestDeckForUserAsync(user.Id);
        await CreateMixedDeckCardsAsync(deck.Id);
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync($"/api/decks/{deck.Id}/cards?maindeckOnly=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        var returnedDeckCards = JsonSerializer.Deserialize<List<DeckCardDto>>(
            responseContent, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase, 
                PropertyNameCaseInsensitive = true,
                ReferenceHandler = ReferenceHandler.Preserve
            });

        returnedDeckCards!.All(dc => dc.MaindeckQuantity > 0).Should().BeTrue();
    }

    [Fact]
    public async Task GetDeckCardById_WithValidId_ReturnsDeckCard()
    {
        // Arrange
        var user = await CreateTestUserAsync("testuser@example.com", "testuser");
        var deck = await CreateTestDeckForUserAsync(user.Id);
        var deckCards = await CreateTestDeckCardsForDeckAsync(deck.Id, 1);
        var deckCard = deckCards.First();
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync($"/api/decks/{deck.Id}/cards/{deckCard.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        var returnedDeckCard = JsonSerializer.Deserialize<DeckCardDto>(
            responseContent, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true });

        returnedDeckCard!.Id.Should().Be(deckCard.Id);
        returnedDeckCard.DeckId.Should().Be(deck.Id);
    }

    [Fact]
    public async Task CreateDeckCard_WithValidData_CreatesDeckCard()
    {
        // Arrange
        var user = await CreateTestUserAsync("testuser@example.com", "testuser");
        var deck = await CreateTestDeckForUserAsync(user.Id);
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var createDto = new CreateDeckCardDto
        {
            OracleId = "oracle-123",
            Name = "Lightning Bolt",
            SetCode = "LEA",
            SetName = "Limited Edition Alpha",
            MaindeckQuantity = 4,
            SideboardQuantity = 0,
            ImageUrl = "TEST"
        };

        var json = JsonSerializer.Serialize(createDto, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true 
        });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync($"/api/decks/{deck.Id}/cards", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var responseContent = await response.Content.ReadAsStringAsync();
        var createdDeckCard = JsonSerializer.Deserialize<DeckCardDto>(
            responseContent, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true });

        createdDeckCard!.DeckId.Should().Be(deck.Id);
        createdDeckCard.Name.Should().Be("Lightning Bolt");
        createdDeckCard.MaindeckQuantity.Should().Be(4);
    }

    [Fact]
    public async Task UpdateDeckCard_WithValidData_UpdatesDeckCard()
    {
        // Arrange
        var user = await CreateTestUserAsync("testuser@example.com", "testuser");
        var deck = await CreateTestDeckForUserAsync(user.Id);
        var deckCards = await CreateTestDeckCardsForDeckAsync(deck.Id, 1);
        var deckCard = deckCards.First();
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var updateDto = new UpdateDeckCardDto
        {
            MaindeckQuantity = 2,
            SideboardQuantity = 2
        };

        var json = JsonSerializer.Serialize(updateDto, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true 
        });
        
        // Debug: Check the JSON being sent
        Console.WriteLine($"Update JSON: {json}");
        
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PutAsync($"/api/decks/{deck.Id}/cards/{deckCard.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        var updatedDeckCard = JsonSerializer.Deserialize<DeckCardDto>(
            responseContent, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true });

        updatedDeckCard!.MaindeckQuantity.Should().Be(2);
        updatedDeckCard.SideboardQuantity.Should().Be(2);
    }

    [Fact]
    public async Task DeleteDeckCard_WithValidId_DeletesDeckCard()
    {
        // Arrange
        var user = await CreateTestUserAsync("testuser@example.com", "testuser");
        var deck = await CreateTestDeckForUserAsync(user.Id);
        var deckCards = await CreateTestDeckCardsForDeckAsync(deck.Id, 1);
        var deckCard = deckCards.First();
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.DeleteAsync($"/api/decks/{deck.Id}/cards/{deckCard.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deletion
        var getResponse = await client.GetAsync($"/api/decks/{deck.Id}/cards/{deckCard.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetDeckCards_FromOtherUsersDeck_ReturnsForbidden()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com", "owner");
        var otherUser = await CreateTestUserAsync("other@example.com", "other");
        var deck = await CreateTestDeckForUserAsync(owner.Id);
        using var client = _factory.CreateClientWithUser(otherUser.Id, otherUser.UserName!, otherUser.Email!);

        // Act
        var response = await client.GetAsync($"/api/decks/{deck.Id}/cards");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetDeckCards_ConcurrentRequests_ShouldNotCauseDbContextIssues()
    {
        // Arrange
        var user = await CreateTestUserAsync("concurrent@example.com", "concurrent");
        var deck = await CreateTestDeckForUserAsync(user.Id);
        var collection = await CreateCollectionWithOwnedCards(user.Id);
        
        // Create some deck cards and owned cards
        var deckCards = await CreateTestDeckCardsForDeckAsync(deck.Id, 5);
        await CreateOwnedCardsForDeckCards(collection.Id, deckCards.Take(3).ToList());
        
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        // Act - Make 10 concurrent requests to the same endpoint
        var tasks = Enumerable.Range(0, 10).Select(_ => 
            client.GetAsync($"/api/decks/{deck.Id}/cards")
        ).ToArray();

        var responses = await Task.WhenAll(tasks);

        // Assert - All responses should be successful
        foreach (var response in responses)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            var returnedDeckCards = JsonSerializer.Deserialize<List<DeckCardDto>>(
                responseContent, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase, 
                    PropertyNameCaseInsensitive = true,
                    ReferenceHandler = ReferenceHandler.IgnoreCycles
                });

            // Verify the response structure
            returnedDeckCards.Should().HaveCount(5);
            returnedDeckCards!.All(dc => dc.DeckId == deck.Id).Should().BeTrue();
            
            // Verify ownership detection is working
            var ownedCards = returnedDeckCards.Where(dc => dc.IsOwned).ToList();
            ownedCards.Should().HaveCount(3); // We created owned cards for 3 deck cards
            ownedCards.All(dc => dc.OwnedQuantity > 0).Should().BeTrue();
        }
    }

    [Fact]
    public async Task GetDeckCards_SequentialRequests_ShouldReturnConsistentResults()
    {
        // Arrange
        var user = await CreateTestUserAsync("sequential@example.com", "sequential");
        var deck = await CreateTestDeckForUserAsync(user.Id);
        var collection = await CreateCollectionWithOwnedCards(user.Id);
        
        var deckCards = await CreateTestDeckCardsForDeckAsync(deck.Id, 3);
        await CreateOwnedCardsForDeckCards(collection.Id, deckCards);
        
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        // Act - Make 5 sequential requests
        var responses = new List<List<DeckCardDto>>();
        for (int i = 0; i < 5; i++)
        {
            var response = await client.GetAsync($"/api/decks/{deck.Id}/cards");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            var returnedDeckCards = JsonSerializer.Deserialize<List<DeckCardDto>>(
                responseContent, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase, 
                    PropertyNameCaseInsensitive = true,
                    ReferenceHandler = ReferenceHandler.IgnoreCycles
                });
                
            responses.Add(returnedDeckCards!);
        }

        // Assert - All responses should be identical
        var firstResponse = responses[0];
        foreach (var response in responses.Skip(1))
        {
            response.Should().HaveCount(firstResponse.Count);
            for (int i = 0; i < firstResponse.Count; i++)
            {
                response[i].Id.Should().Be(firstResponse[i].Id);
                response[i].OwnedQuantity.Should().Be(firstResponse[i].OwnedQuantity);
                response[i].IsOwned.Should().Be(firstResponse[i].IsOwned);
                response[i].OwnershipStatus.Should().Be(firstResponse[i].OwnershipStatus);
            }
        }
    }

    private async Task<AppUser> CreateTestUserAsync(string email, string userName)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = _testDataBuilder.CreateUser(email, userName);
        await userManager.CreateAsync(user, "Password123!");
        return user;
    }

    private async Task<Deck> CreateTestDeckForUserAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MainContext>();
        var deck = _testDataBuilder.CreateDeck(userId);
        deck.Name = "Test Deck";
        context.Decks.Add(deck);
        await context.SaveChangesAsync();
        return deck;
    }

    private async Task<List<DeckCard>> CreateTestDeckCardsForDeckAsync(int deckId, int count)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MainContext>();
        var deckCards = new List<DeckCard>();

        for (int i = 0; i < count; i++)
        {
            var deckCard = new DeckCard
            {
                DeckId = deckId,
                OracleId = $"oracle-{i}",
                Name = $"Test Card {i}",
                SetCode = "LEA",
                MaindeckQuantity = 4,
                SideboardQuantity = 0
            };
            deckCards.Add(deckCard);
            context.DeckCards.Add(deckCard);
        }

        await context.SaveChangesAsync();
        return deckCards;
    }

    private async Task CreateMixedDeckCardsAsync(int deckId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MainContext>();

        var maindeckCard = new DeckCard
        {
            DeckId = deckId,
            OracleId = "oracle-main",
            Name = "Maindeck Card",
            SetCode = "LEA",
            MaindeckQuantity = 4,
            SideboardQuantity = 0
        };

        var sideboardCard = new DeckCard
        {
            DeckId = deckId,
            OracleId = "oracle-side",
            Name = "Sideboard Card",
            SetCode = "ICE",
            MaindeckQuantity = 0,
            SideboardQuantity = 2
        };

        context.DeckCards.AddRange(maindeckCard, sideboardCard);
        await context.SaveChangesAsync();
    }

    private async Task<Collection> CreateCollectionWithOwnedCards(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MainContext>();
        var collection = _testDataBuilder.CreateCollection(userId);
        collection.Name = "Test Collection";
        collection.Color = "Blue";
        context.Collections.Add(collection);
        await context.SaveChangesAsync();
        return collection;
    }

    private async Task CreateOwnedCardsForDeckCards(int collectionId, List<DeckCard> deckCards)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MainContext>();

        foreach (var deckCard in deckCards)
        {
            var ownedCard = _testDataBuilder.CreateCardWithOracleId(
                collectionId, 
                deckCard.OracleId, 
                deckCard.Name, 
                2); // Set owned quantity to 2
            context.Cards.Add(ownedCard);
        }

        await context.SaveChangesAsync();
    }
}
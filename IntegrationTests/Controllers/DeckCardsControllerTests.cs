using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.Dtos.Decks;
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
        
        var returnedDeckCards = DeserializeDeckCardList(responseContent);

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
        var returnedDeckCards = DeserializeDeckCardList(responseContent);

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
        var returnedDeckCard = DeserializeDeckCard(responseContent);

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

        var content = SerializeToJson(createDto);

        // Act
        var response = await client.PostAsync($"/api/decks/{deck.Id}/cards", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var responseContent = await response.Content.ReadAsStringAsync();
        var createdDeckCard = DeserializeDeckCard(responseContent);

        createdDeckCard!.DeckId.Should().Be(deck.Id);
        createdDeckCard.Name.Should().Be("Lightning Bolt");
        createdDeckCard.MaindeckQuantity.Should().Be(4);
    }

    [Fact]
    public async Task CreateDeckCard_WithMissingRequiredFields_ReturnsValidationErrors()
    {
        // Arrange
        var user = await CreateTestUserAsync("validationuser@example.com", "validationuser");
        var deck = await CreateTestDeckForUserAsync(user.Id);
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var createDto = new CreateDeckCardDto
        {
            OracleId = string.Empty,
            Name = "",
            SetCode = "LEA",
            ImageUrl = "https://example.com/card.png",
            MaindeckQuantity = 1,
            SideboardQuantity = 0
        };

        var content = SerializeToJson(createDto);

        // Act
        var response = await client.PostAsync($"/api/decks/{deck.Id}/cards", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var responseContent = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(responseContent);
        var errorsElement = json.RootElement.GetProperty("errors");

        errorsElement.TryGetProperty("OracleId", out var oracleErrors).Should().BeTrue();
        oracleErrors[0].GetString().Should().Be("Oracle ID is required.");

        errorsElement.TryGetProperty("Name", out var nameErrors).Should().BeTrue();
        nameErrors[0].GetString().Should().Be("Card name is required.");
    }

    [Fact]
    public async Task CreateDeckCard_WithZeroQuantities_ReturnsValidationError()
    {
        // Arrange
        var user = await CreateTestUserAsync("quantityuser@example.com", "quantityuser");
        var deck = await CreateTestDeckForUserAsync(user.Id);
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var createDto = new CreateDeckCardDto
        {
            OracleId = "97398ad2-675b-4a34-aab7-935dd6714f1c",
            Name = "Lightning Bolt",
            SetCode = "LEA",
            ImageUrl = "https://example.com/card.png",
            MaindeckQuantity = 0,
            SideboardQuantity = 0
        };

        var content = SerializeToJson(createDto);

        // Act
        var response = await client.PostAsync($"/api/decks/{deck.Id}/cards", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var responseContent = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(responseContent);
        var errorsElement = json.RootElement.GetProperty("errors");

        errorsElement.TryGetProperty("quantities", out var quantityErrors).Should().BeTrue();
        quantityErrors[0].GetString().Should().Be("You must specify at least one card in the maindeck or sideboard.");
    }

    [Fact]
    public async Task GetMissingDeckCards_ReturnsOnlyUnownedCards()
    {
        // Arrange
        var user = await CreateTestUserAsync("missingcards@example.com", "missingcards");
        var deck = await CreateTestDeckForUserAsync(user.Id);
        var deckCards = await CreateTestDeckCardsForDeckAsync(deck.Id, 4);
        var collection = await CreateCollectionWithOwnedCards(user.Id);
        await CreateOwnedCardsForDeckCards(collection.Id, deckCards.Take(2).ToList());

        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync($"/api/decks/{deck.Id}/missing-cards");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        var missingCards = DeserializeDeckCardList(responseContent);

        missingCards.Should().HaveCount(2);
        missingCards!.All(dc => !dc.IsOwned).Should().BeTrue();
        var expectedOracleIds = deckCards.Skip(2).Select(dc => dc.OracleId).ToList();
        missingCards.Select(dc => dc.OracleId).Should().BeEquivalentTo(expectedOracleIds);
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

        var content = SerializeToJson(updateDto);

        // Act
        var response = await client.PutAsync($"/api/decks/{deck.Id}/cards/{deckCard.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        var updatedDeckCard = DeserializeDeckCard(responseContent);

        updatedDeckCard!.MaindeckQuantity.Should().Be(2);
        updatedDeckCard.SideboardQuantity.Should().Be(2);
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
            var returnedDeckCards = DeserializeDeckCardList(responseContent);

            // Verify the response structure
            returnedDeckCards.Should().HaveCount(5);
            returnedDeckCards!.All(dc => dc.DeckId == deck.Id).Should().BeTrue();
            
            // Verify ownership detection is working
            var ownedCards = returnedDeckCards.Where(dc => dc.IsOwned).ToList();
            ownedCards.Should().HaveCount(3); // We created owned cards for 3 deck cards
            ownedCards.All(dc => dc.OwnedQuantity > 0).Should().BeTrue();
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
            var deckCard = CreateDeckCardEntity(deckId, $"oracle-{i}", $"Test Card {i}", "LEA", 4, 0);
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

        var maindeckCard = CreateDeckCardEntity(deckId, "oracle-main", "Maindeck Card", "LEA", 4, 0);
        var sideboardCard = CreateDeckCardEntity(deckId, "oracle-side", "Sideboard Card", "ICE", 0, 2);

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

    private static DeckCard CreateDeckCardEntity(int deckId, string oracleId, string name, string setCode, int maindeckQuantity, int sideboardQuantity)
    {
        return new DeckCard
        {
            DeckId = deckId,
            OracleId = oracleId,
            Name = name,
            SetCode = setCode,
            MaindeckQuantity = maindeckQuantity,
            SideboardQuantity = sideboardQuantity
        };
    }

    private static StringContent SerializeToJson<T>(T obj)
    {
        var json = JsonSerializer.Serialize(obj, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        });
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static List<DeckCardDto> DeserializeDeckCardList(string json)
    {
        return JsonSerializer.Deserialize<List<DeckCardDto>>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        })!;
    }

    private static DeckCardDto DeserializeDeckCard(string json)
    {
        return JsonSerializer.Deserialize<DeckCardDto>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        })!;
    }
}

using System.Net;
using System.Text;
using System.Text.Json;
using API.Dtos.Cards;
using API.Services;
using Core.Enums;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Core.Models;
using Core.Models.Identity;
using Infrastructure.Data;
using TestUtilities.Authentication;
using TestUtilities.Builders;
using TestUtilities.Serialization;

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
            responseContent, JsonContentHelper.DefaultOptions);

        returnedCard.Should().NotBeNull();
        returnedCard.Id.Should().Be(card.Id);
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
    public async Task UpdateCard_WithValidData_ReturnsUpdatedCard()
    {
        var owner = await CreateTestUserAsync("cardupdate@example.com", "cardupdate");
        var collection = await CreateTestCollectionAsync(owner.Id, "Update Collection");
        var card = await CreateTestCardAsync(collection.Id, "Test Card");
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var updateDto = new UpdateCollectionCardDto
        {
            CollectionId = collection.Id,
            Quantity = card.Quantity + 2,
            Language = Language.En,
            Condition = "NearMint",
            IsFoil = card.IsFoil,
            PurchasePrice = card.PurchasePrice + 1,
            PurchasePriceCurrency = Currency.Usd,
            IsMisprint = card.IsMisprint,
            IsAltered = card.IsAltered
        };

        var content = JsonContentHelper.CreateContent(updateDto);
        var response = await client.PutAsync($"/api/cards/{card.Id}", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadAsStringAsync();
        var updatedCard = JsonSerializer.Deserialize<Card>(payload, JsonContentHelper.DefaultOptions);

        updatedCard.Should().NotBeNull();
        updatedCard.Quantity.Should().Be(updateDto.Quantity);
        updatedCard.PurchasePrice.Should().Be(updateDto.PurchasePrice);
    }

    [Fact]
    public async Task UpdateCard_WithInvalidQuantity_ReturnsBadRequest()
    {
        var owner = await CreateTestUserAsync("cardupdate-invalid@example.com", "cardupdate_invalid");
        var collection = await CreateTestCollectionAsync(owner.Id, "Invalid Update Collection");
        var card = await CreateTestCardAsync(collection.Id, "Invalid Card");
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var updateDto = new UpdateCollectionCardDto
        {
            CollectionId = collection.Id,
            Quantity = 0,
            Language = Language.En,
            Condition = "NearMint",
            IsFoil = false,
            PurchasePrice = card.PurchasePrice,
            PurchasePriceCurrency = Currency.Usd,
            IsMisprint = false,
            IsAltered = false
        };

        var content = JsonContentHelper.CreateContent(updateDto);
        var response = await client.PutAsync($"/api/cards/{card.Id}", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateCard_WithoutAuthentication_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("cardupdate-noauth@example.com", "cardupdate_noauth");
        var collection = await CreateTestCollectionAsync(owner.Id, "No Auth Update Collection");
        var card = await CreateTestCardAsync(collection.Id, "No Auth Card");
        using var client = _factory.CreateClient();

        var updateDto = new UpdateCollectionCardDto
        {
            CollectionId = collection.Id,
            Quantity = card.Quantity,
            Language = Language.En,
            Condition = "NearMint",
            IsFoil = false,
            PurchasePrice = card.PurchasePrice,
            PurchasePriceCurrency = Currency.Usd,
            IsMisprint = false,
            IsAltered = false
        };

        var response = await client.PutAsync($"/api/cards/{card.Id}", JsonContentHelper.CreateContent(updateDto));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateCard_WithInvalidId_ReturnsNotFound()
    {
        var owner = await CreateTestUserAsync("cardupdate-missing@example.com", "cardupdate_missing");
        var collection = await CreateTestCollectionAsync(owner.Id, "Missing Update Collection");
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var updateDto = new UpdateCollectionCardDto
        {
            CollectionId = collection.Id,
            Quantity = 1,
            Language = Language.En,
            Condition = "NearMint",
            IsFoil = false,
            PurchasePrice = 1,
            PurchasePriceCurrency = Currency.Usd,
            IsMisprint = false,
            IsAltered = false
        };

        var response = await client.PutAsync($"/api/cards/{int.MaxValue}", JsonContentHelper.CreateContent(updateDto));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateCard_WithOtherUsersCard_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("cardupdate-owner@example.com", "cardupdate_owner");
        var intruder = await CreateTestUserAsync("cardupdate-intruder@example.com", "cardupdate_intruder");
        var collection = await CreateTestCollectionAsync(owner.Id, "Owner Collection");
        var card = await CreateTestCardAsync(collection.Id, "Owner Card");

        using var client = _factory.CreateClientWithUser(intruder.Id, intruder.UserName!, intruder.Email!);
        var updateDto = new UpdateCollectionCardDto
        {
            CollectionId = collection.Id,
            Quantity = card.Quantity,
            Language = Language.En,
            Condition = "NearMint",
            IsFoil = false,
            PurchasePrice = card.PurchasePrice,
            PurchasePriceCurrency = Currency.Usd,
            IsMisprint = false,
            IsAltered = false
        };

        var response = await client.PutAsync($"/api/cards/{card.Id}", JsonContentHelper.CreateContent(updateDto));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
    
    [Fact]
    public async Task GetCardSF_WithValidId_ReturnsCard()
    {
        var owner = await CreateTestUserAsync("cardupdate-valid@example.com", "cardupdate_valid");
        var collection = await CreateTestCollectionAsync(owner.Id, "Update Collection");
        var card = await CreateTestCardAsync(collection.Id, "Force of Will");
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        card.ScryfallId = "026983a4-03ca-4812-b129-5ea523596942";
        
        var updateDto = new UpdateCollectionCardWithSfIdDto
        {
            CollectionId = collection.Id,
            Quantity = 1,
            Language = Language.En,
            Condition = "NearMint",
            IsFoil = false,
            PurchasePrice = card.PurchasePrice,
            PurchasePriceCurrency = Currency.Usd,
            IsMisprint = false,
            IsAltered = false,
            ScryfallId = "dd60b291-0a88-4e8e-bef8-76cdfd6c8183"
        };

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MainContext>();
            var cardDataService = scope.ServiceProvider.GetRequiredService<CardDataService>();
            var oracleId = cardDataService.CardDataById.TryGetValue(updateDto.ScryfallId, out var scryfallCard)
                ? scryfallCard.OracleId
                : cardDataService.CardDataByName.TryGetValue("Force of Will", out var namedCard)
                    ? namedCard.OracleId
                    : cardDataService.CardDataById.Values.First().OracleId;

            var trackedCard = await context.Cards.FindAsync(card.Id);
            trackedCard!.OracleId = oracleId;
            await context.SaveChangesAsync();
        }

        var content = JsonContentHelper.CreateContent(updateDto);
        var response = await client.PutAsync($"/api/cards/{card.Id}/versions", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadAsStringAsync();
        var updatedCard = JsonSerializer.Deserialize<Card>(payload, JsonContentHelper.DefaultOptions);

        updatedCard.Should().NotBeNull();
        updatedCard.ScryfallId.Should().Be(updateDto.ScryfallId);
        updatedCard.SetName.Should().Be("Double Masters");
        updatedCard.Quantity.Should().Be(updateDto.Quantity);
        updatedCard.PurchasePrice.Should().Be(updateDto.PurchasePrice);
    }
    
    [Fact]
    public async Task UpdateCardSF_WithInvalidVersion_ReturnsBadRequest()
    {
        var owner = await CreateTestUserAsync("cardupdate-invalid@example.com", "cardupdate_invalid");
        var collection = await CreateTestCollectionAsync(owner.Id, "Invalid Update Collection");
        var card = await CreateTestCardAsync(collection.Id, "Invalid Card");
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        card.ScryfallId = "026983a4-03ca-4812-b129-5ea523596942";
        card.Name = "Force of Will";
        
        var updateDto = new UpdateCollectionCardWithSfIdDto
        {
            CollectionId = collection.Id,
            Quantity = 1,
            Language = Language.En,
            Condition = "NearMint",
            IsFoil = false,
            PurchasePrice = card.PurchasePrice,
            PurchasePriceCurrency = Currency.Usd,
            IsMisprint = false,
            IsAltered = false,
            ScryfallId = "0a1b4e2e-5459-4fae-81d9-1e882647daac"
        };

        var content = JsonContentHelper.CreateContent(updateDto);
        var response = await client.PutAsync($"/api/cards/{card.Id}/versions", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task UpdateCardSF_WithInvalidSFId_ReturnsBadRequest()
    {
        var owner = await CreateTestUserAsync("cardupdate-invalid@example.com", "cardupdate_invalid");
        var collection = await CreateTestCollectionAsync(owner.Id, "Invalid Update Collection");
        var card = await CreateTestCardAsync(collection.Id, "Invalid Card");
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var updateDto = new UpdateCollectionCardWithSfIdDto
        {
            CollectionId = collection.Id,
            Quantity = 1,
            Language = Language.En,
            Condition = "NearMint",
            IsFoil = false,
            PurchasePrice = card.PurchasePrice,
            PurchasePriceCurrency = Currency.Usd,
            IsMisprint = false,
            IsAltered = false,
            ScryfallId = "TEST"
        };

        var content = JsonContentHelper.CreateContent(updateDto);
        var response = await client.PutAsync($"/api/cards/{card.Id}/versions", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task UpdateCardSF_WithoutAuthentication_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("cardupdate-noauth@example.com", "cardupdate_noauth");
        var collection = await CreateTestCollectionAsync(owner.Id, "No Auth Update Collection");
        var card = await CreateTestCardAsync(collection.Id, "No Auth Card");
        using var client = _factory.CreateClient();

        var updateDto = new UpdateCollectionCardWithSfIdDto
        {
            CollectionId = collection.Id,
            Quantity = card.Quantity,
            Language = Language.En,
            Condition = "NearMint",
            IsFoil = false,
            PurchasePrice = card.PurchasePrice,
            PurchasePriceCurrency = Currency.Usd,
            IsMisprint = false,
            IsAltered = false,
            ScryfallId = "TEST"
        };

        var response = await client.PutAsync($"/api/cards/{card.Id}/versions", JsonContentHelper.CreateContent(updateDto));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
    
    [Fact]
    public async Task UpdateCardSF_WithInvalidId_ReturnsNotFound()
    {
        var owner = await CreateTestUserAsync("cardupdate-missing@example.com", "cardupdate_missing");
        var collection = await CreateTestCollectionAsync(owner.Id, "Missing Update Collection");
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var updateDto = new UpdateCollectionCardWithSfIdDto
        {
            CollectionId = collection.Id,
            Quantity = 1,
            Language = Language.En,
            Condition = "NearMint",
            IsFoil = false,
            PurchasePrice = 1,
            PurchasePriceCurrency = Currency.Usd,
            IsMisprint = false,
            IsAltered = false,
            ScryfallId = "TEST"
        };

        var response = await client.PutAsync($"/api/cards/{int.MaxValue}/versions", JsonContentHelper.CreateContent(updateDto));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task UpdateCardSF_WithOtherUsersCard_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("cardupdate-owner@example.com", "cardupdate_owner");
        var intruder = await CreateTestUserAsync("cardupdate-intruder@example.com", "cardupdate_intruder");
        var collection = await CreateTestCollectionAsync(owner.Id, "Owner Collection");
        var card = await CreateTestCardAsync(collection.Id, "Owner Card");

        using var client = _factory.CreateClientWithUser(intruder.Id, intruder.UserName!, intruder.Email!);
        var updateDto = new UpdateCollectionCardWithSfIdDto
        {
            CollectionId = collection.Id,
            Quantity = card.Quantity,
            Language = Language.En,
            Condition = "NearMint",
            IsFoil = false,
            PurchasePrice = card.PurchasePrice,
            PurchasePriceCurrency = Currency.Usd,
            IsMisprint = false,
            IsAltered = false,
            ScryfallId =  "test"
        };

        var response = await client.PutAsync($"/api/cards/{card.Id}/versions", JsonContentHelper.CreateContent(updateDto));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "because authenticated users should be able to delete their own cards");

        // Verify card was actually deleted
        await VerifyCardDeletedFromDatabase(card.Id);

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<MainContext>();
        var updatedCollection = await verificationContext.Collections.FindAsync(collection.Id);

        updatedCollection.Should().NotBeNull();
        updatedCollection.NumberOfCards.Should().Be(0);
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
    public async Task DeleteCard_WithoutAuthentication_ReturnsUnauthorized()
    {
        var user = await CreateTestUserAsync("deletecard-noauth@example.com", "deletecard_noauth");
        var collection = await CreateTestCollectionAsync(user.Id, "Delete Card NoAuth Collection");
        var card = await CreateTestCardAsync(collection.Id, "Delete Card NoAuth");

        using var client = _factory.CreateClient();
        var response = await client.DeleteAsync($"/api/cards/{card.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
            responseContent, JsonContentHelper.DefaultOptions);

        cards.Should().NotBeNull();
        cards.Should().HaveCountGreaterThan(0, "because there should be cards matching 'Lightning'");
    }

    [Fact]
    public async Task SearchCards_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/cards/search/Lightning");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SearchCards_WithNoMatches_ReturnsNotFound()
    {
        var user = await CreateTestUserAsync("searcher-nomatch@example.com", "searcher_nomatch");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var response = await client.GetAsync("/api/cards/search/NoSuchCardNameShouldExist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
            ScryfallId = "97398ad2-675b-4a34-aab7-935dd6714f1c",
            CollectionId = collection.Id,
            Quantity = 2,
            Language = Language.En,
            Version = "dmc",
            Condition = "NearMint",
            IsFoil = false,
            PurchasePrice = 1.50,
            PurchasePriceCurrency = Currency.Usd,
            IsMisprint = false,
            IsAltered = false
        };

        var json = JsonSerializer.Serialize(cardRequest, JsonContentHelper.DefaultOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/cards", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, "because valid card data should create a new card");

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<MainContext>();
        var updatedCollection = await verificationContext.Collections.FindAsync(collection.Id);

        updatedCollection.Should().NotBeNull();
        updatedCollection.NumberOfCards.Should().Be(cardRequest.Quantity);
    }

    [Fact]
    public async Task AddNewCard_WithoutPurchasePrice_UsesLivePricing()
    {
        // Arrange
        var user = await CreateTestUserAsync("cardadder-noprice@example.com", "cardadder_noprice");
        var collection = await CreateTestCollectionAsync(user.Id, "Auto Price Collection");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var cardRequest = new
        {
            ScryfallId = "97398ad2-675b-4a34-aab7-935dd6714f1c",
            CollectionId = collection.Id,
            Quantity = 1,
            Language = Language.En,
            Condition = "NearMint",
            IsFoil = false,
            IsMisprint = false,
            IsAltered = false
        };

        var json = JsonSerializer.Serialize(cardRequest, JsonContentHelper.DefaultOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/cards", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, "because the API should fill missing prices with live data");

        var payload = await response.Content.ReadAsStringAsync();
        var createdCard = JsonSerializer.Deserialize<Card>(payload, JsonContentHelper.DefaultOptions);

        createdCard.Should().NotBeNull();
        createdCard.PurchasePrice.Should().BeGreaterThan(0, "because a live price should be applied when none is supplied");
        Enum.IsDefined(createdCard.PurchasePriceCurrency).Should().BeTrue();
    }
    
    [Fact]
    public async Task AddNewCard_WithInvalidCondition_ReturnsBadRequest()
    {
        var user = await CreateTestUserAsync("cardadder-invalid@example.com", "cardadder_invalid");
        var collection = await CreateTestCollectionAsync(user.Id, "Invalid Card Collection");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var request = new
        {
            OracleId = "97398ad2-675b-4a34-aab7-935dd6714f1c",
            CollectionId = collection.Id,
            Quantity = 1,
            Language = Language.En,
            Condition = "InvalidCondition",
            IsFoil = false,
            PurchasePrice = 1.0,
            PurchasePriceCurrency = Currency.Usd,
            IsMisprint = false,
            IsAltered = false
        };

        var response = await client.PostAsync("/api/cards", JsonContentHelper.CreateContent(request));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddNewCard_WithoutAuthentication_ReturnsUnauthorized()
    {
        var user = await CreateTestUserAsync("cardadder-noauth@example.com", "cardadder_noauth");
        var collection = await CreateTestCollectionAsync(user.Id, "NoAuth Collection");
        using var client = _factory.CreateClient();

        var request = new InternalCardDto
        {
            ScryfallId = "97398ad2-675b-4a34-aab7-935dd6714f1c",
            CollectionId = collection.Id,
            Quantity = 1,
            Language = Language.En,
            Condition = "NearMint",
            IsFoil = false,
            PurchasePrice = 1.0,
            PurchasePriceCurrency = Currency.Usd,
            IsMisprint = false,
            IsAltered = false
        };

        var response = await client.PostAsync("/api/cards", JsonContentHelper.CreateContent(request));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AddNewCard_WithUnknownCollection_ReturnsNotFound()
    {
        var user = await CreateTestUserAsync("cardadder-missing@example.com", "cardadder_missing");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var request = new InternalCardDto
        {
            ScryfallId = "97398ad2-675b-4a34-aab7-935dd6714f1c",
            CollectionId = int.MaxValue,
            Quantity = 1,
            Language = Language.En,
            Condition = "NearMint",
            IsFoil = false,
            PurchasePrice = 1.0,
            PurchasePriceCurrency = Currency.Usd,
            IsMisprint = false,
            IsAltered = false
        };

        var response = await client.PostAsync("/api/cards", JsonContentHelper.CreateContent(request));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ProcessCardList_WithValidInput_ReturnsOracleCards()
    {
        var user = await CreateTestUserAsync("cardlist-user@example.com", "cardlist_user");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var request = new CardListDto
        {
            CardList = "Counterspell\nLightning Bolt"
        };

        var response = await client.PostAsync("/api/cards/card-list", JsonContentHelper.CreateContent(request));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadAsStringAsync();
        var cards = JsonSerializer.Deserialize<List<ScryfallCardDto>>(payload, JsonContentHelper.DefaultOptions);

        cards.Should().NotBeNull();
        cards.Should().HaveCount(2);
        cards.Select(c => c.Name).Should().Contain(["Counterspell", "Lightning Bolt"]);
    }

    [Fact]
    public async Task ProcessCardList_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();
        var request = new CardListDto { CardList = "Counterspell" };

        var response = await client.PostAsync("/api/cards/card-list", JsonContentHelper.CreateContent(request));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
    
    [Fact]
    public async Task GetCardFromName_ReturnsCard()
    {
        // Arrange
        const string cardName = "Counterspell";
        var user = await CreateTestUserAsync("user@example.com", "user");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);
        // Act
        var response = await client.GetAsync($"/api/cards/sf/name/{cardName}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "because counterspell exists");
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var returnedCard = JsonSerializer.Deserialize<ScryfallCardDto>(
            responseContent, JsonContentHelper.DefaultOptions);

        returnedCard.Should().NotBeNull();
        returnedCard.Name.Should().Be(cardName);
        returnedCard.ImageUris!.Large.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCardFromName_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/cards/sf/name/Counterspell");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCardFromName_WithUnknownCard_ReturnsNotFound()
    {
        var user = await CreateTestUserAsync("cardname-missing@example.com", "cardname_missing");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var response = await client.GetAsync("/api/cards/sf/name/NotARealCardName");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetCardFromOracleId_ReturnsCard()
    {
        // Arrange
        const string oracleId = "0df55e3f-14de-46ef-b6b1-616618724d9e";
        const string cardName = "Counterspell";
        var user = await CreateTestUserAsync("user@example.com", "user");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);
        // Act
        var response = await client.GetAsync($"/api/cards/sf/id/{oracleId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "because counterspell exists");
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var returnedCard = JsonSerializer.Deserialize<ScryfallCardDto>(
            responseContent, JsonContentHelper.DefaultOptions);

        returnedCard.Should().NotBeNull();
        returnedCard.Id.Should().Be(oracleId);
        returnedCard.Name.Should().Be(cardName);
        returnedCard.ImageUris!.Large.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCardFromOracleId_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/cards/sf/id/0df55e3f-14de-46ef-b6b1-616618724d9e");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCardFromOracleId_WithUnknownId_ReturnsNotFound()
    {
        var user = await CreateTestUserAsync("oracleid-missing@example.com", "oracleid_missing");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var response = await client.GetAsync($"/api/cards/sf/id/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetCardVersions_WithValidName_ReturnsVersions()
    {
        var user = await CreateTestUserAsync("versions-user@example.com", "versions_user");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var response = await client.GetAsync("/api/cards/Counterspell/versions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(payload);
        json.RootElement.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetCardVersions_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/cards/Counterspell/versions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCardVersions_WithUnknownCard_ReturnsNotFound()
    {
        var user = await CreateTestUserAsync("versions-missing@example.com", "versions_missing");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var response = await client.GetAsync("/api/cards/UnknownCardName/versions");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #region Helper Methods

    private Task<AppUser> CreateTestUserAsync(string baseEmail, string baseUserName) =>
        TestUserFactory.CreateAsync(_factory.Services, _testDataBuilder, baseEmail, baseUserName);

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
            ScryfallId = Guid.NewGuid()
                .ToString(),
            OracleId = Guid.NewGuid().ToString(),
            Quantity = quantity,
            Language = Language.En,
            Condition = Condition.NearMint,
            IsFoil = false,
            PurchasePrice = 1.0,
            PurchasePriceCurrency = Currency.Usd,
            ImageUrl = "https://example.com/card.jpg",
            BackImageUrl = null,
            SetCode = "TST",
            SetName = "Test Set",
            CollectorNumber = "001",
            Rarity = "Common",
            IsMisprint = false,
            IsAltered = false,
            ArtCrop = "https://example.com/card.jpg",
            TypeLine = "Instant"
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

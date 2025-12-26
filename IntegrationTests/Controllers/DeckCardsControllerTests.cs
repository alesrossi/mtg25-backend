using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.Dtos.Cards;
using API.Dtos.Decks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Core.Models;
using Core.Models.Identity;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using API.Services;
using TestUtilities.Authentication;
using TestUtilities.Builders;
using TestUtilities.Serialization;
using TestUtilities.Scryfall;

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
    public async Task GetDeckCards_WithoutAuthentication_ReturnsUnauthorized()
    {
        var user = await CreateTestUserAsync("deckcards-noauth@example.com", "deckcards_noauth");
        var deck = await CreateTestDeckForUserAsync(user.Id);
        await CreateTestDeckCardsForDeckAsync(deck.Id, 1);

        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/decks/{deck.Id}/cards");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDeckCards_WithInvalidDeck_ReturnsNotFound()
    {
        var user = await CreateTestUserAsync("deckcards-missing@example.com", "deckcards_missing");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var response = await client.GetAsync($"/api/decks/{int.MaxValue}/cards");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
    public async Task GetDeckCards_WithOwnedCardFromDifferentPrinting_DetectsOwnershipByName()
    {
        // Arrange
        var user = await CreateTestUserAsync("deckcards-diffprint@example.com", "deckcards_diffprint");
        var deck = await CreateTestDeckForUserAsync(user.Id);
        var deckCard = await CreateDeckCardWithNameAsync(deck.Id, Guid.NewGuid().ToString(), "Serra Angel");
        var collection = await CreateCollectionWithOwnedCards(user.Id);
        await AddOwnedCardVersionAsync(collection.Id, Guid.NewGuid().ToString(), deckCard.Name, quantity: 3);

        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync($"/api/decks/{deck.Id}/cards");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        var returnedDeckCards = DeserializeDeckCardList(responseContent);

        var targetCard = returnedDeckCards.Should()
            .ContainSingle(dc => dc.Id == deckCard.Id)
            .Subject;
        targetCard.IsOwned.Should().BeTrue();
        targetCard.OwnedQuantity.Should().Be(3);
        targetCard.OwnershipStatus.Should().Be("PartiallyOwned");
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
    public async Task GetDeckCardById_WithoutAuthentication_ReturnsUnauthorized()
    {
        var user = await CreateTestUserAsync("deckcard-noauth@example.com", "deckcard_noauth");
        var deck = await CreateTestDeckForUserAsync(user.Id);
        var deckCard = (await CreateTestDeckCardsForDeckAsync(deck.Id, 1)).First();

        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/decks/{deck.Id}/cards/{deckCard.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDeckCardById_WithInvalidDeck_ReturnsNotFound()
    {
        var user = await CreateTestUserAsync("deckcard-missingdeck@example.com", "deckcard_missingdeck");
        var deck = await CreateTestDeckForUserAsync(user.Id);
        var deckCard = (await CreateTestDeckCardsForDeckAsync(deck.Id, 1)).First();

        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);
        var response = await client.GetAsync($"/api/decks/{int.MaxValue}/cards/{deckCard.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetDeckCardById_WithInvalidCardId_ReturnsNotFound()
    {
        var user = await CreateTestUserAsync("deckcard-missingcard@example.com", "deckcard_missingcard");
        var deck = await CreateTestDeckForUserAsync(user.Id);
        await CreateTestDeckCardsForDeckAsync(deck.Id, 1);

        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);
        var response = await client.GetAsync($"/api/decks/{deck.Id}/cards/{int.MaxValue}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateDeckCard_WithValidData_CreatesDeckCard()
    {
        // Arrange
        var user = await CreateTestUserAsync("testuser@example.com", "testuser");
        var deck = await CreateTestDeckForUserAsync(user.Id);
        await SeedDefaultCardDataAsync("Lightning Bolt");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var createDto = new CreateDeckCardDto
        {
            Name = "Lightning Bolt",
            MaindeckQuantity = 4,
            SideboardQuantity = 0
        };

        var content = SerializeToJson(createDto);

        // Act
        var response = await client.PostAsync($"/api/decks/{deck.Id}/cards", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        var createdDeckCard = DeserializeDeckCard(responseContent);

        createdDeckCard!.DeckId.Should().Be(deck.Id);
        createdDeckCard.Name.Should().Be("Lightning Bolt");
        createdDeckCard.MaindeckQuantity.Should().Be(4);
    }

    [Fact]
    public async Task CreateDeckCard_WithOwnedVersion_UsesOwnedCardDetails()
    {
        var user = await CreateTestUserAsync("deckcard-owned@example.com", "deckcard_owned");
        var deck = await CreateTestDeckForUserAsync(user.Id);
        await SeedDefaultCardDataAsync("Serra Angel", "Creature", new[] { "W" });
        var collection = await CreateCollectionWithOwnedCards(user.Id);
        await AddOwnedCardVersionAsync(collection.Id, Guid.NewGuid().ToString(), "Serra Angel", quantity: 1);
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var createDto = BuildDeckCardRequest("Serra Angel", 1);

        var response = await client.PostAsync($"/api/decks/{deck.Id}/cards", SerializeToJson(createDto));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var createdDeckCard = DeserializeDeckCard(await response.Content.ReadAsStringAsync());
        createdDeckCard!.IsOwned.Should().BeTrue();
        createdDeckCard.OwnedQuantity.Should().BeGreaterThan(0);
        createdDeckCard.OwnedCardId.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateDeckCard_WithPriceData_UpdatesDeckTotalPrice()
    {
        // Arrange
        var user = await CreateTestUserAsync("priceupdate@example.com", "priceupdate");
        var deck = await CreateTestDeckForUserAsync(user.Id);
        var pricedCard = CreateScryfallCard("oracle-priced", "Priced Card", "LEA", "Limited Edition Alpha") with
        {
            Prices = new Prices("3.00", null, "1.50", null, null)
        };
        await SeedCardDataAsync(new[] { pricedCard });

        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);
        var createDto = new CreateDeckCardDto
        {
            Name = pricedCard.Name,
            MaindeckQuantity = 2,
            SideboardQuantity = 1
        };

        // Act
        var response = await client.PostAsync($"/api/decks/{deck.Id}/cards", SerializeToJson(createDto));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshedDeck = await LoadDeckAsync(deck.Id);
        refreshedDeck!.TotalPrice.Should().Be(4.5);
        refreshedDeck.TotalPriceCurrency.Should().Be(Currency.Eur);
        refreshedDeck.NumberOfCards.Should().Be(3);
        refreshedDeck.NumberOfMainBoardCards.Should().Be(2);
        refreshedDeck.NumberOfSideBoardCards.Should().Be(1);
    }

    [Fact]
    public async Task CreateDeckCard_AddsColorsToDeck()
    {
        var user = await CreateTestUserAsync("deckcard-colors@example.com", "deckcard_colors");
        var deck = await CreateTestDeckForUserAsync(user.Id);
        await SeedDefaultCardDataAsync("Izzet Charm", "Instant", new[] { "U", "R" });
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var createDto = BuildDeckCardRequest("Izzet Charm", 1);

        var response = await client.PostAsync($"/api/decks/{deck.Id}/cards", SerializeToJson(createDto));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshedDeck = await LoadDeckAsync(deck.Id);
        refreshedDeck!.ColorIdentity.Should().Contain(new[] { "U", "R" });
    }

    [Fact]
    public async Task CreateDeckCard_WithoutAuthentication_ReturnsUnauthorized()
    {
        var user = await CreateTestUserAsync("deckcard-create-noauth@example.com", "deckcard_create_noauth");
        var deck = await CreateTestDeckForUserAsync(user.Id);

        using var client = _factory.CreateClient();
        var createDto = BuildDeckCardRequest("Unauthorized", 1);

        var response = await client.PostAsync($"/api/decks/{deck.Id}/cards", SerializeToJson(createDto));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateDeckCard_WithTooManyCopies_ReturnsBadRequest()
    {
        var user = await CreateTestUserAsync("deckcard-limit@example.com", "deckcard_limit");
        var deck = await CreateTestDeckForUserAsync(user.Id);
        await SeedDefaultCardDataAsync("Lightning Bolt");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var initialDto = BuildDeckCardRequest("Lightning Bolt", 3, 1);
        var duplicateDto = BuildDeckCardRequest("Lightning Bolt", 1);

        var firstResponse = await client.PostAsync($"/api/decks/{deck.Id}/cards", SerializeToJson(initialDto));
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondResponse = await client.PostAsync($"/api/decks/{deck.Id}/cards", SerializeToJson(duplicateDto));
        secondResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var responseContent = await secondResponse.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(responseContent);
        var errors = json.RootElement.GetProperty("errors");
        errors.GetArrayLength().Should().BeGreaterThan(0);
        errors[0].GetString().Should().Contain("4-copy");
    }

    [Fact]
    public async Task CreateDeckCard_WithBasicLand_AllowsMoreThanFourCopies()
    {
        var user = await CreateTestUserAsync("deckcard-basic@example.com", "deckcard_basic");
        var deck = await CreateTestDeckForUserAsync(user.Id);
        await SeedDefaultCardDataAsync("Plains", "Basic Land — Plains");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var firstDto = BuildDeckCardRequest("Plains", 4);
        var secondDto = BuildDeckCardRequest("Plains", 5);

        var firstResponse = await client.PostAsync($"/api/decks/{deck.Id}/cards", SerializeToJson(firstDto));
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondResponse = await client.PostAsync($"/api/decks/{deck.Id}/cards", SerializeToJson(secondDto));
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateDeckCard_WithInvalidDeck_ReturnsNotFound()
    {
        var user = await CreateTestUserAsync("deckcard-create-missingdeck@example.com", "deckcard_create_missingdeck");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var createDto = new CreateDeckCardDto
        {
            Name = "Missing Deck",
            MaindeckQuantity = 1,
            SideboardQuantity = 0
        };

        var response = await client.PostAsync($"/api/decks/{int.MaxValue}/cards", SerializeToJson(createDto));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateDeckCard_WithMissingRequiredFields_ReturnsValidationErrors()
    {
        // Arrange
        var user = await CreateTestUserAsync("validationuser@example.com", "validationuser");
        var deck = await CreateTestDeckForUserAsync(user.Id);
        await SeedDefaultCardDataAsync("Validation Card");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var createDto = new CreateDeckCardDto
        {
            Name = "",
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

        errorsElement.TryGetProperty("Name", out var nameErrors).Should().BeTrue();
        nameErrors[0].GetString().Should().Be("Name is required.");
    }

    [Fact]
    public async Task CreateDeckCard_WithZeroQuantities_ReturnsValidationError()
    {
        // Arrange
        var user = await CreateTestUserAsync("quantityuser@example.com", "quantityuser");
        var deck = await CreateTestDeckForUserAsync(user.Id);
        await SeedDefaultCardDataAsync("Lightning Bolt");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var createDto = new CreateDeckCardDto
        {
            Name = "Lightning Bolt",
            MaindeckQuantity = 0,
            SideboardQuantity = 0
        };

        var content = SerializeToJson(createDto);

        // Act
        var response = await client.PostAsync($"/api/decks/{deck.Id}/cards", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
        var expectedOracleIds = deckCards.Skip(2).Select(dc => dc.ScryfallId).ToList();
        missingCards.Select(dc => dc.ScryfallId).Should().BeEquivalentTo(expectedOracleIds);
    }

    [Fact]
    public async Task GetMissingDeckCards_WithoutAuthentication_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("deckmissing-noauth@example.com", "deckmissing_noauth");
        var deck = await CreateTestDeckForUserAsync(owner.Id);

        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/decks/{deck.Id}/missing-cards");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMissingDeckCards_WithInvalidDeck_ReturnsNotFound()
    {
        var owner = await CreateTestUserAsync("deckmissing-missingdeck@example.com", "deckmissing_missingdeck");
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var response = await client.GetAsync($"/api/decks/{int.MaxValue}/missing-cards");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
    public async Task UpdateDeckCard_WithQuantityChange_RecalculatesDeckPrice()
    {
        // Arrange
        var user = await CreateTestUserAsync("priceupdate2@example.com", "priceupdate2");
        var deck = await CreateTestDeckForUserAsync(user.Id);
        var pricedCard = CreateScryfallCard("oracle-priced-update", "Priced Update", "LEA", "Limited Edition Alpha") with
        {
            Prices = new Prices("2.00", null, "1.50", null, null)
        };
        await SeedCardDataAsync(new[] { pricedCard });

        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);
        var createDto = BuildDeckCardRequest(pricedCard.Name, 2, 1);

        var createResponse = await client.PostAsync($"/api/decks/{deck.Id}/cards", SerializeToJson(createDto));
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var createdDeckCard = DeserializeDeckCard(await createResponse.Content.ReadAsStringAsync());

        var updateDto = new UpdateDeckCardDto
        {
            MaindeckQuantity = 1,
            SideboardQuantity = 3,
            OwnedCardId = null
        };

        // Act
        var updateResponse = await client.PutAsync($"/api/decks/{deck.Id}/cards/{createdDeckCard!.Id}", SerializeToJson(updateDto));

        // Assert
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshedDeck = await LoadDeckAsync(deck.Id);
        refreshedDeck!.TotalPrice.Should().Be(6.0);
        refreshedDeck.TotalPriceCurrency.Should().Be(Currency.Eur);
        refreshedDeck.NumberOfCards.Should().Be(4);
        refreshedDeck.NumberOfMainBoardCards.Should().Be(1);
        refreshedDeck.NumberOfSideBoardCards.Should().Be(3);
    }

    [Fact]
    public async Task UpdateDeckCard_WithQuantityLimitExceeded_ReturnsBadRequest()
    {
        var user = await CreateTestUserAsync("deckcard-update-limit@example.com", "deckcard_update_limit");
        var deck = await CreateTestDeckForUserAsync(user.Id);
        await SeedDefaultCardDataAsync("Lightning Bolt");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var createDto = BuildDeckCardRequest("Lightning Bolt", 3);

        var createResponse = await client.PostAsync($"/api/decks/{deck.Id}/cards", SerializeToJson(createDto));
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var createdDeckCard = DeserializeDeckCard(await createResponse.Content.ReadAsStringAsync());

        var updateDto = new UpdateDeckCardDto
        {
            MaindeckQuantity = 5,
            SideboardQuantity = 0,
            OwnedCardId = null
        };

        var updateResponse = await client.PutAsync($"/api/decks/{deck.Id}/cards/{createdDeckCard!.Id}", SerializeToJson(updateDto));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await updateResponse.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(payload);
        var errors = json.RootElement.GetProperty("errors");
        errors[0].GetString().Should().Contain("4-copy");
    }

    [Fact]
    public async Task UpdateDeckCardVersion_WithNewColors_UpdatesDeckColorIdentity()
    {
        var user = await CreateTestUserAsync("deckcard-version-colors@example.com", "deckcard_version_colors");
        var deck = await CreateTestDeckForUserAsync(user.Id);
        await SeedDefaultCardDataAsync("Feral Hydra", "Creature", new[] { "G" });
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var createDto = BuildDeckCardRequest("Feral Hydra", 2);

        var createResponse = await client.PostAsync($"/api/decks/{deck.Id}/cards", SerializeToJson(createDto));
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var createdDeckCard = DeserializeDeckCard(await createResponse.Content.ReadAsStringAsync());

        var newVersion = CreateScryfallCard("two-color-new", createDto.Name, "RNA", "Ravnica Allegiance") with
        {
            ColorIdentity = new List<string?> { "G", "U" },
            TypeLine = "Creature"
        };
        await SeedCardDataAsync(new[] { newVersion });

        var updateDto = new UpdateDeckCardVersionDto
        {
            ScryfallId = newVersion.Id,
            MaindeckQuantity = createDto.MaindeckQuantity,
            SideboardQuantity = createDto.SideboardQuantity,
            OwnedCardId = null
        };

        var updateResponse = await client.PutAsync($"/api/decks/{deck.Id}/cards/{createdDeckCard!.Id}/versions", SerializeToJson(updateDto));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshedDeck = await LoadDeckAsync(deck.Id);
        refreshedDeck!.ColorIdentity.Should().Contain("U");
    }

    [Fact]
    public async Task DeleteDeckCard_WithValidData_RemovesDeckCard()
    {
        var owner = await CreateTestUserAsync("deckcard-delete-owner@example.com", "deckcard_delete_owner");
        var deck = await CreateTestDeckForUserAsync(owner.Id);
        var deckCard = (await CreateTestDeckCardsForDeckAsync(deck.Id, 1)).First();
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var response = await client.DeleteAsync($"/api/decks/{deck.Id}/cards/{deckCard.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await client.GetAsync($"/api/decks/{deck.Id}/cards");
        var payload = await listResponse.Content.ReadAsStringAsync();
        var cards = DeserializeDeckCardList(payload);
        cards.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteDeckCard_RemovesUnusedColorIdentity()
    {
        var owner = await CreateTestUserAsync("deckcard-delete-color@example.com", "deckcard_delete_color");
        var deck = await CreateTestDeckForUserAsync(owner.Id);
        await SeedDefaultCardDataAsync("Serra Angel", "Creature", new[] { "W" });
        await SeedDefaultCardDataAsync("Counterspell", "Instant", new[] { "U" });
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        await client.PostAsync($"/api/decks/{deck.Id}/cards", SerializeToJson(BuildDeckCardRequest("Serra Angel", 1)));
        var secondResponse = await client.PostAsync($"/api/decks/{deck.Id}/cards", SerializeToJson(BuildDeckCardRequest("Counterspell", 1)));
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var counterspell = DeserializeDeckCard(await secondResponse.Content.ReadAsStringAsync());

        var deleteResponse = await client.DeleteAsync($"/api/decks/{deck.Id}/cards/{counterspell!.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refreshedDeck = await LoadDeckAsync(deck.Id);
        refreshedDeck!.ColorIdentity.Should().NotContain("U");
        refreshedDeck.ColorIdentity.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteDeckCard_UpdatesDeckAggregates()
    {
        var owner = await CreateTestUserAsync("deckcard-delete-agg@example.com", "deckcard_delete_agg");
        var deck = await CreateTestDeckForUserAsync(owner.Id);

        var pricedCard = CreateScryfallCard("delete-priced", "Shock", "M10", "Magic 2010") with
        {
            Prices = new Prices("1.00", null, "0.50", null, null)
        };
        await SeedCardDataAsync(new[] { pricedCard });

        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);
        var createDto = BuildDeckCardRequest(pricedCard.Name, 2, 1);

        var createResponse = await client.PostAsync($"/api/decks/{deck.Id}/cards", SerializeToJson(createDto));
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var createdDeckCard = DeserializeDeckCard(await createResponse.Content.ReadAsStringAsync());

        var deckBeforeDelete = await LoadDeckAsync(deck.Id);
        deckBeforeDelete!.NumberOfCards.Should().Be(3);
        deckBeforeDelete.TotalPrice.Should().BeGreaterThan(0);

        var deleteResponse = await client.DeleteAsync($"/api/decks/{deck.Id}/cards/{createdDeckCard!.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refreshedDeck = await LoadDeckAsync(deck.Id);
        refreshedDeck!.NumberOfCards.Should().Be(0);
        refreshedDeck.NumberOfMainBoardCards.Should().Be(0);
        refreshedDeck.NumberOfSideBoardCards.Should().Be(0);
        refreshedDeck.TotalPrice.Should().Be(0);
    }

    [Fact]
    public async Task DeleteDeckCard_WithoutAuthentication_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("deckcard-delete-noauth@example.com", "deckcard_delete_noauth");
        var deck = await CreateTestDeckForUserAsync(owner.Id);
        var deckCard = (await CreateTestDeckCardsForDeckAsync(deck.Id, 1)).First();

        using var client = _factory.CreateClient();
        var response = await client.DeleteAsync($"/api/decks/{deck.Id}/cards/{deckCard.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteDeckCard_WithInvalidDeck_ReturnsNotFound()
    {
        var owner = await CreateTestUserAsync("deckcard-delete-missingdeck@example.com", "deckcard_delete_missingdeck");
        var deck = await CreateTestDeckForUserAsync(owner.Id);
        var deckCard = (await CreateTestDeckCardsForDeckAsync(deck.Id, 1)).First();

        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);
        var response = await client.DeleteAsync($"/api/decks/{int.MaxValue}/cards/{deckCard.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteDeckCard_WithInvalidCardId_ReturnsNotFound()
    {
        var owner = await CreateTestUserAsync("deckcard-delete-missingcard@example.com", "deckcard_delete_missingcard");
        var deck = await CreateTestDeckForUserAsync(owner.Id);
        await CreateTestDeckCardsForDeckAsync(deck.Id, 1);

        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);
        var response = await client.DeleteAsync($"/api/decks/{deck.Id}/cards/{int.MaxValue}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateDeckCard_WithoutAuthentication_ReturnsUnauthorized()
    {
        var user = await CreateTestUserAsync("deckcard-update-noauth@example.com", "deckcard_update_noauth");
        var deck = await CreateTestDeckForUserAsync(user.Id);
        var deckCard = (await CreateTestDeckCardsForDeckAsync(deck.Id, 1)).First();

        using var client = _factory.CreateClient();
        var updateDto = new UpdateDeckCardDto { MaindeckQuantity = 1, SideboardQuantity = 1 };
        var response = await client.PutAsync($"/api/decks/{deck.Id}/cards/{deckCard.Id}", SerializeToJson(updateDto));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateDeckCard_WithInvalidDeck_ReturnsNotFound()
    {
        var user = await CreateTestUserAsync("deckcard-update-missingdeck@example.com", "deckcard_update_missingdeck");
        var deck = await CreateTestDeckForUserAsync(user.Id);
        var deckCard = (await CreateTestDeckCardsForDeckAsync(deck.Id, 1)).First();

        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);
        var updateDto = new UpdateDeckCardDto { MaindeckQuantity = 1, SideboardQuantity = 0 };
        var response = await client.PutAsync($"/api/decks/{int.MaxValue}/cards/{deckCard.Id}", SerializeToJson(updateDto));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateDeckCardVersion_WithValidData_ReturnsUpdatedDeckCard()
    {
        var owner = await CreateTestUserAsync("deckcard-version@example.com", "deckcard_version");
        var deck = await CreateTestDeckForUserAsync(owner.Id);
        var deckCard = await CreateDeckCardWithNameAsync(deck.Id, "026983a4-03ca-4812-b129-5ea523596942", "Force of Will");
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var updateDto = new UpdateDeckCardVersionDto
        {
            ScryfallId = "dd60b291-0a88-4e8e-bef8-76cdfd6c8183"
        };

        await SeedCardDataAsync(new[]
        {
            CreateScryfallCard("026983a4-03ca-4812-b129-5ea523596942", "Force of Will", "ALL", "Alliances")
        });
        
        await SeedCardDataAsync(new[]
        {
            CreateScryfallCard(updateDto.ScryfallId, "Force of Will", "2XM", "Double Masters")
        });

        var response = await client.PutAsync($"/api/decks/{deck.Id}/cards/{deckCard.Id}/versions", SerializeToJson(updateDto));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadAsStringAsync();
        var updatedDeckCard = DeserializeDeckCard(payload);

        updatedDeckCard.Should().NotBeNull();
        updatedDeckCard!.ScryfallId.Should().Be(updateDto.ScryfallId);
        updatedDeckCard.SetName.Should().Be("Double Masters");
    }

    [Fact]
    public async Task UpdateDeckCardVersion_WithInvalidVersion_ReturnsBadRequest()
    {
        var owner = await CreateTestUserAsync("deckcard-version-invalid@example.com", "deckcard_version_invalid");
        var deck = await CreateTestDeckForUserAsync(owner.Id);
        var deckCard = await CreateDeckCardWithNameAsync(deck.Id, "026983a4-03ca-4812-b129-5ea523596942", "Force of Will");
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var updateDto = new UpdateDeckCardVersionDto
        {
            ScryfallId = "0df55e3f-14de-46ef-b6b1-616618724d9e"
        };

        await SeedCardDataAsync(new[]
        {
            CreateScryfallCard(updateDto.ScryfallId, "Lightning Bolt", "LEA", "Limited Edition Alpha")
        });

        var response = await client.PutAsync($"/api/decks/{deck.Id}/cards/{deckCard.Id}/versions", SerializeToJson(updateDto));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateDeckCardVersion_WithInvalidScryfallId_ReturnsBadRequest()
    {
        var owner = await CreateTestUserAsync("deckcard-version-invalidsf@example.com", "deckcard_version_invalidsf");
        var deck = await CreateTestDeckForUserAsync(owner.Id);
        var deckCard = await CreateDeckCardWithNameAsync(deck.Id, "026983a4-03ca-4812-b129-5ea523596942", "Force of Will");
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var updateDto = new UpdateDeckCardVersionDto { ScryfallId = "invalid" };

        var response = await client.PutAsync($"/api/decks/{deck.Id}/cards/{deckCard.Id}/versions", SerializeToJson(updateDto));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateDeckCardVersion_WithoutAuthentication_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("deckcard-version-noauth@example.com", "deckcard_version_noauth");
        var deck = await CreateTestDeckForUserAsync(owner.Id);
        var deckCard = await CreateDeckCardWithNameAsync(deck.Id, "026983a4-03ca-4812-b129-5ea523596942", "Force of Will");
        using var client = _factory.CreateClient();

        var updateDto = new UpdateDeckCardVersionDto { ScryfallId = "dd60b291-0a88-4e8e-bef8-76cdfd6c8183" };
        var response = await client.PutAsync($"/api/decks/{deck.Id}/cards/{deckCard.Id}/versions", SerializeToJson(updateDto));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateDeckCardVersion_WithInvalidDeck_ReturnsNotFound()
    {
        var owner = await CreateTestUserAsync("deckcard-version-missingdeck@example.com", "deckcard_version_missingdeck");
        var deck = await CreateTestDeckForUserAsync(owner.Id);
        var deckCard = await CreateDeckCardWithNameAsync(deck.Id, "026983a4-03ca-4812-b129-5ea523596942", "Force of Will");
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var updateDto = new UpdateDeckCardVersionDto { ScryfallId = "dd60b291-0a88-4e8e-bef8-76cdfd6c8183" };
        var response = await client.PutAsync($"/api/decks/{int.MaxValue}/cards/{deckCard.Id}/versions", SerializeToJson(updateDto));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateDeckCardVersion_WithOtherUsersDeck_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("deckcard-version-owner@example.com", "deckcard_version_owner");
        var intruder = await CreateTestUserAsync("deckcard-version-intruder@example.com", "deckcard_version_intruder");
        var deck = await CreateTestDeckForUserAsync(owner.Id);
        var deckCard = await CreateDeckCardWithNameAsync(deck.Id, "026983a4-03ca-4812-b129-5ea523596942", "Force of Will");

        using var client = _factory.CreateClientWithUser(intruder.Id, intruder.UserName!, intruder.Email!);
        var updateDto = new UpdateDeckCardVersionDto { ScryfallId = "dd60b291-0a88-4e8e-bef8-76cdfd6c8183" };

        await SeedCardDataAsync(new[]
        {
            CreateScryfallCard(updateDto.ScryfallId, "Force of Will", "2XM", "Double Masters")
        });

        var response = await client.PutAsync($"/api/decks/{deck.Id}/cards/{deckCard.Id}/versions", SerializeToJson(updateDto));

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

    private Task<AppUser> CreateTestUserAsync(string email, string userName) =>
        TestUserFactory.CreateAsync(_factory.Services, _testDataBuilder, email, userName, requirePassword: true);

    private async Task<Deck> CreateTestDeckForUserAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MainContext>();
        var deck = _testDataBuilder.CreateDeck(userId);
        deck.Name = "Test Deck";
        deck.TotalPrice = 0;
        deck.TotalPriceCurrency = null;
        deck.NumberOfCards = 0;
        deck.NumberOfMainBoardCards = 0;
        deck.NumberOfSideBoardCards = 0;
        context.Decks.Add(deck);
        await context.SaveChangesAsync();
        return deck;
    }

    private async Task<Deck?> LoadDeckAsync(int deckId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MainContext>();
        return await context.Decks.FindAsync(deckId);
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

    private async Task<DeckCard> CreateDeckCardWithNameAsync(int deckId, string scryfallId, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MainContext>();
        var deckCard = CreateDeckCardEntity(deckId, scryfallId, name, "LEA", 4, 0);
        deckCard.SetName = "Test Set";
        deckCard.Rarity = "rare";
        context.DeckCards.Add(deckCard);
        await context.SaveChangesAsync();
        return deckCard;
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
                deckCard.ScryfallId, 
                deckCard.Name, 
                2); // Set owned quantity to 2
            context.Cards.Add(ownedCard);
        }

        await context.SaveChangesAsync();
    }

    private async Task AddOwnedCardVersionAsync(int collectionId, string scryfallId, string name, int quantity)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MainContext>();
        var ownedCard = _testDataBuilder.CreateCardWithOracleId(collectionId, scryfallId, name, quantity);
        context.Cards.Add(ownedCard);
        await context.SaveChangesAsync();
    }

    private DeckCard CreateDeckCardEntity(int deckId, string oracleId, string name, string setCode, int maindeckQuantity, int sideboardQuantity) =>
        _testDataBuilder.CreateDeckCard(deckId, oracleId, name, setCode, maindeckQuantity, sideboardQuantity);

    private static CreateDeckCardDto BuildDeckCardRequest(string name, int mainQuantity, int sideQuantity = 0) =>
        new()
        {
            Name = name,
            MaindeckQuantity = mainQuantity,
            SideboardQuantity = sideQuantity
        };

    private Task SeedDefaultCardDataAsync(string name, string typeLine = "Instant", IEnumerable<string?>? colorIdentity = null)
    {
        var card = CreateScryfallCard(Guid.NewGuid().ToString(), name, "TST", "Test Set") with
        {
            TypeLine = typeLine,
            ColorIdentity = colorIdentity?.ToList() ?? new List<string?>()
        };
        return SeedCardDataAsync(new[] { card });
    }

    private static StringContent SerializeToJson<T>(T obj) => JsonContentHelper.CreateContent(obj);

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

    private Task SeedCardDataAsync(IEnumerable<ScryfallCardDto> cards)
    {
        using var scope = _factory.Services.CreateScope();
        var cardDataService = scope.ServiceProvider.GetRequiredService<CardDataService>();
        CardDataServiceTestHelper.Populate(cardDataService, cards);
        return Task.CompletedTask;
    }

    private ScryfallCardDto CreateScryfallCard(string id, string name, string setCode, string setName) =>
        _testDataBuilder.CreateOracleCard(id: id, oracleId: Guid.NewGuid().ToString(), name: name, setCode: setCode, setName: setName);
}

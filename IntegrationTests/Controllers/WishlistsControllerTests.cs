using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using API.Dtos.Cards;
using API.Dtos.Wishlists;
using Core.Enums;
using API.Services;
using Core.Models;
using Core.Models.Identity;
using FluentAssertions;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using TestUtilities.Authentication;
using TestUtilities.Builders;
using TestUtilities.Serialization;
using TestUtilities.Scryfall;

namespace IntegrationTests.Controllers;

[Collection("Integration Tests")]
public class WishlistsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly TestDataBuilder _testDataBuilder;

    public WishlistsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _testDataBuilder = new TestDataBuilder();
    }

    [Fact]
    public async Task CreateWishlist_WithValidData_ReturnsCreatedWishlist()
    {
        var user = await CreateTestUserAsync("wishlist-creator@test.com", "wishlist_creator");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var request = new CreateWishlistDto
        {
            Name = "Commander Staples",
            Description = "Cards I need for commander decks",
            IsPublic = true
        };

        var content = JsonContentHelper.CreateContent(request);
        var response = await client.PostAsync("/api/wishlists", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();

        var wishlist = JsonSerializer.Deserialize<WishlistDto>(responseContent, JsonContentHelper.DefaultOptions);
        wishlist.Should().NotBeNull();
        wishlist!.Name.Should().Be(request.Name);
        wishlist.Description.Should().Be(request.Description);
        wishlist.IsPublic.Should().BeTrue();
        wishlist.OwnerId.Should().Be(user.Id);
        wishlist.CardsCount.Should().Be(0);
        wishlist.IndividualCardsCount.Should().Be(0);
        wishlist.TotalPrice.Should().Be(0);
        wishlist.TotalPriceCurrency.Should().BeNull();
    }

    [Fact]
    public async Task CreateWishlist_WithInvalidData_ReturnsBadRequest()
    {
        var user = await CreateTestUserAsync("wishlist-create-invalid@test.com", "wishlist_create_invalid");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var request = new CreateWishlistDto
        {
            Name = string.Empty,
            Description = "",
            IsPublic = false
        };

        var response = await client.PostAsync("/api/wishlists", JsonContentHelper.CreateContent(request));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateWishlist_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var request = new CreateWishlistDto
        {
            Name = "No Auth Wishlist",
            Description = "",
            IsPublic = true
        };

        var response = await client.PostAsync("/api/wishlists", JsonContentHelper.CreateContent(request));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetWishlists_ForUser_ReturnsOwnedWishlists()
    {
        var owner = await CreateTestUserAsync("wishlist-owner@test.com", "wishlist_owner");
        var otherUser = await CreateTestUserAsync("wishlist-other@test.com", "wishlist_other");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MainContext>();
            var ownerWishlists = new[]
            {
                _testDataBuilder.CreateWishlist(owner.Id, isPublic: false),
                _testDataBuilder.CreateWishlist(owner.Id, isPublic: true)
            };
            var otherWishlist = _testDataBuilder.CreateWishlist(otherUser.Id, isPublic: true);

            context.Wishlists.AddRange(ownerWishlists);
            context.Wishlists.Add(otherWishlist);
            await context.SaveChangesAsync();
        }

        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);
        var response = await client.GetAsync("/api/wishlists");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();

        var wishlists = JsonSerializer.Deserialize<List<WishlistSummaryDto>>(responseContent, JsonContentHelper.DefaultOptions);
        wishlists.Should().NotBeNull();
        wishlists!.Should().HaveCount(2);
        wishlists.Should().OnlyContain(w => w.CardsCount == 0 && w.IndividualCardsCount == 0 && w.TotalPrice == 0 && w.TotalPriceCurrency == null);
    }

    [Fact]
    public async Task GetWishlists_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/wishlists");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateWishlist_WithValidData_ReturnsUpdatedWishlist()
    {
        var owner = await CreateTestUserAsync("wishlist-update@test.com", "wishlist_update");
        Wishlist wishlist;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MainContext>();
            wishlist = _testDataBuilder.CreateWishlist(owner.Id, isPublic: false);
            wishlist.Name = "Original";
            context.Wishlists.Add(wishlist);
            await context.SaveChangesAsync();
        }

        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);
        var request = new UpdateWishlistDto
        {
            Name = "Updated Wishlist",
            Description = "Updated description",
            IsPublic = true
        };

        var response = await client.PutAsync($"/api/wishlists/{wishlist.Id}", JsonContentHelper.CreateContent(request));

        var payload = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, payload);
        var updatedWishlist = JsonSerializer.Deserialize<WishlistDto>(payload, JsonContentHelper.DefaultOptions);

        updatedWishlist.Should().NotBeNull();
        updatedWishlist!.Name.Should().Be(request.Name);
        updatedWishlist.Description.Should().Be(request.Description);
        updatedWishlist.IsPublic.Should().BeTrue();
        updatedWishlist.CardsCount.Should().Be(0);
        updatedWishlist.IndividualCardsCount.Should().Be(0);
        updatedWishlist.TotalPrice.Should().Be(0);
        updatedWishlist.TotalPriceCurrency.Should().BeNull();
    }

    [Fact]
    public async Task UpdateWishlist_WithInvalidData_ReturnsBadRequest()
    {
        var owner = await CreateTestUserAsync("wishlist-update-invalid@test.com", "wishlist_update_invalid");
        var wishlist = await CreateWishlistAsync(owner.Id);
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var request = new UpdateWishlistDto
        {
            Name = string.Empty,
            Description = "",
            IsPublic = false
        };

        var response = await client.PutAsync($"/api/wishlists/{wishlist.Id}", JsonContentHelper.CreateContent(request));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateWishlist_WithInvalidId_ReturnsNotFound()
    {
        var owner = await CreateTestUserAsync("wishlist-update-missing@test.com", "wishlist_update_missing");
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var request = new UpdateWishlistDto
        {
            Name = "Missing",
            Description = "",
            IsPublic = false
        };

        var response = await client.PutAsync($"/api/wishlists/{int.MaxValue}", JsonContentHelper.CreateContent(request));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateWishlist_WithoutAuthentication_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("wishlist-update-noauth@test.com", "wishlist_update_noauth");
        var wishlist = await CreateWishlistAsync(owner.Id);
        using var client = _factory.CreateClient();

        var request = new UpdateWishlistDto
        {
            Name = "Unauthorized",
            Description = "",
            IsPublic = true
        };

        var response = await client.PutAsync($"/api/wishlists/{wishlist.Id}", JsonContentHelper.CreateContent(request));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetWishlistById_WithValidData_ReturnsWishlist()
    {
        var owner = await CreateTestUserAsync("wishlist-get-owner@test.com", "wishlist_get_owner");
        var wishlist = await CreateWishlistAsync(owner.Id, isPublic: false);
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var response = await client.GetAsync($"/api/wishlists/{wishlist.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<WishlistDto>(payload, JsonContentHelper.DefaultOptions);

        dto.Should().NotBeNull();
        dto!.Id.Should().Be(wishlist.Id);
        dto.OwnerId.Should().Be(owner.Id);
    }

    [Fact]
    public async Task GetWishlistById_WithDifferentUser_ReturnsNotFound()
    {
        var owner = await CreateTestUserAsync("wishlist-get-owner2@test.com", "wishlist_get_owner2");
        var intruder = await CreateTestUserAsync("wishlist-get-intruder@test.com", "wishlist_get_intruder");
        var wishlist = await CreateWishlistAsync(owner.Id, isPublic: false);

        using var client = _factory.CreateClientWithUser(intruder.Id, intruder.UserName!, intruder.Email!);
        var response = await client.GetAsync($"/api/wishlists/{wishlist.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetWishlistById_WithInvalidId_ReturnsNotFound()
    {
        var owner = await CreateTestUserAsync("wishlist-get-missing@test.com", "wishlist_get_missing");
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var response = await client.GetAsync($"/api/wishlists/{int.MaxValue}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetWishlistById_WithoutAuthentication_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("wishlist-get-noauth@test.com", "wishlist_get_noauth");
        var wishlist = await CreateWishlistAsync(owner.Id);
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/wishlists/{wishlist.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteWishlist_RemovesWishlist()
    {
        var owner = await CreateTestUserAsync("wishlist-delete@test.com", "wishlist_delete");
        Wishlist wishlist;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MainContext>();
            wishlist = _testDataBuilder.CreateWishlist(owner.Id, isPublic: false);
            context.Wishlists.Add(wishlist);
            await context.SaveChangesAsync();
        }

        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);
        var response = await client.DeleteAsync($"/api/wishlists/{wishlist.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<MainContext>();
        var deletedWishlist = await verificationContext.Wishlists.FindAsync(wishlist.Id);
        deletedWishlist.Should().BeNull();
    }

    [Fact]
    public async Task DeleteWishlist_WithUnauthorizedUser_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("wishlist-delete-owner2@test.com", "wishlist_delete_owner2");
        var intruder = await CreateTestUserAsync("wishlist-delete-intruder@test.com", "wishlist_delete_intruder");
        var wishlist = await CreateWishlistAsync(owner.Id, isPublic: false);

        using var client = _factory.CreateClientWithUser(intruder.Id, intruder.UserName!, intruder.Email!);
        var response = await client.DeleteAsync($"/api/wishlists/{wishlist.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteWishlist_WithInvalidId_ReturnsNotFound()
    {
        var owner = await CreateTestUserAsync("wishlist-delete-missing@test.com", "wishlist_delete_missing");
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var response = await client.DeleteAsync($"/api/wishlists/{int.MaxValue}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteWishlist_WithoutAuthentication_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("wishlist-delete-noauth@test.com", "wishlist_delete_noauth");
        var wishlist = await CreateWishlistAsync(owner.Id);
        using var client = _factory.CreateClient();

        var response = await client.DeleteAsync($"/api/wishlists/{wishlist.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateWishlistCard_WithValidData_ReturnsUpdatedCard()
    {
        var owner = await CreateTestUserAsync("wishlist-card-update@test.com", "wishlist_card_update");
        Wishlist wishlist;
        WishlistCard wishlistCard;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MainContext>();
            wishlist = _testDataBuilder.CreateWishlist(owner.Id, isPublic: false);
            context.Wishlists.Add(wishlist);
            await context.SaveChangesAsync();

            wishlistCard = _testDataBuilder.CreateWishlistCard(wishlist.Id, oracleId: Guid.NewGuid().ToString(), name: "Original Card");
            wishlistCard.Notes = "Original notes";
            context.WishlistCards.Add(wishlistCard);
            await context.SaveChangesAsync();
        }

        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var request = new UpdateWishlistCardDto
        {
            DesiredQuantity = 3,
            IsFoil = true,
            Language = Language.It,
            Notes = "Updated notes"
        };

        var response = await client.PutAsync($"/api/wishlists/{wishlist.Id}/cards/{wishlistCard.Id}", JsonContentHelper.CreateContent(request));
        var payload = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, payload);
        var updatedCard = JsonSerializer.Deserialize<WishlistCardDto>(payload, JsonContentHelper.DefaultOptions);

        updatedCard.Should().NotBeNull();
        updatedCard!.Id.Should().Be(wishlistCard.Id);
        updatedCard.DesiredQuantity.Should().Be(request.DesiredQuantity);
        updatedCard.IsFoil.Should().BeTrue();
        updatedCard.Language.Should().Be(request.Language);
        updatedCard.Notes.Should().Be(request.Notes);
    }

    [Fact]
    public async Task UpdateWishlistCard_WhenUserDoesNotOwnWishlist_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("wishlist-card-owner@test.com", "wishlist_card_owner");
        var intruder = await CreateTestUserAsync("wishlist-card-intruder@test.com", "wishlist_card_intruder");
        Wishlist wishlist;
        WishlistCard wishlistCard;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MainContext>();
            wishlist = _testDataBuilder.CreateWishlist(owner.Id, isPublic: false);
            context.Wishlists.Add(wishlist);
            await context.SaveChangesAsync();

            wishlistCard = _testDataBuilder.CreateWishlistCard(wishlist.Id, oracleId: Guid.NewGuid().ToString(), name: "Protected Card");
            wishlistCard.Notes = "Protected notes";
            context.WishlistCards.Add(wishlistCard);
            await context.SaveChangesAsync();
        }

        using var client = _factory.CreateClientWithUser(intruder.Id, intruder.UserName!, intruder.Email!);

        var request = new UpdateWishlistCardDto
        {
            DesiredQuantity = 2,
            IsFoil = false
        };

        var response = await client.PutAsync($"/api/wishlists/{wishlist.Id}/cards/{wishlistCard.Id}", JsonContentHelper.CreateContent(request));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WishlistCards_FullCrudFlow_Works()
    {
        var owner = await CreateTestUserAsync("wishlist-card@test.com", "wishlist_card");
        Wishlist wishlist;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MainContext>();
            wishlist = _testDataBuilder.CreateWishlist(owner.Id, isPublic: false);
            context.Wishlists.Add(wishlist);
            await context.SaveChangesAsync();
        }

        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var createCardDto = new CreateWishlistCardDto
        {
            ScryfallId = "89f612d6-7c59-4a7b-a87d-45f789e88ba5",
            DesiredQuantity = 2,
            IsFoil = false,
            Language = Language.En,
            Notes = ""
        };
        var list = new List<CreateWishlistCardDto> { createCardDto };

        var createResponse = await client.PostAsync($"/api/wishlists/{wishlist.Id}/cards", JsonContentHelper.CreateContent(list));
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var createdPayload = await createResponse.Content.ReadAsStringAsync();
        var createdCardList = JsonSerializer.Deserialize<List<WishlistCard>>(createdPayload, JsonContentHelper.DefaultOptions);
        createdCardList.Should().NotBeNull();
        createdCardList[0].Name.Should().Be("Force of Will");

        var wishlistResponse = await client.GetAsync($"/api/wishlists/{wishlist.Id}");
        wishlistResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var wishlistPayload = await wishlistResponse.Content.ReadAsStringAsync();
        var wishlistDto = JsonSerializer.Deserialize<WishlistDto>(wishlistPayload, JsonContentHelper.DefaultOptions);
        wishlistDto.Should().NotBeNull();
        wishlistDto!.CardsCount.Should().Be(createCardDto.DesiredQuantity);
        wishlistDto.IndividualCardsCount.Should().Be(1);

        var listResponse = await client.GetAsync($"/api/wishlists/{wishlist.Id}/cards");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listPayload = await listResponse.Content.ReadAsStringAsync();
        var cards = JsonSerializer.Deserialize<List<WishlistCardDto>>(listPayload, JsonContentHelper.DefaultOptions);
        cards.Should().NotBeNull();
        cards!.Should().HaveCount(1);

        var deleteResponse = await client.DeleteAsync($"/api/wishlists/{wishlist.Id}/cards/{createdCardList[0].Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var emptyWishlistResponse = await client.GetAsync($"/api/wishlists/{wishlist.Id}");
        emptyWishlistResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var emptyWishlistPayload = await emptyWishlistResponse.Content.ReadAsStringAsync();
        var emptyWishlistDto = JsonSerializer.Deserialize<WishlistDto>(emptyWishlistPayload, JsonContentHelper.DefaultOptions);
        emptyWishlistDto.Should().NotBeNull();
        emptyWishlistDto!.CardsCount.Should().Be(0);
        emptyWishlistDto.IndividualCardsCount.Should().Be(0);

        var confirmResponse = await client.GetAsync($"/api/wishlists/{wishlist.Id}/cards");
        var confirmPayload = await confirmResponse.Content.ReadAsStringAsync();
        var remainingCards = JsonSerializer.Deserialize<List<WishlistCardDto>>(confirmPayload, JsonContentHelper.DefaultOptions);
        remainingCards.Should().NotBeNull();
        remainingCards!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetWishlistCards_WithNonOwner_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("wishlist-cards-owner@test.com", "wishlist_cards_owner");
        var intruder = await CreateTestUserAsync("wishlist-cards-intruder@test.com", "wishlist_cards_intruder");
        var wishlist = await CreateWishlistAsync(owner.Id, isPublic: false);

        using var client = _factory.CreateClientWithUser(intruder.Id, intruder.UserName!, intruder.Email!);
        var response = await client.GetAsync($"/api/wishlists/{wishlist.Id}/cards");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetWishlistCards_WithInvalidWishlist_ReturnsNotFound()
    {
        var owner = await CreateTestUserAsync("wishlist-cards-missing@test.com", "wishlist_cards_missing");
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var response = await client.GetAsync($"/api/wishlists/{int.MaxValue}/cards");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetWishlistCards_WithoutAuthentication_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("wishlist-cards-noauth@test.com", "wishlist_cards_noauth");
        var wishlist = await CreateWishlistAsync(owner.Id);
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/wishlists/{wishlist.Id}/cards");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetWishlistCardById_WithValidData_ReturnsCard()
    {
        var owner = await CreateTestUserAsync("wishlist-card-detail-owner@test.com", "wishlist_card_detail_owner");
        var wishlist = await CreateWishlistAsync(owner.Id);
        var card = await CreateWishlistCardEntityAsync(wishlist.Id);
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var response = await client.GetAsync($"/api/wishlists/{wishlist.Id}/cards/{card.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<WishlistCardDto>(payload, JsonContentHelper.DefaultOptions);

        dto.Should().NotBeNull();
        dto!.Id.Should().Be(card.Id);
        dto.WishlistId.Should().Be(wishlist.Id);
    }

    [Fact]
    public async Task GetWishlistCardById_WithInvalidCardId_ReturnsNotFound()
    {
        var owner = await CreateTestUserAsync("wishlist-card-detail-missing@test.com", "wishlist_card_detail_missing");
        var wishlist = await CreateWishlistAsync(owner.Id);
        await CreateWishlistCardEntityAsync(wishlist.Id);
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var response = await client.GetAsync($"/api/wishlists/{wishlist.Id}/cards/{int.MaxValue}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetWishlistCardById_WithNonOwner_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("wishlist-card-detail-owner2@test.com", "wishlist_card_detail_owner2");
        var intruder = await CreateTestUserAsync("wishlist-card-detail-intruder@test.com", "wishlist_card_detail_intruder");
        var wishlist = await CreateWishlistAsync(owner.Id);
        var card = await CreateWishlistCardEntityAsync(wishlist.Id);

        using var client = _factory.CreateClientWithUser(intruder.Id, intruder.UserName!, intruder.Email!);
        var response = await client.GetAsync($"/api/wishlists/{wishlist.Id}/cards/{card.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetWishlistCardById_WithoutAuthentication_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("wishlist-card-detail-noauth@test.com", "wishlist_card_detail_noauth");
        var wishlist = await CreateWishlistAsync(owner.Id);
        var card = await CreateWishlistCardEntityAsync(wishlist.Id);
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/wishlists/{wishlist.Id}/cards/{card.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateWishlistCard_WithInvalidData_ReturnsBadRequest()
    {
        var owner = await CreateTestUserAsync("wishlist-card-create-invalid@test.com", "wishlist_card_create_invalid");
        var wishlist = await CreateWishlistAsync(owner.Id);
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var request = new List<CreateWishlistCardDto>
        {
            new()
            {
                ScryfallId = string.Empty,
                DesiredQuantity = 0,
                Notes = string.Empty
            }
        };

        var response = await client.PostAsync($"/api/wishlists/{wishlist.Id}/cards", JsonContentHelper.CreateContent(request));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateWishlistCard_WithNonOwner_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("wishlist-card-create-owner@test.com", "wishlist_card_create_owner");
        var intruder = await CreateTestUserAsync("wishlist-card-create-intruder@test.com", "wishlist_card_create_intruder");
        var wishlist = await CreateWishlistAsync(owner.Id);

        using var client = _factory.CreateClientWithUser(intruder.Id, intruder.UserName!, intruder.Email!);
        var request = new List<CreateWishlistCardDto>
        {
            new()
            {
                ScryfallId = "89f612d6-7c59-4a7b-a87d-45f789e88ba5",
                DesiredQuantity = 1,
                Notes = "test"
            }
        };

        var response = await client.PostAsync($"/api/wishlists/{wishlist.Id}/cards", JsonContentHelper.CreateContent(request));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateWishlistCard_WithInvalidWishlist_ReturnsNotFound()
    {
        var owner = await CreateTestUserAsync("wishlist-card-create-missing@test.com", "wishlist_card_create_missing");
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var request = new List<CreateWishlistCardDto>
        {
            new()
            {
                ScryfallId = "89f612d6-7c59-4a7b-a87d-45f789e88ba5",
                DesiredQuantity = 1,
                Notes = "test"
            }
        };

        var response = await client.PostAsync($"/api/wishlists/{int.MaxValue}/cards", JsonContentHelper.CreateContent(request));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateWishlistCard_RecalculatesTotalPrice()
    {
        var owner = await CreateTestUserAsync("wishlist-card-price-create@test.com", "wishlist_card_price_create");
        var wishlist = await CreateWishlistAsync(owner.Id);

        var pricedCard = _testDataBuilder.CreateOracleCard(id: "wishlist-price-1", name: "Wishlist Price", setCode: "TST", setName: "Test Set") with
        {
            Prices = new Prices("2.00", "2.50", "1.50", "1.80", null)
        };
        await SeedCardDataAsync(new[] { pricedCard });

        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);
        var request = new List<CreateWishlistCardDto>
        {
            new()
            {
                ScryfallId = pricedCard.Id,
                DesiredQuantity = 3,
                Notes = string.Empty
            }
        };

        var response = await client.PostAsync($"/api/wishlists/{wishlist.Id}/cards", JsonContentHelper.CreateContent(request));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var wishlistResponse = await client.GetAsync($"/api/wishlists/{wishlist.Id}");
        wishlistResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await wishlistResponse.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<WishlistDto>(payload, JsonContentHelper.DefaultOptions);

        dto.Should().NotBeNull();
        dto!.TotalPrice.Should().Be(4.5);
        dto.TotalPriceCurrency.Should().Be(Currency.Eur);
    }

    [Fact]
    public async Task CreateWishlistCard_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();
        var request = new List<CreateWishlistCardDto>
        {
            new()
            {
                ScryfallId = "89f612d6-7c59-4a7b-a87d-45f789e88ba5",
                DesiredQuantity = 1,
                Notes = "test"
            }
        };

        var response = await client.PostAsync($"/api/wishlists/{int.MaxValue}/cards", JsonContentHelper.CreateContent(request));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateWishlistCard_WithInvalidData_ReturnsBadRequest()
    {
        var owner = await CreateTestUserAsync("wishlist-card-update-invalid@test.com", "wishlist_card_update_invalid");
        var wishlist = await CreateWishlistAsync(owner.Id);
        var card = await CreateWishlistCardEntityAsync(wishlist.Id);
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var request = new UpdateWishlistCardDto
        {
            DesiredQuantity = 0,
            Notes = string.Empty
        };

        var response = await client.PutAsync($"/api/wishlists/{wishlist.Id}/cards/{card.Id}", JsonContentHelper.CreateContent(request));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateWishlistCard_WithInvalidCardId_ReturnsNotFound()
    {
        var owner = await CreateTestUserAsync("wishlist-card-update-missing@test.com", "wishlist_card_update_missing");
        var wishlist = await CreateWishlistAsync(owner.Id);
        await CreateWishlistCardEntityAsync(wishlist.Id);
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var request = new UpdateWishlistCardDto
        {
            DesiredQuantity = 1,
            Notes = "notes"
        };

        var response = await client.PutAsync($"/api/wishlists/{wishlist.Id}/cards/{int.MaxValue}", JsonContentHelper.CreateContent(request));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateWishlistCard_RecalculatesTotalPrice()
    {
        var owner = await CreateTestUserAsync("wishlist-card-price-update@test.com", "wishlist_card_price_update");
        var wishlist = await CreateWishlistAsync(owner.Id);

        var pricedCard = _testDataBuilder.CreateOracleCard(id: "wishlist-price-2", name: "Wishlist Update Price", setCode: "TST", setName: "Test Set") with
        {
            Prices = new Prices("2.00", "2.50", "1.50", "1.80", null)
        };
        await SeedCardDataAsync(new[] { pricedCard });

        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);
        var createRequest = new List<CreateWishlistCardDto>
        {
            new()
            {
                ScryfallId = pricedCard.Id,
                DesiredQuantity = 1
            }
        };

        var createResponse = await client.PostAsync($"/api/wishlists/{wishlist.Id}/cards", JsonContentHelper.CreateContent(createRequest));
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var createdPayload = await createResponse.Content.ReadAsStringAsync();
        var createdCards = JsonSerializer.Deserialize<List<WishlistCard>>(createdPayload, JsonContentHelper.DefaultOptions);
        createdCards.Should().NotBeNull();

        var updateDto = new UpdateWishlistCardDto
        {
            DesiredQuantity = 4
        };

        var updateResponse = await client.PutAsync($"/api/wishlists/{wishlist.Id}/cards/{createdCards![0].Id}", JsonContentHelper.CreateContent(updateDto));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var wishlistResponse = await client.GetAsync($"/api/wishlists/{wishlist.Id}");
        var payload = await wishlistResponse.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<WishlistDto>(payload, JsonContentHelper.DefaultOptions);

        dto.Should().NotBeNull();
        dto!.TotalPrice.Should().Be(6.0);
        dto.TotalPriceCurrency.Should().Be(Currency.Eur);
    }

    [Fact]
    public async Task UpdateWishlistCard_WithoutAuthentication_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("wishlist-card-update-noauth@test.com", "wishlist_card_update_noauth");
        var wishlist = await CreateWishlistAsync(owner.Id);
        var card = await CreateWishlistCardEntityAsync(wishlist.Id);
        using var client = _factory.CreateClient();

        var request = new UpdateWishlistCardDto
        {
            DesiredQuantity = 1,
            Notes = "notes"
        };

        var response = await client.PutAsync($"/api/wishlists/{wishlist.Id}/cards/{card.Id}", JsonContentHelper.CreateContent(request));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteWishlistCard_WithNonOwner_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("wishlist-card-delete-owner@test.com", "wishlist_card_delete_owner");
        var intruder = await CreateTestUserAsync("wishlist-card-delete-intruder@test.com", "wishlist_card_delete_intruder");
        var wishlist = await CreateWishlistAsync(owner.Id);
        var card = await CreateWishlistCardEntityAsync(wishlist.Id);

        using var client = _factory.CreateClientWithUser(intruder.Id, intruder.UserName!, intruder.Email!);
        var response = await client.DeleteAsync($"/api/wishlists/{wishlist.Id}/cards/{card.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteWishlistCard_WithInvalidId_ReturnsNotFound()
    {
        var owner = await CreateTestUserAsync("wishlist-card-delete-missing@test.com", "wishlist_card_delete_missing");
        var wishlist = await CreateWishlistAsync(owner.Id);
        await CreateWishlistCardEntityAsync(wishlist.Id);
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var response = await client.DeleteAsync($"/api/wishlists/{wishlist.Id}/cards/{int.MaxValue}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteWishlistCard_WithoutAuthentication_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("wishlist-card-delete-noauth@test.com", "wishlist_card_delete_noauth");
        var wishlist = await CreateWishlistAsync(owner.Id);
        var card = await CreateWishlistCardEntityAsync(wishlist.Id);
        using var client = _factory.CreateClient();

        var response = await client.DeleteAsync($"/api/wishlists/{wishlist.Id}/cards/{card.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteWishlistCard_RecalculatesTotalPrice()
    {
        var owner = await CreateTestUserAsync("wishlist-card-price-delete@test.com", "wishlist_card_price_delete");
        var wishlist = await CreateWishlistAsync(owner.Id);

        var pricedCard = _testDataBuilder.CreateOracleCard(id: "wishlist-price-3", name: "Wishlist Delete Price", setCode: "TST", setName: "Test Set") with
        {
            Prices = new Prices("2.00", "2.50", "1.50", "1.80", null)
        };
        await SeedCardDataAsync(new[] { pricedCard });

        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);
        var createRequest = new List<CreateWishlistCardDto>
        {
            new()
            {
                ScryfallId = pricedCard.Id,
                DesiredQuantity = 2
            }
        };

        var createResponse = await client.PostAsync($"/api/wishlists/{wishlist.Id}/cards", JsonContentHelper.CreateContent(createRequest));
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var createdPayload = await createResponse.Content.ReadAsStringAsync();
        var createdCards = JsonSerializer.Deserialize<List<WishlistCard>>(createdPayload, JsonContentHelper.DefaultOptions);
        createdCards.Should().NotBeNull();

        var deleteResponse = await client.DeleteAsync($"/api/wishlists/{wishlist.Id}/cards/{createdCards![0].Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var wishlistResponse = await client.GetAsync($"/api/wishlists/{wishlist.Id}");
        var payload = await wishlistResponse.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<WishlistDto>(payload, JsonContentHelper.DefaultOptions);

        dto.Should().NotBeNull();
        dto!.TotalPrice.Should().Be(0);
        dto.TotalPriceCurrency.Should().BeNull();
    }

    private Task<AppUser> CreateTestUserAsync(string email, string userName) =>
        TestUserFactory.CreateAsync(_factory.Services, _testDataBuilder, email, userName, requirePassword: true);

    private async Task<Wishlist> CreateWishlistAsync(string ownerId, bool isPublic = false)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MainContext>();
        var wishlist = _testDataBuilder.CreateWishlist(ownerId, isPublic);
        context.Wishlists.Add(wishlist);
        await context.SaveChangesAsync();
        return wishlist;
    }

    private async Task<WishlistCard> CreateWishlistCardEntityAsync(int wishlistId, string? oracleId = null, string? name = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MainContext>();
        var card = _testDataBuilder.CreateWishlistCard(wishlistId, oracleId ?? Guid.NewGuid().ToString(), name ?? "Test Wishlist Card");
        context.WishlistCards.Add(card);
        await context.SaveChangesAsync();
        return card;
    }

    private Task SeedCardDataAsync(IEnumerable<ScryfallCardDto> cards)
    {
        using var scope = _factory.Services.CreateScope();
        var cardDataService = scope.ServiceProvider.GetRequiredService<CardDataService>();
        foreach (var card in cards)
        {
            cardDataService.CardDataById[card.Id] = card;
            cardDataService.CardDataByName[card.Name] = card;
        }

        return Task.CompletedTask;
    }
}

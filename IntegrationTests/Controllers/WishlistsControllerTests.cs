using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Json;
using API.Dtos.Wishlists;
using Core.Models;
using Core.Models.Identity;
using FluentAssertions;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using TestUtilities.Builders;

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

        var content = Serialize(request);
        var response = await client.PostAsync("/api/wishlists", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();

        var wishlist = JsonSerializer.Deserialize<WishlistDto>(responseContent, JsonOptions);
        wishlist.Should().NotBeNull();
        wishlist!.Name.Should().Be(request.Name);
        wishlist.Description.Should().Be(request.Description);
        wishlist.IsPublic.Should().BeTrue();
        wishlist.OwnerId.Should().Be(user.Id);
        wishlist.CardsCount.Should().Be(0);
        wishlist.IndividualCardsCount.Should().Be(0);
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

        var wishlists = JsonSerializer.Deserialize<List<WishlistSummaryDto>>(responseContent, JsonOptions);
        wishlists.Should().NotBeNull();
        wishlists!.Should().HaveCount(2);
        wishlists.Should().OnlyContain(w => w.CardsCount == 0 && w.IndividualCardsCount == 0);
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

        var response = await client.PutAsync($"/api/wishlists/{wishlist.Id}", Serialize(request));

        var payload = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, payload);
        var updatedWishlist = JsonSerializer.Deserialize<WishlistDto>(payload, JsonOptions);

        updatedWishlist.Should().NotBeNull();
        updatedWishlist!.Name.Should().Be(request.Name);
        updatedWishlist.Description.Should().Be(request.Description);
        updatedWishlist.IsPublic.Should().BeTrue();
        updatedWishlist.CardsCount.Should().Be(0);
        updatedWishlist.IndividualCardsCount.Should().Be(0);
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
            Name = "Updated Card Name",
            SetCode = "SET",
            SetName = "Updated Set",
            ImageUrl = "https://example.com/updated-card.jpg",
            CollectorNumber = "123",
            Rarity = "Rare",
            DesiredQuantity = 3,
            IsFoil = true,
            Language = "es",
            Notes = "Updated notes"
        };

        var response = await client.PutAsync($"/api/wishlists/{wishlist.Id}/cards/{wishlistCard.Id}", Serialize(request));
        var payload = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, payload);
        var updatedCard = JsonSerializer.Deserialize<WishlistCardDto>(payload, JsonOptions);

        updatedCard.Should().NotBeNull();
        updatedCard!.Id.Should().Be(wishlistCard.Id);
        updatedCard.Name.Should().Be(request.Name);
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
            Name = "Intruder Update",
            SetCode = "SET",
            DesiredQuantity = 2,
            IsFoil = false
        };

        var response = await client.PutAsync($"/api/wishlists/{wishlist.Id}/cards/{wishlistCard.Id}", Serialize(request));

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
            OracleId = "89f612d6-7c59-4a7b-a87d-45f789e88ba5",
            DesiredQuantity = 2,
            IsFoil = false,
            Language = "en",
            Notes = ""
        };
        var list = new List<CreateWishlistCardDto> { createCardDto };

        var createResponse = await client.PostAsync($"/api/wishlists/{wishlist.Id}/cards", Serialize(list));
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var createdPayload = await createResponse.Content.ReadAsStringAsync();
        var createdCardList = JsonSerializer.Deserialize<List<WishlistCard>>(createdPayload, JsonOptions);
        createdCardList.Should().NotBeNull();
        createdCardList[0].Name.Should().Be("Force of Will");

        var wishlistResponse = await client.GetAsync($"/api/wishlists/{wishlist.Id}");
        wishlistResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var wishlistPayload = await wishlistResponse.Content.ReadAsStringAsync();
        var wishlistDto = JsonSerializer.Deserialize<WishlistDto>(wishlistPayload, JsonOptions);
        wishlistDto.Should().NotBeNull();
        wishlistDto!.CardsCount.Should().Be(createCardDto.DesiredQuantity);
        wishlistDto.IndividualCardsCount.Should().Be(1);

        var listResponse = await client.GetAsync($"/api/wishlists/{wishlist.Id}/cards");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listPayload = await listResponse.Content.ReadAsStringAsync();
        var cards = JsonSerializer.Deserialize<List<WishlistCardDto>>(listPayload, JsonOptions);
        cards.Should().NotBeNull();
        cards!.Should().HaveCount(1);

        var deleteResponse = await client.DeleteAsync($"/api/wishlists/{wishlist.Id}/cards/{createdCardList[0].Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var emptyWishlistResponse = await client.GetAsync($"/api/wishlists/{wishlist.Id}");
        emptyWishlistResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var emptyWishlistPayload = await emptyWishlistResponse.Content.ReadAsStringAsync();
        var emptyWishlistDto = JsonSerializer.Deserialize<WishlistDto>(emptyWishlistPayload, JsonOptions);
        emptyWishlistDto.Should().NotBeNull();
        emptyWishlistDto!.CardsCount.Should().Be(0);
        emptyWishlistDto.IndividualCardsCount.Should().Be(0);

        var confirmResponse = await client.GetAsync($"/api/wishlists/{wishlist.Id}/cards");
        var confirmPayload = await confirmResponse.Content.ReadAsStringAsync();
        var remainingCards = JsonSerializer.Deserialize<List<WishlistCardDto>>(confirmPayload, JsonOptions);
        remainingCards.Should().NotBeNull();
        remainingCards!.Should().BeEmpty();
    }

    private static StringContent Serialize<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private async Task<AppUser> CreateTestUserAsync(string email, string userName)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = _testDataBuilder.CreateUser(email, userName);
        var result = await userManager.CreateAsync(user, "Password123!");
        result.Succeeded.Should().BeTrue();
        return user;
    }
}

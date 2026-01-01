using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using API.Dtos.Trades;
using Core.Models;
using Core.Models.Identity;
using FluentAssertions;
using Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using TestUtilities.Authentication;
using TestUtilities.Builders;
using TestUtilities.Serialization;

namespace IntegrationTests.Controllers;

[Collection("Integration Tests")]
public class TradesControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory factory;
    private readonly TestDataBuilder testDataBuilder;

    public TradesControllerTests(CustomWebApplicationFactory factory)
    {
        this.factory = factory;
        testDataBuilder = new TestDataBuilder();
    }

    [Fact]
    public async Task Match_ReturnsWishlistAndBinderMatches()
    {
        var initiator = await CreateTestUserAsync("trade-initiator@test.com", "trade_initiator");
        var partner = await CreateTestUserAsync("trade-partner@test.com", "trade_partner");

        await CreatePublicBinderCardAsync(initiator.Id, "Shared Match");
        await CreatePublicWishlistCardAsync(partner.Id, "Shared Match");

        using var client = factory.CreateClientWithUser(initiator.Id, initiator.UserName!, initiator.Email!);

        var response = await client.GetAsync($"/api/trades/match?initiatorUserId={initiator.Id}&partnerUserId={partner.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<TradeConnectionDto>(payload, JsonContentHelper.DefaultOptions);

        dto.Should().NotBeNull();
        dto!.InitiatorMatches.Should().ContainSingle(m => m.OfferingCard.Name == "Shared Match" && m.ToUserId == partner.Id);
        dto.PartnerMatches.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTradeSession_WithUnauthorizedUser_ReturnsForbidden()
    {
        var initiator = await CreateTestUserAsync("trade-session-owner@test.com", "trade_session_owner");
        var partner = await CreateTestUserAsync("trade-session-partner@test.com", "trade_session_partner");
        var intruder = await CreateTestUserAsync("trade-session-intruder@test.com", "trade_session_intruder");

        await CreatePublicBinderCardAsync(initiator.Id, "Force of Will");
        await CreatePublicWishlistCardAsync(partner.Id, "Force of Will");

        using var initiatorClient = factory.CreateClientWithUser(initiator.Id, initiator.UserName!, initiator.Email!);
        var matchResponse = await initiatorClient.GetAsync($"/api/trades/match?initiatorUserId={initiator.Id}&partnerUserId={partner.Id}");
        matchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var tradeDto = await matchResponse.Content.ReadFromJsonAsync<TradeConnectionDto>(JsonContentHelper.DefaultOptions);
        tradeDto.Should().NotBeNull();

        using var intruderClient = factory.CreateClientWithUser(intruder.Id, intruder.UserName!, intruder.Email!);
        var getResponse = await intruderClient.GetAsync($"/api/trades/{tradeDto!.TradeId}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<AppUser> CreateTestUserAsync(string email, string userName) =>
        await TestUserFactory.CreateAsync(factory.Services, testDataBuilder, email, userName, requirePassword: true);

    private async Task CreatePublicWishlistCardAsync(string ownerId, string cardName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MainContext>();

        var wishlist = testDataBuilder.CreateWishlist(ownerId, isPublic: true);
        context.Wishlists.Add(wishlist);
        await context.SaveChangesAsync();

        var card = testDataBuilder.CreateWishlistCard(wishlist.Id, Guid.NewGuid().ToString(), cardName);
        card.WishlistId = wishlist.Id;
        context.WishlistCards.Add(card);
        await context.SaveChangesAsync();
    }

    private async Task CreatePublicBinderCardAsync(string ownerId, string cardName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MainContext>();

        var collection = testDataBuilder.CreateCollection(ownerId);
        context.Collections.Add(collection);
        await context.SaveChangesAsync();

        var ownedCard = testDataBuilder.CreateCard(collection.Id, cardName);
        context.Cards.Add(ownedCard);
        await context.SaveChangesAsync();

        var binder = testDataBuilder.CreateTradeBinder(ownerId, isPublic: true);
        context.TradeBinders.Add(binder);
        await context.SaveChangesAsync();

        var binderCard = new BinderCard
        {
            TradeBinderId = binder.Id,
            CardId = ownedCard.Id,
            Name = cardName,
            QuantityToTrade = 1,
            Notes = "integration-test",
            Card = ownedCard
        };
        context.BinderCards.Add(binderCard);
        await context.SaveChangesAsync();
    }
}

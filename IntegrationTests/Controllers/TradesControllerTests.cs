using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using API;
using API.Dtos.Trades;
using API.Services;
using Core.Models;
using Core.Models.Identity;
using FluentAssertions;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TestUtilities.Authentication;
using TestUtilities.Builders;
using TestUtilities.Serialization;

namespace IntegrationTests.Controllers;

[Collection("Integration Tests")]
public class TradesControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly TestDataBuilder _testDataBuilder;

    public TradesControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _testDataBuilder = new TestDataBuilder();
    }

    [Fact]
    public async Task Match_ReturnsWishlistAndBinderMatches()
    {
        var initiator = await CreateTestUserAsync("trade-initiator@test.com", "trade_initiator");
        var partner = await CreateTestUserAsync("trade-partner@test.com", "trade_partner");

        await CreatePublicBinderCardAsync(initiator.Id, "Lightning Bolt");
        await CreatePublicWishlistCardAsync(partner.Id, "Lightning Bolt");

        using var client = _factory.CreateClientWithUser(initiator.Id, initiator.UserName!, initiator.Email!);

        var response = await client.GetAsync($"/api/trades/match?initiatorUserId={initiator.Id}&partnerUserId={partner.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<TradeConnectionDto>(payload, JsonContentHelper.DefaultOptions);

        dto.Should().NotBeNull();
        dto.InitiatorMatches.Should().ContainSingle(m => m.OfferingCard.Name == "Lightning Bolt" && m.ToUserId == partner.Id);
        dto.PartnerMatches.Should().BeEmpty();
        dto.InitiatorTotalValue.Should().BeGreaterThan(0);
        dto.ValueDifference.Should().Be(dto.InitiatorTotalValue - dto.PartnerTotalValue);
        dto.InitiatorMatches.Single().OfferingCard.MarketPrice.Should().NotBeNull();
        dto.InitiatorMatches.Single().IsSelected.Should().BeTrue();
    }

    [Fact]
    public async Task Match_WhenPartnerUserIdMissing_ReturnsBadRequest()
    {
        var initiator = await CreateTestUserAsync("trade-initiator@test.com", "trade_initiator");
        using var client = CreateClientWithUser(_factory, initiator);

        var response = await client.GetAsync($"/api/trades/match?initiatorUserId={initiator.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Match_WhenJwtDoesNotMatchInitiator_ReturnsUnauthorized()
    {
        var initiator = await CreateTestUserAsync("trade-initiator@test.com", "trade_initiator");
        var partner = await CreateTestUserAsync("trade-partner@test.com", "trade_partner");

        using var client = CreateClientWithUser(_factory, partner);
        var response = await client.GetAsync($"/api/trades/match?initiatorUserId={initiator.Id}&partnerUserId={partner.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Match_WhenServiceReportsMissingTrade_ReturnsNotFound()
    {
        using var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ITradeConnectionService>();
                services.AddSingleton<ITradeConnectionService>(new NotFoundTradeConnectionService());
            });
        });

        var initiator = await CreateTestUserAsync("trade-initiator@test.com", "trade_initiator", customFactory.Services);
        var partner = await CreateTestUserAsync("trade-partner@test.com", "trade_partner", customFactory.Services);

        using var client = CreateClientWithUser(customFactory, initiator);
        var response = await client.GetAsync($"/api/trades/match?initiatorUserId={initiator.Id}&partnerUserId={partner.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RequestTrade_CreatesNotificationForTargetUser()
    {
        var requester = await CreateTestUserAsync("trade-requester@test.com", "trade_requester");
        var target = await CreateTestUserAsync("trade-target@test.com", "trade_target");

        using var client = CreateClientWithUser(_factory, requester);
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/trades/{target.Id}/request"));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        await using var scope = _factory.Services.CreateAsyncScope();
        var identityContext = scope.ServiceProvider.GetRequiredService<MainContext>();
        var notification = await identityContext.Notifications.SingleAsync(n => n.AppUserId == target.Id);

        notification.Name.Should().Be("trade_request");
        notification.MessageKey.Should().Be("Notifications.TradeRequest");
        notification.MessageArgsJson.Should().Contain(requester.DisplayName);
        notification.ObjectId.Should().Be(requester.Id);
        notification.Origin.Should().Be($"trade_request.{requester.Id}");
    }

    [Fact]
    public async Task RequestTrade_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var target = await CreateTestUserAsync("trade-target@test.com", "trade_target");
        using var client = _factory.CreateClient();

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/trades/{target.Id}/request"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RequestTrade_WhenRequestingSelf_ReturnsBadRequest()
    {
        var requester = await CreateTestUserAsync("trade-requester@test.com", "trade_requester");

        using var client = CreateClientWithUser(_factory, requester);
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/trades/{requester.Id}/request"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RequestTrade_WhenRequestedUserMissing_ReturnsNotFound()
    {
        var requester = await CreateTestUserAsync("trade-requester@test.com", "trade_requester");

        using var client = CreateClientWithUser(_factory, requester);
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/trades/{Guid.NewGuid():N}/request"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Fact]
    public async Task RequestCommit_CreatesNotificationForOtherParticipant()
    {
        var (initiator, partner, trade) = await PrepareTradeSessionAsync();

        using var client = CreateClientWithUser(_factory, initiator);
        var response = await client.PostAsync($"/api/trades/{trade.TradeId}/request-commit", null);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        await using var scope = _factory.Services.CreateAsyncScope();
        var identityContext = scope.ServiceProvider.GetRequiredService<MainContext>();
        var notification = await identityContext.Notifications.SingleAsync(n =>
            n.AppUserId == partner.Id && n.ObjectId == trade.TradeId && n.Name == "trade_commit_request");
        notification.MessageKey.Should().Be("Notifications.TradeCommitRequest");
        notification.MessageArgsJson.Should().Contain(initiator.DisplayName);
    }

    [Fact]
    public async Task RequestCommit_WhenNotParticipant_ReturnsForbidden()
    {
        var (_, _, trade) = await PrepareTradeSessionAsync();
        var intruder = await CreateTestUserAsync("trade-intruder@test.com", "trade_intruder");

        using var client = CreateClientWithUser(_factory, intruder);
        var response = await client.PostAsync($"/api/trades/{trade.TradeId}/request-commit", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RequestCommit_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var (_, _, trade) = await PrepareTradeSessionAsync();
        using var client = _factory.CreateClient();

        var response = await client.PostAsync($"/api/trades/{trade.TradeId}/request-commit", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Match_WhenLiveTradingDisabledWithoutApproval_ReturnsForbidden()
    {
        var initiator = await CreateTestUserAsync("trade-initiator@test.com", "trade_initiator");
        var partner = await CreateTestUserAsync("trade-partner@test.com", "trade_partner");

        using var client = CreateClientWithUser(_factory, initiator);
        var response = await client.GetAsync($"/api/trades/match?initiatorUserId={initiator.Id}&partnerUserId={partner.Id}&liveTrading=false");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Match_WhenLiveTradingDisabledWithApproval_ReturnsOk()
    {
        var initiator = await CreateTestUserAsync("trade-initiator@test.com", "trade_initiator");
        var partner = await CreateTestUserAsync("trade-partner@test.com", "trade_partner");

        await CreatePublicBinderCardAsync(initiator.Id, "Lightning Bolt");
        await CreatePublicWishlistCardAsync(partner.Id, "Lightning Bolt");
        await CreateApprovedTradeNotificationAsync(initiator.Id, partner.Id);

        using var client = CreateClientWithUser(_factory, initiator);
        var response = await client.GetAsync($"/api/trades/match?initiatorUserId={initiator.Id}&partnerUserId={partner.Id}&liveTrading=false");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
    
    [Fact]
    public async Task UpdateTrade_WhenNotLive_RemovesCommitNotifications()
    {
        var (initiator, _, trade) = await PrepareTradeSessionAsync(liveTrading: false, requireApproval: true);

        using var initiatorClient = CreateClientWithUser(_factory, initiator);
        var requestCommitResponse = await initiatorClient.PostAsync($"/api/trades/{trade.TradeId}/request-commit", null);
        requestCommitResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var matchId = trade.InitiatorMatches[0].MatchId;
        var updateRequest = new UpdateTradeRequest
        {
            InitiatorMatches =
            [
                new TradeMatchUpdateDto
                {
                    MatchId = matchId,
                    QuantityToTrade = 0
                }
            ]
        };

        var updateResponse = await initiatorClient.PutAsJsonAsync(
            $"/api/trades/{trade.TradeId}",
            updateRequest,
            JsonContentHelper.DefaultOptions);

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var scope = _factory.Services.CreateAsyncScope();
        var identityContext = scope.ServiceProvider.GetRequiredService<MainContext>();
        var pendingNotifications = await identityContext.Notifications
            .Where(n => n.Name == "trade_commit_request" && n.ObjectId == trade.TradeId)
            .ToListAsync();

        pendingNotifications.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTradeSession_WithUnauthorizedUser_ReturnsForbidden()
    {
        var initiator = await CreateTestUserAsync("trade-session-owner@test.com", "trade_session_owner");
        var partner = await CreateTestUserAsync("trade-session-partner@test.com", "trade_session_partner");
        var intruder = await CreateTestUserAsync("trade-session-intruder@test.com", "trade_session_intruder");

        await CreatePublicBinderCardAsync(initiator.Id, "Force of Will");
        await CreatePublicWishlistCardAsync(partner.Id, "Force of Will");

        using var initiatorClient = _factory.CreateClientWithUser(initiator.Id, initiator.UserName!, initiator.Email!);
        var matchResponse = await initiatorClient.GetAsync($"/api/trades/match?initiatorUserId={initiator.Id}&partnerUserId={partner.Id}");
        matchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var tradeDto = await matchResponse.Content.ReadFromJsonAsync<TradeConnectionDto>(JsonContentHelper.DefaultOptions);
        tradeDto.Should().NotBeNull();

        using var intruderClient = _factory.CreateClientWithUser(intruder.Id, intruder.UserName!, intruder.Email!);
        var getResponse = await intruderClient.GetAsync($"/api/trades/{tradeDto.TradeId}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetTradeSession_ReturnsSnapshotForParticipant()
    {
        var (_, partner, trade) = await PrepareTradeSessionAsync();

        using var partnerClient = CreateClientWithUser(_factory, partner);
        var response = await partnerClient.GetAsync($"/api/trades/{trade.TradeId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<TradeConnectionDto>(JsonContentHelper.DefaultOptions);
        dto.Should().NotBeNull();
        dto.TradeId.Should().Be(trade.TradeId);
        dto.PartnerMatches.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTradeSession_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var (_, _, trade) = await PrepareTradeSessionAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/trades/{trade.TradeId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTradeSession_WhenTradeDoesNotExist_ReturnsNotFound()
    {
        var user = await CreateTestUserAsync("trade-session-owner@test.com", "trade_session_owner");
        using var client = CreateClientWithUser(_factory, user);

        var response = await client.GetAsync($"/api/trades/{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateTradeSession_AllowsParticipantToAdjustSelection()
    {
        var (initiator, _, trade) = await PrepareTradeSessionAsync();
        using var initiatorClient = CreateClientWithUser(_factory, initiator);

        var matchId = trade.InitiatorMatches[0].MatchId;
        var request = new UpdateTradeRequest
        {
            InitiatorMatches =
            [
                new TradeMatchUpdateDto
                {
                    MatchId = matchId,
                    QuantityToTrade = 0,
                    IsSelected = false
                }
            ]
        };

        var response = await initiatorClient.PutAsJsonAsync($"/api/trades/{trade.TradeId}", request, JsonContentHelper.DefaultOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<TradeConnectionDto>(JsonContentHelper.DefaultOptions);
        updated!.InitiatorMatches.Single().IsSelected.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateTradeSession_WhenQuantityTooHigh_ReturnsBadRequest()
    {
        var (initiator, _, trade) = await PrepareTradeSessionAsync(collectionQuantity: 2);
        using var initiatorClient = CreateClientWithUser(_factory, initiator);

        var matchId = trade.InitiatorMatches[0].MatchId;
        var request = new UpdateTradeRequest
        {
            InitiatorMatches =
            [
                new TradeMatchUpdateDto
                {
                    MatchId = matchId,
                    QuantityToTrade = 5
                }
            ]
        };

        var response = await initiatorClient.PutAsJsonAsync($"/api/trades/{trade.TradeId}", request, JsonContentHelper.DefaultOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateTradeSession_WhenRequesterNotParticipant_ReturnsForbidden()
    {
        var (_, _, trade) = await PrepareTradeSessionAsync();
        var intruder = await CreateTestUserAsync("trade-intruder@test.com", "trade_intruder");
        using var intruderClient = CreateClientWithUser(_factory, intruder);

        var matchId = trade.InitiatorMatches[0].MatchId;
        var request = new UpdateTradeRequest
        {
            InitiatorMatches =
            [
                new TradeMatchUpdateDto
                {
                    MatchId = matchId,
                    QuantityToTrade = 1
                }
            ]
        };

        var response = await intruderClient.PutAsJsonAsync($"/api/trades/{trade.TradeId}", request, JsonContentHelper.DefaultOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateTradeSession_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var (_, _, trade) = await PrepareTradeSessionAsync();
        using var client = _factory.CreateClient();

        var request = new UpdateTradeRequest
        {
            InitiatorMatches =
            [
                new TradeMatchUpdateDto
                {
                    MatchId = trade.InitiatorMatches[0].MatchId,
                    QuantityToTrade = 1
                }
            ]
        };

        var response = await client.PutAsJsonAsync($"/api/trades/{trade.TradeId}", request, JsonContentHelper.DefaultOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateTradeSession_WhenTradeMissing_ReturnsNotFound()
    {
        var initiator = await CreateTestUserAsync("trade-initiator@test.com", "trade_initiator");
        using var client = CreateClientWithUser(_factory, initiator);

        var request = new UpdateTradeRequest
        {
            InitiatorMatches = Array.Empty<TradeMatchUpdateDto>()
        };

        var response = await client.PutAsJsonAsync($"/api/trades/{Guid.NewGuid():N}", request, JsonContentHelper.DefaultOptions);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CancelTradeSession_AllowsParticipantToDelete()
    {
        var (initiator, _, trade) = await PrepareTradeSessionAsync();
        using var client = CreateClientWithUser(_factory, initiator);

        var response = await client.DeleteAsync($"/api/trades/{trade.TradeId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await client.GetAsync($"/api/trades/{trade.TradeId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CancelTradeSession_WhenRequesterNotParticipant_ReturnsForbidden()
    {
        var (_, _, trade) = await PrepareTradeSessionAsync();
        var intruder = await CreateTestUserAsync("trade-intruder@test.com", "trade_intruder");
        using var client = CreateClientWithUser(_factory, intruder);

        var response = await client.DeleteAsync($"/api/trades/{trade.TradeId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CancelTradeSession_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var (_, _, trade) = await PrepareTradeSessionAsync();
        using var client = _factory.CreateClient();

        var response = await client.DeleteAsync($"/api/trades/{trade.TradeId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CancelTradeSession_WhenTradeMissing_ReturnsNotFound()
    {
        var initiator = await CreateTestUserAsync("trade-initiator@test.com", "trade_initiator");
        using var client = CreateClientWithUser(_factory, initiator);

        var response = await client.DeleteAsync($"/api/trades/{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CommitTradeSession_CompletesAndRemovesSession()
    {
        var (initiator, partner, trade) = await PrepareTradeSessionAsync(includePartnerOffer: true);
        var initiatorCollectionId = await GetAnyCollectionIdAsync(initiator.Id);
        var partnerCollectionId = await GetAnyCollectionIdAsync(partner.Id);

        using var initiatorClient = CreateClientWithUser(_factory, initiator);
        using var partnerClient = CreateClientWithUser(_factory, partner);

        var initiatorUpdate = new UpdateTradeRequest { InitiatorCollectionId = initiatorCollectionId };
        var partnerUpdate = new UpdateTradeRequest { PartnerCollectionId = partnerCollectionId };

        var initiatorUpdateResponse = await initiatorClient.PutAsJsonAsync($"/api/trades/{trade.TradeId}", initiatorUpdate, JsonContentHelper.DefaultOptions);
        initiatorUpdateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var partnerUpdateResponse = await partnerClient.PutAsJsonAsync($"/api/trades/{trade.TradeId}", partnerUpdate, JsonContentHelper.DefaultOptions);
        partnerUpdateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await initiatorClient.PutAsync($"/api/trades/{trade.TradeId}/commit", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var getResponse = await initiatorClient.GetAsync($"/api/trades/{trade.TradeId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CommitTradeSession_RecalculatesCollectionTotals()
    {
        var (initiator, partner, trade) = await PrepareTradeSessionAsync(includePartnerOffer: true);
        var initiatorCollectionId = await GetAnyCollectionIdAsync(initiator.Id);
        var partnerCollectionId = await GetAnyCollectionIdAsync(partner.Id);

        var initiatorMatch = trade.InitiatorMatches.Single();
        var partnerMatch = trade.PartnerMatches.Single();
        var initiatorTradeQuantity = initiatorMatch.OfferingCard.QuantityToTrade;
        var partnerTradeQuantity = partnerMatch.OfferingCard.QuantityToTrade;

        const double initiatorCardPrice = 12.5;
        const double partnerCardPrice = 7.25;

        double initiatorInitialTotal;
        double partnerInitialTotal;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MainContext>();
            var initiatorCollection = await context.Collections.FirstAsync(c => c.OwnerId == initiator.Id);
            var partnerCollection = await context.Collections.FirstAsync(c => c.OwnerId == partner.Id);

            var initiatorCard = await context.Cards.FirstAsync(c =>
                c.CollectionId == initiatorCollection.Id && c.Name == initiatorMatch.CardName);
            initiatorCard.PurchasePrice = initiatorCardPrice;
            initiatorCard.Quantity = Math.Max(initiatorTradeQuantity + 1, 2);
            initiatorCollection.NumberOfCards = initiatorCard.Quantity;
            initiatorCollection.TotalPrice = CalculateValue(initiatorCardPrice, initiatorCard.Quantity);

            var partnerCard = await context.Cards.FirstAsync(c =>
                c.CollectionId == partnerCollection.Id && c.Name == partnerMatch.CardName);
            partnerCard.PurchasePrice = partnerCardPrice;
            partnerCard.Quantity = Math.Max(partnerTradeQuantity + 1, 2);
            partnerCollection.NumberOfCards = partnerCard.Quantity;
            partnerCollection.TotalPrice = CalculateValue(partnerCardPrice, partnerCard.Quantity);

            await context.SaveChangesAsync();
            initiatorInitialTotal = initiatorCollection.TotalPrice;
            partnerInitialTotal = partnerCollection.TotalPrice;
        }

        var initiatorRemoved = CalculateValue(initiatorCardPrice, initiatorTradeQuantity);
        var partnerRemoved = CalculateValue(partnerCardPrice, partnerTradeQuantity);
        var initiatorAddition = partnerMatch.OfferingCard.MarketPrice.HasValue
            ? CalculateValue(partnerMatch.OfferingCard.MarketPrice.Value, partnerTradeQuantity)
            : 0;
        var partnerAddition = initiatorMatch.OfferingCard.MarketPrice.HasValue
            ? CalculateValue(initiatorMatch.OfferingCard.MarketPrice.Value, initiatorTradeQuantity)
            : 0;

        using var initiatorClient = CreateClientWithUser(_factory, initiator);
        using var partnerClient = CreateClientWithUser(_factory, partner);

        var initiatorUpdate = new UpdateTradeRequest { InitiatorCollectionId = initiatorCollectionId };
        var partnerUpdate = new UpdateTradeRequest { PartnerCollectionId = partnerCollectionId };

        (await initiatorClient.PutAsJsonAsync($"/api/trades/{trade.TradeId}", initiatorUpdate, JsonContentHelper.DefaultOptions))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await partnerClient.PutAsJsonAsync($"/api/trades/{trade.TradeId}", partnerUpdate, JsonContentHelper.DefaultOptions))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await initiatorClient.PutAsync($"/api/trades/{trade.TradeId}/commit", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<MainContext>();
        var initiatorCollectionAfter = await verificationContext.Collections.AsNoTracking()
            .FirstAsync(c => c.OwnerId == initiator.Id);
        var partnerCollectionAfter = await verificationContext.Collections.AsNoTracking()
            .FirstAsync(c => c.OwnerId == partner.Id);

        var initiatorExpected = initiatorInitialTotal - initiatorRemoved + initiatorAddition;
        var partnerExpected = partnerInitialTotal - partnerRemoved + partnerAddition;

        initiatorCollectionAfter.TotalPrice.Should().BeApproximately(initiatorExpected, 0.01);
        partnerCollectionAfter.TotalPrice.Should().BeApproximately(partnerExpected, 0.01);
    }

    [Fact]
    public async Task CommitTradeSession_WhenCollectionsMissing_ReturnsBadRequest()
    {
        var (initiator, _, trade) = await PrepareTradeSessionAsync(includePartnerOffer: true);
        using var client = CreateClientWithUser(_factory, initiator);

        var response = await client.PutAsync($"/api/trades/{trade.TradeId}/commit", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CommitTradeSession_WhenRequesterNotParticipant_ReturnsForbidden()
    {
        var (_, _, trade) = await PrepareTradeSessionAsync(includePartnerOffer: true);
        var intruder = await CreateTestUserAsync("trade-intruder@test.com", "trade_intruder");
        using var client = CreateClientWithUser(_factory, intruder);

        var response = await client.PutAsync($"/api/trades/{trade.TradeId}/commit", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CommitTradeSession_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var (_, _, trade) = await PrepareTradeSessionAsync(includePartnerOffer: true);
        using var client = _factory.CreateClient();

        var response = await client.PutAsync($"/api/trades/{trade.TradeId}/commit", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CommitTradeSession_WhenTradeMissing_ReturnsNotFound()
    {
        var initiator = await CreateTestUserAsync("trade-initiator@test.com", "trade_initiator");
        using var client = CreateClientWithUser(_factory, initiator);

        var response = await client.PutAsync($"/api/trades/{Guid.NewGuid():N}/commit", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CommitTradeSession_WhenNotLiveWithoutApproval_ReturnsForbidden()
    {
        var (initiator, partner, trade) = await PrepareTradeSessionAsync(includePartnerOffer: true, liveTrading: false, requireApproval: true);

        using var initiatorClient = CreateClientWithUser(_factory, initiator);
        using var partnerClient = CreateClientWithUser(_factory, partner);

        var initiatorCollectionId = await GetAnyCollectionIdAsync(initiator.Id);
        var partnerCollectionId = await GetAnyCollectionIdAsync(partner.Id);

        var initiatorUpdate = new UpdateTradeRequest { InitiatorCollectionId = initiatorCollectionId };
        await initiatorClient.PutAsJsonAsync($"/api/trades/{trade.TradeId}", initiatorUpdate, JsonContentHelper.DefaultOptions);

        var partnerUpdate = new UpdateTradeRequest { PartnerCollectionId = partnerCollectionId };
        await partnerClient.PutAsJsonAsync($"/api/trades/{trade.TradeId}", partnerUpdate, JsonContentHelper.DefaultOptions);

        var response = await initiatorClient.PutAsync($"/api/trades/{trade.TradeId}/commit", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CommitTradeSession_WhenNotLiveWithApproval_Succeeds()
    {
        var (initiator, partner, trade) = await PrepareTradeSessionAsync(includePartnerOffer: true, liveTrading: false, requireApproval: true);

        using var initiatorClient = CreateClientWithUser(_factory, initiator);
        using var partnerClient = CreateClientWithUser(_factory, partner);

        var initiatorCollectionId = await GetAnyCollectionIdAsync(initiator.Id);
        var partnerCollectionId = await GetAnyCollectionIdAsync(partner.Id);

        var initiatorUpdate = new UpdateTradeRequest { InitiatorCollectionId = initiatorCollectionId };
        await initiatorClient.PutAsJsonAsync($"/api/trades/{trade.TradeId}", initiatorUpdate, JsonContentHelper.DefaultOptions);

        var partnerUpdate = new UpdateTradeRequest { PartnerCollectionId = partnerCollectionId };
        await partnerClient.PutAsJsonAsync($"/api/trades/{trade.TradeId}", partnerUpdate, JsonContentHelper.DefaultOptions);

        await CreateCommitNotificationAsync(trade.TradeId, partner.Id, initiator.Id, approval: true);

        var response = await initiatorClient.PutAsync($"/api/trades/{trade.TradeId}/commit", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CancelTradeSession_SetsTradeNotificationsApprovalToFalse()
    {
        var (initiator, partner, trade) = await PrepareTradeSessionAsync();

        await CreateApprovedTradeNotificationAsync(initiator.Id, partner.Id);
        await CreateCommitNotificationAsync(trade.TradeId, initiator.Id, partner.Id, approval: true);

        using var client = CreateClientWithUser(_factory, initiator);
        var response = await client.DeleteAsync($"/api/trades/{trade.TradeId}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MainContext>();
        var tradeNotifications = await context.Notifications
            .Where(n =>
                (n.Name == "trade_commit_request" && n.ObjectId == trade.TradeId) ||
                (n.Name == "trade_request" && n.ObjectId == initiator.Id && n.AppUserId == partner.Id))
            .ToListAsync();

        tradeNotifications.Should().HaveCount(2);
        tradeNotifications.Should().AllSatisfy(n => n.Approval.Should().BeFalse());
    }

    private async Task<AppUser> CreateTestUserAsync(string email, string userName, IServiceProvider? services = null) =>
        await TestUserFactory.CreateAsync(services ?? _factory.Services, _testDataBuilder, email, userName, requirePassword: true);

    private async Task CreatePublicWishlistCardAsync(string ownerId, string cardName, IServiceProvider? services = null)
    {
        services ??= _factory.Services;
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MainContext>();
        var cardDataService = scope.ServiceProvider.GetRequiredService<CardDataService>();

        var wishlist = _testDataBuilder.CreateWishlist(ownerId, isPublic: true);
        context.Wishlists.Add(wishlist);
        await context.SaveChangesAsync();

        var marketCard = cardDataService.CardDataByName.TryGetValue(cardName, out var cardData)
            ? cardData
            : cardDataService.CardDataById.Values.First();

        var card = _testDataBuilder.CreateWishlistCard(wishlist.Id, marketCard.OracleId, cardName);
        card.ScryfallId = marketCard.Id;
        card.OracleId = marketCard.OracleId;
        card.WishlistId = wishlist.Id;
        context.WishlistCards.Add(card);
        await context.SaveChangesAsync();
    }

    private async Task CreatePublicBinderCardAsync(string ownerId, string cardName, IServiceProvider? services = null, int? collectionQuantity = null)
    {
        services ??= _factory.Services;
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MainContext>();
        var cardDataService = scope.ServiceProvider.GetRequiredService<CardDataService>();

        var collection = _testDataBuilder.CreateCollection(ownerId);
        context.Collections.Add(collection);
        await context.SaveChangesAsync();

        var marketCard = cardDataService.CardDataByName.TryGetValue(cardName, out var cardData)
            ? cardData
            : cardDataService.CardDataById.Values.First();

        var ownedCard = _testDataBuilder.CreateCard(collection.Id, cardName);
        ownedCard.ScryfallId = marketCard.Id;
        ownedCard.OracleId = marketCard.OracleId;
        ownedCard.IsFoil = false;
        if (collectionQuantity.HasValue)
        {
            ownedCard.Quantity = collectionQuantity.Value;
        }
        context.Cards.Add(ownedCard);
        await context.SaveChangesAsync();

        var binder = _testDataBuilder.CreateTradeBinder(ownerId, isPublic: true);
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

    private async Task CreateApprovedTradeNotificationAsync(string requesterUserId, string requestedUserId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var identityContext = scope.ServiceProvider.GetRequiredService<MainContext>();

        identityContext.Notifications.Add(new Notification
        {
            Name = "trade_request",
            Message = "Trade approved",
            Origin = $"trade_request.{requesterUserId}",
            ObjectId = requesterUserId,
            AppUserId = requestedUserId,
            Approval = true,
            CreationDateTime = DateTime.UtcNow,
            AppUser = null!
        });

        await identityContext.SaveChangesAsync();
    }

    private async Task CreateCommitNotificationAsync(string tradeId, string requesterUserId, string recipientUserId, bool approval = false)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var identityContext = scope.ServiceProvider.GetRequiredService<MainContext>();

        identityContext.Notifications.Add(new Notification
        {
            Name = "trade_commit_request",
            Message = "Commit trade",
            Origin = $"{tradeId}.{requesterUserId}",
            ObjectId = tradeId,
            AppUserId = recipientUserId,
            Approval = approval,
            CreationDateTime = DateTime.UtcNow,
            AppUser = null!
        });

        await identityContext.SaveChangesAsync();
    }

    private async Task<(AppUser Initiator, AppUser Partner, TradeConnectionDto Trade)> PrepareTradeSessionAsync(
        bool includePartnerOffer = false,
        bool liveTrading = true,
        bool requireApproval = false,
        WebApplicationFactory<Program>? targetFactory = null,
        int? collectionQuantity = null)
    {
        targetFactory ??= _factory;
        var services = targetFactory.Services;

        var initiator = await CreateTestUserAsync("trade-initiator@test.com", "trade_initiator", services);
        var partner = await CreateTestUserAsync("trade-partner@test.com", "trade_partner", services);

        await CreatePublicBinderCardAsync(initiator.Id, "Lightning Bolt", services, collectionQuantity);
        await CreatePublicWishlistCardAsync(partner.Id, "Lightning Bolt", services);

        if (requireApproval)
        {
            await CreateApprovedTradeNotificationAsync(initiator.Id, partner.Id);
            await CreateApprovedTradeNotificationAsync(partner.Id, initiator.Id);
        }

        if (includePartnerOffer)
        {
            await CreatePublicBinderCardAsync(partner.Id, "Counterspell", services);
            await CreatePublicWishlistCardAsync(initiator.Id, "Counterspell", services);
        }

        using var initiatorClient = CreateClientWithUser(targetFactory, initiator);
        var url = $"/api/trades/match?initiatorUserId={initiator.Id}&partnerUserId={partner.Id}";
        if (!liveTrading)
        {
            url += "&liveTrading=false";
        }

        var response = await initiatorClient.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var trade = await response.Content.ReadFromJsonAsync<TradeConnectionDto>(JsonContentHelper.DefaultOptions);

        trade.Should().NotBeNull();
        return (initiator, partner, trade);
    }

    private static HttpClient CreateClientWithUser(WebApplicationFactory<Program> targetFactory, AppUser user) =>
        CreateClientWithUser(targetFactory, user.Id, user.UserName ?? "user", user.Email ?? $"{user.UserName}@example.com");

    private static HttpClient CreateClientWithUser(WebApplicationFactory<Program> targetFactory, string userId, string userName, string email)
    {
        var client = targetFactory.CreateClient();
        client.DefaultRequestHeaders.Add("Test-UserId", userId);
        client.DefaultRequestHeaders.Add("Test-UserName", userName);
        client.DefaultRequestHeaders.Add("Test-Email", email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");
        return client;
    }

    private async Task<int> GetAnyCollectionIdAsync(string ownerId, IServiceProvider? services = null)
    {
        services ??= _factory.Services;
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MainContext>();
        var collection = await context.Collections.FirstAsync(c => c.OwnerId == ownerId);
        return collection.Id;
    }

    private static double CalculateValue(double price, int quantity)
    {
        var safeQuantity = Math.Max(0, quantity);
        if (safeQuantity == 0 || price <= 0)
        {
            return 0;
        }

        var safePrice = Math.Max(0, price);
        return Math.Round(safePrice * safeQuantity, 2, MidpointRounding.AwayFromZero);
    }

    private sealed class NotFoundTradeConnectionService : ITradeConnectionService
    {
        public Task CancelConnectionAsync(string tradeId, string requesterUserId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task CommitTradeAsync(string tradeId, string requesterUserId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<TradeConnectionDto> GetConnectionAsync(string tradeId, string requesterUserId, CancellationToken cancellationToken = default)
        {
            return Task.FromException<TradeConnectionDto>(new InvalidOperationException("This stub only supports PrepareConnectionAsync."));
        }

        public Task<TradeConnectionDto> PrepareConnectionAsync(string initiatorUserId, string partnerUserId, bool liveTrading, CancellationToken cancellationToken = default)
        {
            return Task.FromException<TradeConnectionDto>(new KeyNotFoundException("Trade connection missing."));
        }

        public Task<TradeConnectionDto> UpdateConnectionAsync(string tradeId, string requesterUserId, UpdateTradeRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromException<TradeConnectionDto>(new InvalidOperationException("This stub only supports PrepareConnectionAsync."));
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using API.Dtos.Binders;
using Core.Models;
using Core.Models.Identity;
using FluentAssertions;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using TestUtilities.Authentication;
using TestUtilities.Builders;
using TestUtilities.Serialization;

namespace IntegrationTests.Controllers;

[Collection("Integration Tests")]
public class BindersControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly TestDataBuilder _testDataBuilder;

    public BindersControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _testDataBuilder = new TestDataBuilder();
    }

    [Fact]
    public async Task CreateBinder_WithValidData_ReturnsCreatedBinder()
    {
        var user = await CreateTestUserAsync("binder-create@test.com", "binder_creator");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var request = new CreateBinderDto
        {
            Name = "My Trade Binder",
            Description = "Cards I am willing to trade",
            IsPublic = true
        };

        var response = await client.PostAsync("/api/binders", Serialize(request));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadAsStringAsync();

        var binder = JsonSerializer.Deserialize<BinderDto>(payload, JsonContentHelper.DefaultOptions);
        binder.Should().NotBeNull();
        binder!.Name.Should().Be(request.Name);
        binder.Description.Should().Be(request.Description);
        binder.IsPublic.Should().BeTrue();
        binder.OwnerId.Should().Be(user.Id);
        binder.CardsCount.Should().Be(0);
    }

    [Fact]
    public async Task GetBinders_ForUser_ReturnsOwnedBinders()
    {
        var owner = await CreateTestUserAsync("binder-owner@test.com", "binder_owner");
        var otherUser = await CreateTestUserAsync("binder-other@test.com", "binder_other");

        TradeBinder otherBinder;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MainContext>();
            var ownerBinders = new[]
            {
                _testDataBuilder.CreateTradeBinder(owner.Id, isPublic: false),
                _testDataBuilder.CreateTradeBinder(owner.Id, isPublic: true)
            };
            otherBinder = _testDataBuilder.CreateTradeBinder(otherUser.Id, isPublic: true);

            context.TradeBinders.AddRange(ownerBinders);
            context.TradeBinders.Add(otherBinder);
            await context.SaveChangesAsync();
        }

        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);
        var response = await client.GetAsync("/api/binders");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadAsStringAsync();

        var binders = JsonSerializer.Deserialize<List<BinderSummaryDto>>(payload, JsonContentHelper.DefaultOptions);
        binders.Should().NotBeNull();
        binders!.Should().HaveCount(2);
        binders.Should().NotContain(b => b.Id == otherBinder.Id);
    }

    [Fact]
    public async Task GetBinderById_RespectsPrivacySettings()
    {
        var owner = await CreateTestUserAsync("binder-privacy-owner@test.com", "binder_privacy_owner");
        var other = await CreateTestUserAsync("binder-privacy-other@test.com", "binder_privacy_other");

        TradeBinder publicBinder;
        TradeBinder privateBinder;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MainContext>();
            publicBinder = _testDataBuilder.CreateTradeBinder(owner.Id, isPublic: true);
            privateBinder = _testDataBuilder.CreateTradeBinder(owner.Id, isPublic: false);
            context.TradeBinders.Add(publicBinder);
            context.TradeBinders.Add(privateBinder);
            await context.SaveChangesAsync();
        }

        using var client = _factory.CreateClientWithUser(other.Id, other.UserName!, other.Email!);

        var publicResponse = await client.GetAsync($"/api/binders/{publicBinder.Id}");
        publicResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var privateResponse = await client.GetAsync($"/api/binders/{privateBinder.Id}");
        privateResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateBinder_WithValidData_ReturnsUpdatedBinder()
    {
        var owner = await CreateTestUserAsync("binder-update@test.com", "binder_update");
        TradeBinder binder;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MainContext>();
            binder = _testDataBuilder.CreateTradeBinder(owner.Id, isPublic: false);
            binder.Name = "Original";
            context.TradeBinders.Add(binder);
            await context.SaveChangesAsync();
        }

        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);
        var request = new UpdateBinderDto
        {
            Name = "Updated Binder",
            Description = "Updated description",
            IsPublic = true
        };

        var response = await client.PutAsync($"/api/binders/{binder.Id}", Serialize(request));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadAsStringAsync();

        var updated = JsonSerializer.Deserialize<BinderDto>(payload, JsonContentHelper.DefaultOptions);
        updated.Should().NotBeNull();
        updated!.Name.Should().Be(request.Name);
        updated.Description.Should().Be(request.Description);
        updated.IsPublic.Should().BeTrue();
    }
    

    [Fact]
    public async Task UpdateBinder_WithInvalidData_ReturnsBadRequest()
    {
        var owner = await CreateTestUserAsync("binder-update@test.com", "binder_update");
        TradeBinder binder;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MainContext>();
            binder = _testDataBuilder.CreateTradeBinder(owner.Id, isPublic: false);
            binder.Name = "Original";
            context.TradeBinders.Add(binder);
            await context.SaveChangesAsync();
        }

        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        const string invalidPayload = """
        {
            "name": "Updated Binder",
            "description": "Updated description",
            "isPublic": "not-a-boolean"
        }
        """;

        var response = await client.PutAsync(
            $"/api/binders/{binder.Id}",
            new StringContent(invalidPayload, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "because a non-boolean value for isPublic should fail model binding");

        var payload = await response.Content.ReadAsStringAsync();
        payload.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task DeleteBinder_RemovesBinder()
    {
        var owner = await CreateTestUserAsync("binder-delete@test.com", "binder_delete");
        TradeBinder binder;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MainContext>();
            binder = _testDataBuilder.CreateTradeBinder(owner.Id, isPublic: false);
            context.TradeBinders.Add(binder);
            await context.SaveChangesAsync();
        }

        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);
        var response = await client.DeleteAsync($"/api/binders/{binder.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<MainContext>();
        var deletedBinder = await verificationContext.TradeBinders.FindAsync(binder.Id);
        deletedBinder.Should().BeNull();
    }

    [Fact]
    public async Task BinderCards_FullCrudFlow_Works()
    {
        var owner = await CreateTestUserAsync("binder-card-owner@test.com", "binder_card_owner");
        TradeBinder binder;
        Card card;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MainContext>();
            binder = _testDataBuilder.CreateTradeBinder(owner.Id, isPublic: false);
            var collection = _testDataBuilder.CreateCollection(owner.Id);
            context.Collections.Add(collection);
            await context.SaveChangesAsync();

            card = _testDataBuilder.CreateCard(collection.Id, name: "Lightning Bolt", price: 2.5);
            card.Quantity = 4;
            context.Cards.Add(card);
            await context.SaveChangesAsync();

            context.TradeBinders.Add(binder);
            await context.SaveChangesAsync();
        }

        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var createDtos = new List<CreateBinderCardDto>
        {
            new()
            {
                CardId = card.Id,
                QuantityToTrade = 2,
                Notes = "Spare copies"
            }
        };

        var createResponse = await client.PostAsync($"/api/binders/{binder.Id}/cards", Serialize(createDtos));
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var createdPayload = await createResponse.Content.ReadAsStringAsync();
        var createdCards = JsonSerializer.Deserialize<List<BinderCardDto>>(createdPayload, JsonContentHelper.DefaultOptions);
        createdCards.Should().NotBeNull();
        createdCards!.Should().ContainSingle();
        var createdCard = createdCards.Single();
        createdCard.Name.Should().Be(card.Name);
        createdCard.QuantityToTrade.Should().Be(2);

        var listResponse = await client.GetAsync($"/api/binders/{binder.Id}/cards");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listPayload = await listResponse.Content.ReadAsStringAsync();
        var cards = JsonSerializer.Deserialize<List<BinderCardDto>>(listPayload, JsonContentHelper.DefaultOptions);
        cards.Should().NotBeNull();
        cards!.Should().HaveCount(1);

        var updateDto = new UpdateBinderCardDto
        {
            QuantityToTrade = 1,
            Notes = "Keeping one copy"
        };

        var updateResponse = await client.PutAsync($"/api/binders/{binder.Id}/cards/{createdCard.Id}", Serialize(updateDto));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedPayload = await updateResponse.Content.ReadAsStringAsync();
        var updatedCard = JsonSerializer.Deserialize<BinderCardDto>(updatedPayload, JsonContentHelper.DefaultOptions);
        updatedCard.Should().NotBeNull();
        updatedCard!.QuantityToTrade.Should().Be(1);
        updatedCard.Notes.Should().Be(updateDto.Notes);

        var deleteResponse = await client.DeleteAsync($"/api/binders/{binder.Id}/cards/{createdCard.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var confirmResponse = await client.GetAsync($"/api/binders/{binder.Id}/cards");
        var confirmPayload = await confirmResponse.Content.ReadAsStringAsync();
        var remainingCards = JsonSerializer.Deserialize<List<BinderCardDto>>(confirmPayload, JsonContentHelper.DefaultOptions);
        remainingCards.Should().NotBeNull();
        remainingCards!.Should().BeEmpty();
    }

    [Fact]
    public async Task BinderCards_PublicBinderVisibleToOtherUsers()
    {
        var owner = await CreateTestUserAsync("binder-public-owner@test.com", "binder_public_owner");
        var other = await CreateTestUserAsync("binder-public-other@test.com", "binder_public_other");
        TradeBinder binder;
        BinderCard binderCard;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MainContext>();
            var collection = _testDataBuilder.CreateCollection(owner.Id);
            context.Collections.Add(collection);
            await context.SaveChangesAsync();

            var card = _testDataBuilder.CreateCard(collection.Id, name: "Counterspell", price: 1.5);
            card.Quantity = 3;
            context.Cards.Add(card);
            await context.SaveChangesAsync();

            binder = _testDataBuilder.CreateTradeBinder(owner.Id, isPublic: true);
            context.TradeBinders.Add(binder);
            await context.SaveChangesAsync();

            binderCard = new BinderCard
            {
                TradeBinderId = binder.Id,
                CardId = card.Id,
                Name = card.Name,
                QuantityToTrade = 1,
                Notes = "Looking for trades",
                Card = card
            };
            context.BinderCards.Add(binderCard);
            await context.SaveChangesAsync();
        }

        using var client = _factory.CreateClientWithUser(other.Id, other.UserName!, other.Email!);
        var response = await client.GetAsync($"/api/binders/{binder.Id}/cards");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadAsStringAsync();

        var cards = JsonSerializer.Deserialize<List<BinderCardDto>>(payload, JsonContentHelper.DefaultOptions);
        cards.Should().NotBeNull();
        cards!.Should().ContainSingle();
        cards[0].Name.Should().Be(binderCard.Name);
    }

    [Fact]
    public async Task BinderCards_PrivateBinderHiddenFromOtherUsers()
    {
        var owner = await CreateTestUserAsync("binder-private-owner@test.com", "binder_private_owner");
        var other = await CreateTestUserAsync("binder-private-other@test.com", "binder_private_other");
        TradeBinder binder;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MainContext>();
            binder = _testDataBuilder.CreateTradeBinder(owner.Id, isPublic: false);
            context.TradeBinders.Add(binder);
            await context.SaveChangesAsync();
        }

        using var client = _factory.CreateClientWithUser(other.Id, other.UserName!, other.Email!);
        var response = await client.GetAsync($"/api/binders/{binder.Id}/cards");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static StringContent Serialize<T>(T value) => JsonContentHelper.CreateContent(value);

    private Task<AppUser> CreateTestUserAsync(string email, string userName) =>
        TestUserFactory.CreateAsync(_factory.Services, _testDataBuilder, email, userName, requirePassword: true);
}

using API.Dtos.Cards;
using API.Dtos.Wishlists;
using API.Services;
using Core.Models;
using Core.Models.Identity;
using FluentAssertions;
using Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TestUtilities.Builders;
using TestUtilities.Scryfall;
using Core.Enums;

namespace UnitTests.Services;

public class WishlistServiceTests
{
    [Fact]
    public async Task CreateWishlistCardsAsync_FiltersUnknownCardsAndRecalculatesTotals()
    {
        await using var context = CreateContext();
        var unitOfWork = CreateUnitOfWork(context);
        const string ownerId = "user-1";
        var wishlist = new Wishlist
        {
            Name = "Wishlist",
            Description = null,
            IsPublic = true,
            OwnerId = ownerId,
            TotalPrice = 0
        };
        context.Wishlists.Add(wishlist);
        await context.SaveChangesAsync();

        var builder = new TestDataBuilder();
        var cardId = Guid.NewGuid().ToString();
        var card = builder.CreateOracleCard(id: cardId, name: "Island")
            with { Prices = new Prices("1.50", null, "1.50", null, null) };
        var cardDataService = CardDataServiceTestHelper.CreateWithCards([card]);

        var settingsServiceMock = new Mock<IUserSettingsService>();
        settingsServiceMock.Setup(s => s.GetMarketProviderAsync(ownerId))
            .ReturnsAsync(MarketProvider.Mkm);
        settingsServiceMock.Setup(s => s.ResolveCurrency(It.IsAny<MarketProvider>()))
            .Returns((MarketProvider provider) => provider == MarketProvider.Mkm ? Currency.Eur : Currency.Usd);

        var pricingService = new WishlistPricingService(
            unitOfWork,
            cardDataService,
            settingsServiceMock.Object,
            NullLogger<WishlistPricingService>.Instance);

        var service = new WishlistService(
            unitOfWork,
            new ValidationService(),
            cardDataService,
            pricingService,
            new Mock<ITeamService>().Object);

        var created = await service.CreateWishlistCardsAsync(
            wishlist.Id,
            [
                new()
                {
                    ScryfallId = cardId,
                    DesiredQuantity = 2,
                    ExactVersion = false
                },

                new()
                {
                    ScryfallId = "missing",
                    DesiredQuantity = 1,
                    ExactVersion = false
                }
            ],
            ownerId);

        created.Should().HaveCount(1);
        (await context.WishlistCards.CountAsync()).Should().Be(1);

        var updatedWishlist = await context.Wishlists.SingleAsync();
        updatedWishlist.TotalPrice.Should().Be(3.00);
        updatedWishlist.TotalPriceCurrency.Should().Be(Currency.Eur);
    }

    [Fact]
    public async Task UpdateWishlistCardAsync_WhenVersionNameMismatch_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        var unitOfWork = CreateUnitOfWork(context);
        const string ownerId = "user-1";
        var wishlist = new Wishlist
        {
            Name = "Wishlist",
            Description = null,
            IsPublic = true,
            OwnerId = ownerId,
            TotalPrice = 0
        };
        context.Wishlists.Add(wishlist);
        await context.SaveChangesAsync();

        var wishlistCard = new WishlistCard
        {
            WishlistId = wishlist.Id,
            Name = "Card A",
            ScryfallId = "original",
            OracleId = "oracle-original",
            DesiredQuantity = 1,
            ExactVersion = false
        };
        context.WishlistCards.Add(wishlistCard);
        await context.SaveChangesAsync();

        var builder = new TestDataBuilder();
        var cardDataService = CardDataServiceTestHelper.CreateWithCards([
            builder.CreateOracleCard(id: "different", name: "Card B")
                with { Prices = new Prices("1.00", null, "1.00", null, null) }
        ]);

        var settingsServiceMock = new Mock<IUserSettingsService>();
        settingsServiceMock.Setup(s => s.GetMarketProviderAsync(ownerId))
            .ReturnsAsync(MarketProvider.Mkm);

        var pricingService = new WishlistPricingService(
            unitOfWork,
            cardDataService,
            settingsServiceMock.Object,
            NullLogger<WishlistPricingService>.Instance);

        var service = new WishlistService(
            unitOfWork,
            new ValidationService(),
            cardDataService,
            pricingService,
            new Mock<ITeamService>().Object);

        Func<Task> act = () => service.UpdateWishlistCardAsync(
            wishlist.Id,
            wishlistCard.Id,
            new UpdateWishlistCardDto { ScryfallId = "different" },
            ownerId);

        var exception = await act.Should().ThrowAsync<WishlistServiceException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        exception.Which.IncludeBody.Should().BeTrue();
        exception.Which.Body.Should().Be("Errors.Wishlists.InvalidVersion");
    }

    [Fact]
    public async Task GetWishlistCardByIdAsync_WhenNotOwner_ReturnsUnauthorized()
    {
        await using var context = CreateContext();
        var unitOfWork = CreateUnitOfWork(context);
        const string ownerId = "owner";
        var wishlist = new Wishlist
        {
            Name = "Wishlist",
            Description = null,
            IsPublic = true,
            OwnerId = ownerId,
            TotalPrice = 0
        };
        context.Wishlists.Add(wishlist);
        await context.SaveChangesAsync();

        var service = new WishlistService(
            unitOfWork,
            new ValidationService(),
            CardDataServiceTestHelper.CreateWithCards(Array.Empty<ScryfallCardDto>()),
            new WishlistPricingService(
                unitOfWork,
                CardDataServiceTestHelper.CreateWithCards(Array.Empty<ScryfallCardDto>()),
                new Mock<IUserSettingsService>().Object,
                NullLogger<WishlistPricingService>.Instance),
            new Mock<ITeamService>().Object);

        Func<Task> act = () => service.GetWishlistCardByIdAsync(wishlist.Id, 1, "other");

        var exception = await act.Should().ThrowAsync<WishlistServiceException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    private static MainContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MainContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new MainContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static UnitOfWork CreateUnitOfWork(MainContext context)
        => new(context, NullLogger<UnitOfWork>.Instance, NullLoggerFactory.Instance);

    
}

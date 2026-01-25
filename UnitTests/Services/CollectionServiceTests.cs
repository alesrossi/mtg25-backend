using API.Dtos.Cards;
using API.Helpers;
using API.Services;
using Core.Interfaces;
using Core.Models;
using Core.Models.Identity;
using Core.Specifications;
using FluentAssertions;
using Core.Enums;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TestUtilities.Builders;
using TestUtilities.Scryfall;

namespace UnitTests.Services;

public class CollectionServiceTests
{
    [Fact]
    public async Task GetCardsFromCollectionAsync_GroupBySetCode_ReturnsGroupedPagination()
    {
        await using var context = CreateContext();
        var unitOfWork = CreateUnitOfWork(context);
        var ownerId = "user-1";
        var collection = new Collection
        {
            Name = "Collection",
            Color = "Blue",
            NumberOfCards = 0,
            TotalPrice = 0,
            OwnerId = ownerId
        };
        context.Collections.Add(collection);
        await context.SaveChangesAsync();

        context.Cards.AddRange(
            CreateCard(collection.Id, "Card A", "set-a"),
            CreateCard(collection.Id, "Card B", "set-a"),
            CreateCard(collection.Id, "Card C", "set-b"));
        await context.SaveChangesAsync();

        var service = CreateService(unitOfWork);
        var entityParams = new EntitySpecParams
        {
            GroupBy = "setcode",
            PageIndex = 1,
            PageSize = 10
        };

        var result = await service.GetCardsFromCollectionAsync(collection.Id, ownerId, entityParams);

        result.Should().BeOfType<GroupedCardsPaginationDto>();
        var grouped = (GroupedCardsPaginationDto)result;
        grouped.TotalGroups.Should().Be(2);
        grouped.TotalCards.Should().Be(3);
        grouped.Groups.Select(g => g.GroupKey).Should().BeEquivalentTo(new[] { "set-a", "set-b" });
    }

    [Fact]
    public async Task GetCardsFromCollectionAsync_CurrentPriceDesc_SortsByMarketPrice()
    {
        await using var context = CreateContext();
        var unitOfWork = CreateUnitOfWork(context);
        var ownerId = "user-1";
        var collection = new Collection
        {
            Name = "Collection",
            Color = "Blue",
            NumberOfCards = 0,
            TotalPrice = 0,
            OwnerId = ownerId
        };
        context.Collections.Add(collection);
        await context.SaveChangesAsync();

        var builder = new TestDataBuilder();
        var priceyCard = builder.CreateOracleCard(id: "pricey", name: "Card A")
            with { Prices = new Prices("2.50", null, "2.50", null, null) };
        var cheapCard = builder.CreateOracleCard(id: "cheap", name: "Card B")
            with { Prices = new Prices("1.00", null, "1.00", null, null) };
        var cardDataService = CardDataServiceTestHelper.CreateWithCards(new[] { priceyCard, cheapCard });

        context.Cards.AddRange(
            CreateCard(collection.Id, "Card A", "set-a", scryfallId: "pricey"),
            CreateCard(collection.Id, "Card B", "set-b", scryfallId: "cheap"));
        await context.SaveChangesAsync();

        var service = CreateService(unitOfWork, cardDataService: cardDataService);
        var entityParams = new EntitySpecParams
        {
            Sort = "currentPriceDesc",
            PageIndex = 1,
            PageSize = 10
        };

        var result = await service.GetCardsFromCollectionAsync(collection.Id, ownerId, entityParams);

        result.Should().BeOfType<Pagination<ExtensiveCardDto>>();
        var pagination = (Pagination<ExtensiveCardDto>)result;
        pagination.Data.Should().HaveCount(2);
        pagination.Data[0].ScryfallId.Should().Be("pricey");
        pagination.Data[1].ScryfallId.Should().Be("cheap");
    }

    [Fact]
    public async Task MassDeleteCardsAsync_RemovesCardsAndUpdatesTotals()
    {
        await using var context = CreateContext();
        var unitOfWork = CreateUnitOfWork(context);
        var ownerId = "user-1";
        var collection = new Collection
        {
            Name = "Collection",
            Color = "Blue",
            NumberOfCards = 6,
            TotalPrice = 12.00,
            OwnerId = ownerId
        };
        context.Collections.Add(collection);
        await context.SaveChangesAsync();

        var cardOne = CreateCard(collection.Id, "Card A", "set-a", purchasePrice: 2.00, quantity: 2);
        var cardTwo = CreateCard(collection.Id, "Card B", "set-b", purchasePrice: 4.00, quantity: 1);
        context.Cards.AddRange(cardOne, cardTwo);
        await context.SaveChangesAsync();

        var service = CreateService(unitOfWork);

        var removed = await service.MassDeleteCardsAsync(collection.Id, ownerId, new List<int> { cardOne.Id, cardTwo.Id });

        removed.Should().Be(2);
        var updatedCollection = await context.Collections.SingleAsync();
        updatedCollection.NumberOfCards.Should().Be(3);
        updatedCollection.TotalPrice.Should().Be(4.00);
    }

    private static CollectionService CreateService(
        IUnitOfWork unitOfWork,
        CardDataService? cardDataService = null,
        IUserSettingsService? userSettingsService = null)
    {
        var settingsServiceMock = new Mock<IUserSettingsService>();
        settingsServiceMock.Setup(s => s.GetMarketProviderAsync(It.IsAny<string>()))
            .ReturnsAsync(MarketProvider.Mkm);
        settingsServiceMock.Setup(s => s.ResolveCurrency(It.IsAny<MarketProvider>()))
            .Returns((MarketProvider provider) => provider == MarketProvider.Mkm ? Currency.Eur : Currency.Usd);

        var userManagerMock = new Mock<Microsoft.AspNetCore.Identity.UserManager<AppUser>>(
            new Mock<IUserStore<AppUser>>().Object, null, null, null, null, null, null, null, null);

        return new CollectionService(
            unitOfWork,
            new ValidationService(),
            cardDataService ?? CardDataServiceTestHelper.CreateWithCards(Array.Empty<ScryfallCardDto>()),
            userSettingsService ?? settingsServiceMock.Object,
            userManagerMock.Object);
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

    private static Card CreateCard(
        int collectionId,
        string name,
        string setCode,
        string? scryfallId = null,
        double purchasePrice = 1.00,
        int quantity = 1)
    {
        return new Card
        {
            Name = name,
            ScryfallId = scryfallId ?? Guid.NewGuid().ToString(),
            CollectionId = collectionId,
            Quantity = quantity,
            Language = Language.En,
            Condition = Condition.NearMint,
            IsFoil = false,
            PurchasePrice = purchasePrice,
            PurchasePriceCurrency = "USD",
            ImageUrl = "http://image",
            BackImageUrl = null,
            ArtCrop = "http://art",
            SetCode = setCode,
            SetName = setCode.ToUpperInvariant(),
            TypeLine = "Creature",
            CollectorNumber = "1",
            Rarity = "common",
            IsMisprint = false,
            IsAltered = false
        };
    }
}

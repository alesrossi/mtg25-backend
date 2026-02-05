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
        const string ownerId = "user-1";
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
        grouped.Groups.Select(g => g.GroupKey).Should().BeEquivalentTo("set-a", "set-b");
    }

    [Fact]
    public async Task GetCardsFromCollectionAsync_CurrentPriceDesc_SortsByMarketPrice()
    {
        await using var context = CreateContext();
        var unitOfWork = CreateUnitOfWork(context);
        const string ownerId = "user-1";
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
        var cardDataService = CardDataServiceTestHelper.CreateWithCards([priceyCard, cheapCard]);

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
        const string ownerId = "user-1";
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

        var removed = await service.MassDeleteCardsAsync(collection.Id, ownerId, [cardOne.Id, cardTwo.Id]);

        removed.Should().Be(2);
        var updatedCollection = await context.Collections.SingleAsync();
        updatedCollection.NumberOfCards.Should().Be(3);
        updatedCollection.TotalPrice.Should().Be(4.00);
    }

    [Fact]
    public async Task MassExportCardsToBinderAsync_AddsBinderCardsWithResolvedNames()
    {
        await using var context = CreateContext();
        var unitOfWork = CreateUnitOfWork(context);
        const string ownerId = "user-1";
        var collection = new Collection
        {
            Name = "Collection",
            Color = "Blue",
            NumberOfCards = 0,
            TotalPrice = 0,
            OwnerId = ownerId
        };
        var binder = new TradeBinder
        {
            Name = "Binder",
            Description = null,
            IsPublic = false,
            OwnerId = ownerId
        };
        context.Collections.Add(collection);
        context.TradeBinders.Add(binder);
        await context.SaveChangesAsync();

        var cardOne = CreateCard(collection.Id, "Local A", "set-a", scryfallId: "card-a", quantity: 3);
        var cardTwo = CreateCard(collection.Id, "Local B", "set-b", scryfallId: "card-b", quantity: 1);
        context.Cards.AddRange(cardOne, cardTwo);
        await context.SaveChangesAsync();

        var builder = new TestDataBuilder();
        var cardDataService = CardDataServiceTestHelper.CreateWithCards(new[]
        {
            builder.CreateOracleCard(id: "card-a", name: "Oracle A", setCode: cardOne.SetCode, setName: cardOne.SetName),
            builder.CreateOracleCard(id: "card-b", name: "Oracle B", setCode: cardTwo.SetCode, setName: cardTwo.SetName)
        });

        var service = CreateService(unitOfWork, cardDataService: cardDataService);

        var added = await service.MassExportCardsToBinderAsync(collection.Id, ownerId, binder.Id, [cardOne.Id, cardTwo.Id]);

        added.Should().Be(2);
        var binderCards = await context.BinderCards.ToListAsync();
        binderCards.Should().HaveCount(2);
        binderCards.Single(card => card.CardId == cardOne.Id).Name.Should().Be("Oracle A");
        binderCards.Single(card => card.CardId == cardTwo.Id).Name.Should().Be("Oracle B");
        binderCards.Single(card => card.CardId == cardOne.Id).QuantityToTrade.Should().Be(cardOne.Quantity);
    }

    [Fact]
    public async Task MassExportCardsToBinderAsync_IgnoresCardsNotInCollection()
    {
        await using var context = CreateContext();
        var unitOfWork = CreateUnitOfWork(context);
        const string ownerId = "user-1";
        var collection = new Collection
        {
            Name = "Collection",
            Color = "Blue",
            NumberOfCards = 0,
            TotalPrice = 0,
            OwnerId = ownerId
        };
        var otherCollection = new Collection
        {
            Name = "Other",
            Color = "Green",
            NumberOfCards = 0,
            TotalPrice = 0,
            OwnerId = ownerId
        };
        var binder = new TradeBinder
        {
            Name = "Binder",
            Description = null,
            IsPublic = false,
            OwnerId = ownerId
        };
        context.Collections.AddRange(collection, otherCollection);
        context.TradeBinders.Add(binder);
        await context.SaveChangesAsync();

        var validCard = CreateCard(collection.Id, "Valid", "set-a", scryfallId: "card-valid", quantity: 2);
        var otherCard = CreateCard(otherCollection.Id, "Other", "set-b", scryfallId: "card-other", quantity: 1);
        context.Cards.AddRange(validCard, otherCard);
        await context.SaveChangesAsync();

        var builder = new TestDataBuilder();
        var cardDataService = CardDataServiceTestHelper.CreateWithCards(new[]
        {
            builder.CreateOracleCard(id: "card-valid", name: "Oracle Valid", setCode: validCard.SetCode, setName: validCard.SetName),
            builder.CreateOracleCard(id: "card-other", name: "Oracle Other", setCode: otherCard.SetCode, setName: otherCard.SetName)
        });

        var service = CreateService(unitOfWork, cardDataService: cardDataService);

        var added = await service.MassExportCardsToBinderAsync(collection.Id, ownerId, binder.Id, [validCard.Id, otherCard.Id, int.MaxValue]);

        added.Should().Be(1);
        var binderCards = await context.BinderCards.ToListAsync();
        binderCards.Should().ContainSingle();
        binderCards.Single().CardId.Should().Be(validCard.Id);
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
            new Mock<IUserStore<AppUser>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);

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
        string? oracleId = null,
        double purchasePrice = 1.00,
        int quantity = 1)
    {
        return new Card
        {
            Name = name,
            ScryfallId = scryfallId ?? Guid.NewGuid().ToString(),
            OracleId = oracleId ?? Guid.NewGuid().ToString(),
            CollectionId = collectionId,
            Quantity = quantity,
            Language = Language.En,
            Condition = Condition.NearMint,
            IsFoil = false,
            PurchasePrice = purchasePrice,
            PurchasePriceCurrency = Currency.Usd,
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

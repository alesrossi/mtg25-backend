using API.Dtos.Cards;
using API.Services;
using Core.Interfaces;
using Core.Models;
using Core.Models.Identity;
using FluentAssertions;
using Core.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TestUtilities.Builders;
using TestUtilities.Scryfall;

namespace UnitTests.Services;

public class CardsServiceTests
{
    [Fact]
    public async Task UpdateCardAsync_ChangesQuantity_RecalculatesCollectionTotals()
    {
        await using var context = CreateContext();
        var unitOfWork = CreateUnitOfWork(context);
        const string ownerId = "user-1";
        var collection = CreateCollection(ownerId, numberOfCards: 1, totalPrice: 2.00);
        context.Collections.Add(collection);
        await context.SaveChangesAsync();

        var card = CreateCard(collection.Id, "Card A", "sf-1", "oi-1", quantity: 1, purchasePrice: 2.00);
        context.Cards.Add(card);
        await context.SaveChangesAsync();

        var service = CreateService(unitOfWork);
        var updateDto = new UpdateCollectionCardDto
        {
            CollectionId = collection.Id,
            Quantity = 3,
            Language = CardLanguage.En,
            Condition = "NearMint",
            IsFoil = false,
            PurchasePrice = 2.00,
            PurchasePriceCurrency = Currency.Usd,
            IsMisprint = false,
            IsAltered = false
        };

        var updatedCard = await service.UpdateCardAsync(card.Id, updateDto, ownerId);

        updatedCard.Quantity.Should().Be(3);
        var updatedCollection = await context.Collections.SingleAsync();
        updatedCollection.NumberOfCards.Should().Be(3);
        updatedCollection.TotalPrice.Should().Be(6.00);
    }

    [Fact]
    public async Task UpdateCardVersionAsync_UpdatesVersionFieldsAndTotals()
    {
        await using var context = CreateContext();
        var unitOfWork = CreateUnitOfWork(context);
        const string ownerId = "user-1";
        var collection = CreateCollection(ownerId, numberOfCards: 1, totalPrice: 2.00);
        context.Collections.Add(collection);
        await context.SaveChangesAsync();

        var card = CreateCard(collection.Id, "Card A", "old-id", "oi-1", quantity: 1, purchasePrice: 2.00);
        context.Cards.Add(card);
        await context.SaveChangesAsync();

        var builder = new TestDataBuilder();
        var scryfallCard = builder.CreateOracleCard(id: "new-id", oracleId: "oi-1", name: "Card A")
            with
            {
                SetId = "set-id-123",
                SetName = "Set Name",
                CollectorNumber = "42",
                Rarity = "rare",
                ImageUris = new ImageUris("small", "normal", "large", "png", "art", "border")
            };
        var cardDataService = CardDataServiceTestHelper.CreateWithCards([scryfallCard]);

        var service = CreateService(unitOfWork, cardDataService);
        var updateDto = new UpdateCollectionCardWithSfIdDto
        {
            CollectionId = collection.Id,
            Quantity = 2,
            Language = CardLanguage.En,
            Condition = "NearMint",
            IsFoil = false,
            PurchasePrice = 2.00,
            PurchasePriceCurrency = Currency.Usd,
            IsMisprint = false,
            IsAltered = false,
            ScryfallId = "new-id"
        };

        var updatedCard = await service.UpdateCardVersionAsync(card.Id, updateDto, ownerId);

        updatedCard.ScryfallId.Should().Be("new-id");
        updatedCard.SetCode.Should().Be("set-id-123");
        updatedCard.SetName.Should().Be("Set Name");
        updatedCard.CollectorNumber.Should().Be("42");
        updatedCard.Rarity.Should().Be("rare");
        updatedCard.ImageUrl.Should().Be("large");
        updatedCard.ArtCrop.Should().Be("art");

        var updatedCollection = await context.Collections.SingleAsync();
        updatedCollection.NumberOfCards.Should().Be(2);
        updatedCollection.TotalPrice.Should().Be(4.00);
    }

    [Fact]
    public async Task AddNewCardAsync_WhenPurchasePriceMissing_UsesMarketPriceAndCurrency()
    {
        await using var context = CreateContext();
        var unitOfWork = CreateUnitOfWork(context);
        const string ownerId = "user-1";
        var collection = CreateCollection(ownerId);
        context.Collections.Add(collection);
        await context.SaveChangesAsync();

        var builder = new TestDataBuilder();
        var scryfallCard = builder.CreateOracleCard(id: "sf-1", name: "Card A")
            with { Prices = new Prices(null, null, "1.50", null, null) };
        var cardDataService = CardDataServiceTestHelper.CreateWithCards([scryfallCard]);

        var settingsServiceMock = new Mock<IUserSettingsService>();
        settingsServiceMock.Setup(s => s.GetMarketProviderAsync(ownerId))
            .ReturnsAsync(MarketProvider.Mkm);
        settingsServiceMock.Setup(s => s.ResolveCurrency(It.IsAny<MarketProvider>()))
            .Returns((MarketProvider provider) => provider == MarketProvider.Mkm ? Currency.Eur : Currency.Usd);

        var service = CreateService(unitOfWork, cardDataService, settingsServiceMock.Object);
        var cardDto = new InternalCardDto
        {
            ScryfallId = "sf-1",
            CollectionId = collection.Id,
            Quantity = 2,
            Language = CardLanguage.En,
            Condition = "NearMint",
            IsFoil = true,
            PurchasePrice = null,
            PurchasePriceCurrency = null,
            IsMisprint = false,
            IsAltered = false
        };

        var card = await service.AddNewCardAsync(cardDto, ownerId);

        card.PurchasePrice.Should().Be(1.50);
        card.PurchasePriceCurrency.Should().Be(Currency.Eur);
        var updatedCollection = await context.Collections.SingleAsync();
        updatedCollection.NumberOfCards.Should().Be(2);
        updatedCollection.TotalPrice.Should().Be(3.00);
    }

    [Fact]
    public async Task GetCardByIdAsync_UsesMarketProviderForPrice()
    {
        await using var context = CreateContext();
        var unitOfWork = CreateUnitOfWork(context);
        const string ownerId = "user-1";
        var collection = CreateCollection(ownerId);
        context.Collections.Add(collection);
        await context.SaveChangesAsync();

        var card = CreateCard(collection.Id, "Card A", "sf-1", "oi-1", quantity: 1, purchasePrice: 2.00, isFoil: true);
        context.Cards.Add(card);
        await context.SaveChangesAsync();

        var builder = new TestDataBuilder();
        var scryfallCard = builder.CreateOracleCard(id: "sf-1", name: "Card A")
            with { Prices = new Prices(null, "2.50", null, "2.50", null) };
        var cardDataService = CardDataServiceTestHelper.CreateWithCards([scryfallCard]);

        var settingsServiceMock = new Mock<IUserSettingsService>();
        settingsServiceMock.Setup(s => s.GetMarketProviderAsync(ownerId))
            .ReturnsAsync(MarketProvider.Mkm);

        var service = CreateService(unitOfWork, cardDataService, settingsServiceMock.Object);

        var dto = await service.GetCardByIdAsync(card.Id, ownerId);

        dto.Price.Should().Be(2.50);
        dto.PriceCurrency.Should().Be(MarketProvider.Mkm);
    }

    [Fact]
    public async Task GetCardImagesByNameAsync_MatchByName_ReturnsMatchingImages()
    {
        await using var context = CreateContext();
        var unitOfWork = CreateUnitOfWork(context);
        var builder = new TestDataBuilder();
        var match = builder.CreateOracleCard(id: "sf-1", name: "Lightning Bolt")
            with { ImageUris = new ImageUris(null, null, null, null, "https://art.example.com/1.jpg", null) };
        var other = builder.CreateOracleCard(id: "sf-2", name: "Counterspell");
        var cardDataService = CardDataServiceTestHelper.CreateWithCards([match, other]);
        var service = CreateService(unitOfWork, cardDataService);

        var result = await service.GetCardImagesByNameAsync("Lightning Bolt", "user-1");

        result.Should().HaveCount(1);
        result[0].ScryfallId.Should().Be("sf-1");
        result[0].ArtCrop.Should().Be("https://art.example.com/1.jpg");
    }

    [Fact]
    public async Task GetCardImagesByNameAsync_MatchByFlavorName_ReturnsMatchingImages()
    {
        await using var context = CreateContext();
        var unitOfWork = CreateUnitOfWork(context);
        var builder = new TestDataBuilder();
        var match = builder.CreateOracleCard(id: "sf-3", name: "Balduvian Bears", flavorName: "Bjornsson")
            with { ImageUris = new ImageUris(null, null, null, null, "https://art.example.com/3.jpg", null) };
        var cardDataService = CardDataServiceTestHelper.CreateWithCards([match]);
        var service = CreateService(unitOfWork, cardDataService);

        var result = await service.GetCardImagesByNameAsync("Bjornsson", "user-1");

        result.Should().HaveCount(1);
        result[0].ScryfallId.Should().Be("sf-3");
    }

    [Fact]
    public async Task GetCardImagesByNameAsync_MatchByPrintedName_ReturnsMatchingImages()
    {
        await using var context = CreateContext();
        var unitOfWork = CreateUnitOfWork(context);
        var builder = new TestDataBuilder();
        var match = builder.CreateOracleCard(id: "sf-4", name: "Lightning Bolt", printedName: "Fulmine")
            with { ImageUris = new ImageUris(null, null, null, null, "https://art.example.com/4.jpg", null) };
        var cardDataService = CardDataServiceTestHelper.CreateWithCards([match]);
        var service = CreateService(unitOfWork, cardDataService);

        var result = await service.GetCardImagesByNameAsync("Fulmine", "user-1");

        result.Should().HaveCount(1);
        result[0].ScryfallId.Should().Be("sf-4");
    }

    [Fact]
    public async Task GetCardImagesByNameAsync_NoMatch_ReturnsEmptyList()
    {
        await using var context = CreateContext();
        var unitOfWork = CreateUnitOfWork(context);
        var builder = new TestDataBuilder();
        var card = builder.CreateOracleCard(id: "sf-5", name: "Counterspell");
        var cardDataService = CardDataServiceTestHelper.CreateWithCards([card]);
        var service = CreateService(unitOfWork, cardDataService);

        var result = await service.GetCardImagesByNameAsync("Unknown Card", "user-1");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCardImagesByNameAsync_MultipleVersions_ReturnsAllMatches()
    {
        await using var context = CreateContext();
        var unitOfWork = CreateUnitOfWork(context);
        var builder = new TestDataBuilder();
        var v1 = builder.CreateOracleCard(id: "sf-6a", name: "Lightning Bolt")
            with { ImageUris = new ImageUris(null, null, null, null, "https://art.example.com/6a.jpg", null) };
        var v2 = builder.CreateOracleCard(id: "sf-6b", name: "Lightning Bolt")
            with { ImageUris = new ImageUris(null, null, null, null, "https://art.example.com/6b.jpg", null) };
        var other = builder.CreateOracleCard(id: "sf-6c", name: "Dark Ritual");
        var cardDataService = CardDataServiceTestHelper.CreateWithCards([v1, v2, other]);
        var service = CreateService(unitOfWork, cardDataService);

        var result = await service.GetCardImagesByNameAsync("Lightning Bolt", "user-1");

        result.Should().HaveCount(2);
        result.Select(r => r.ScryfallId).Should().BeEquivalentTo(["sf-6a", "sf-6b"]);
    }

    private static CardsService CreateService(
        IUnitOfWork unitOfWork,
        CardDataService? cardDataService = null,
        IUserSettingsService? userSettingsService = null)
    {
        var settingsServiceMock = new Mock<IUserSettingsService>();
        settingsServiceMock.Setup(s => s.GetMarketProviderAsync(It.IsAny<string>()))
            .ReturnsAsync(MarketProvider.Mkm);
        settingsServiceMock.Setup(s => s.ResolveCurrency(It.IsAny<MarketProvider>()))
            .Returns((MarketProvider provider) => provider == MarketProvider.Mkm ? Currency.Eur : Currency.Usd);

        return new CardsService(
            unitOfWork,
            cardDataService ?? CardDataServiceTestHelper.CreateWithCards([]),
            userSettingsService ?? settingsServiceMock.Object);
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

    private static Collection CreateCollection(string ownerId, int numberOfCards = 0, double totalPrice = 0)
    {
        return new Collection
        {
            Name = "Collection",
            Color = "Blue",
            NumberOfCards = numberOfCards,
            TotalPrice = totalPrice,
            OwnerId = ownerId
        };
    }

    private static Card CreateCard(
        int collectionId,
        string name,
        string scryfallId,
        string oracleId,
        int quantity,
        double purchasePrice,
        bool isFoil = false)
    {
        return new Card
        {
            Name = name,
            ScryfallId = scryfallId,
            OracleId = oracleId,
            CollectionId = collectionId,
            Quantity = quantity,
            Language = CardLanguage.En,
            Condition = Condition.NearMint,
            IsFoil = isFoil,
            PurchasePrice = purchasePrice,
            PurchasePriceCurrency = Currency.Usd,
            ImageUrl = "http://image",
            BackImageUrl = null,
            ArtCrop = "http://art",
            SetCode = "set",
            SetName = "Set",
            TypeLine = "Creature",
            CollectorNumber = "1",
            Rarity = "common",
            IsMisprint = false,
            IsAltered = false
        };
    }
}

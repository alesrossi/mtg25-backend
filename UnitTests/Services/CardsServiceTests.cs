using System;
using System.Threading.Tasks;
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
using Xunit;

namespace UnitTests.Services;

public class CardsServiceTests
{
    [Fact]
    public async Task UpdateCardAsync_ChangesQuantity_RecalculatesCollectionTotals()
    {
        await using var context = CreateContext();
        var unitOfWork = CreateUnitOfWork(context);
        var ownerId = "user-1";
        var collection = CreateCollection(ownerId, numberOfCards: 1, totalPrice: 2.00);
        context.Collections.Add(collection);
        await context.SaveChangesAsync();

        var card = CreateCard(collection.Id, "Card A", "sf-1", quantity: 1, purchasePrice: 2.00);
        context.Cards.Add(card);
        await context.SaveChangesAsync();

        var service = CreateService(unitOfWork);
        var updateDto = new UpdateCollectionCardDto
        {
            CollectionId = collection.Id,
            Quantity = 3,
            Language = Language.En,
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
        var ownerId = "user-1";
        var collection = CreateCollection(ownerId, numberOfCards: 1, totalPrice: 2.00);
        context.Collections.Add(collection);
        await context.SaveChangesAsync();

        var card = CreateCard(collection.Id, "Card A", "old-id", quantity: 1, purchasePrice: 2.00);
        context.Cards.Add(card);
        await context.SaveChangesAsync();

        var builder = new TestDataBuilder();
        var scryfallCard = builder.CreateOracleCard(id: "new-id", name: "Card A")
            with
            {
                SetId = "set-id-123",
                SetName = "Set Name",
                CollectorNumber = "42",
                Rarity = "rare",
                ImageUris = new ImageUris("small", "normal", "large", "png", "art", "border")
            };
        var cardDataService = CardDataServiceTestHelper.CreateWithCards(new[] { scryfallCard });

        var service = CreateService(unitOfWork, cardDataService);
        var updateDto = new UpdateCollectionCardWithSfIdDto
        {
            CollectionId = collection.Id,
            Quantity = 2,
            Language = Language.En,
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
        var ownerId = "user-1";
        var collection = CreateCollection(ownerId);
        context.Collections.Add(collection);
        await context.SaveChangesAsync();

        var builder = new TestDataBuilder();
        var scryfallCard = builder.CreateOracleCard(id: "sf-1", name: "Card A")
            with { Prices = new Prices(null, null, "1.50", null, null) };
        var cardDataService = CardDataServiceTestHelper.CreateWithCards(new[] { scryfallCard });

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
            Language = Language.En,
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
        var ownerId = "user-1";
        var collection = CreateCollection(ownerId);
        context.Collections.Add(collection);
        await context.SaveChangesAsync();

        var card = CreateCard(collection.Id, "Card A", "sf-1", quantity: 1, purchasePrice: 2.00, isFoil: true);
        context.Cards.Add(card);
        await context.SaveChangesAsync();

        var builder = new TestDataBuilder();
        var scryfallCard = builder.CreateOracleCard(id: "sf-1", name: "Card A")
            with { Prices = new Prices(null, "2.50", null, "2.50", null) };
        var cardDataService = CardDataServiceTestHelper.CreateWithCards(new[] { scryfallCard });

        var settingsServiceMock = new Mock<IUserSettingsService>();
        settingsServiceMock.Setup(s => s.GetMarketProviderAsync(ownerId))
            .ReturnsAsync(MarketProvider.Mkm);

        var service = CreateService(unitOfWork, cardDataService, settingsServiceMock.Object);

        var dto = await service.GetCardByIdAsync(card.Id, ownerId);

        dto.Price.Should().Be(2.50);
        dto.PriceCurrency.Should().Be(MarketProvider.Mkm);
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
            cardDataService ?? CardDataServiceTestHelper.CreateWithCards(Array.Empty<ScryfallCardDto>()),
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
        int quantity,
        double purchasePrice,
        bool isFoil = false)
    {
        return new Card
        {
            Name = name,
            ScryfallId = scryfallId,
            CollectionId = collectionId,
            Quantity = quantity,
            Language = Language.En,
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

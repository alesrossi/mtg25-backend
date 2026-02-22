using API.Dtos.Cards;
using API.Dtos.Decks;
using API.Services;
using Core.Enums;
using Core.Interfaces;
using Core.Models;
using Core.Models.Identity;
using FluentAssertions;
using Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TestUtilities.Builders;
using TestUtilities.Scryfall;

namespace UnitTests.Services;

public class DeckServiceTests
{
    private readonly TestDataBuilder _builder = new();

    [Fact]
    public async Task GetDecksForUserAsync_WhenNoDecks_ReturnsNotFound()
    {
        await using var context = CreateContext();
        var unitOfWork = CreateUnitOfWork(context);
        var user = CreateUser("user-1");
        var userManager = CreateUserManagerMock(user);
        var deckService = CreateService(unitOfWork, userManager.Object);

        Func<Task> act = () => deckService.GetDecksForUserAsync(user.Id);

        var exception = await act.Should().ThrowAsync<DeckServiceException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        exception.Which.IncludeBody.Should().BeTrue();
        exception.Which.Body.Should().Be("Errors.Decks.NoneFound");
    }

    [Fact]
    public async Task ExportDeckAsync_WhenDeckHasNoCards_ReturnsEmptyList()
    {
        await using var context = CreateContext();
        var unitOfWork = CreateUnitOfWork(context);
        var user = CreateUser("user-1");
        var deck = new Deck
        {
            Name = "Empty Deck",
            Format = DeckFormat.Modern,
            OwnerId = user.Id,
            NumberOfCards = 0,
            TotalPrice = 0,
            DeckList = string.Empty
        };
        context.Decks.Add(deck);
        await context.SaveChangesAsync();

        var userManager = CreateUserManagerMock(user);
        var deckService = CreateService(unitOfWork, userManager.Object);

        var result = await deckService.ExportDeckAsync(deck.Id, user.Id);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateDeckCardVersionAsync_InvalidScryfallId_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        var unitOfWork = CreateUnitOfWork(context);
        var user = CreateUser("user-1");
        var deck = new Deck
        {
            Name = "Deck",
            Format = DeckFormat.Modern,
            OwnerId = user.Id,
            NumberOfCards = 0,
            TotalPrice = 0,
            DeckList = string.Empty
        };
        context.Decks.Add(deck);
        await context.SaveChangesAsync();
        var deckCard = new DeckCard
        {
            DeckId = deck.Id,
            Deck = deck,
            ScryfallId = "existing",
            OracleId = "existing",
            Name = "Lightning Bolt",
            SetCode = "lea",
            TypeLine = "Instant",
            MaindeckQuantity = 1,
            SideboardQuantity = 0
        };
        context.DeckCards.Add(deckCard);
        await context.SaveChangesAsync();

        var userManager = CreateUserManagerMock(user);
        var deckService = CreateService(unitOfWork, userManager.Object);

        Func<Task> act = () => deckService.UpdateDeckCardVersionAsync(
            deck.Id,
            deckCard.Id,
            new UpdateDeckCardVersionDto { ScryfallId = "missing" },
            user.Id);

        var exception = await act.Should().ThrowAsync<DeckServiceException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        exception.Which.IncludeBody.Should().BeTrue();
        exception.Which.Body.Should().BeEquivalentTo(new { errors = new[] { "Invalid Scryfall ID provided." } });
    }

    [Fact]
    public async Task ImportDeckFromDecklistAsync_WhenNoCardsParsed_DoesNotCreateDeck()
    {
        await using var context = CreateContext();
        var unitOfWork = CreateUnitOfWork(context);
        var user = CreateUser("user-1");
        var userManager = CreateUserManagerMock(user);
        var parserMock = new Mock<IDecklistParserService>();
        parserMock.Setup(p => p.ParseAsync(It.IsAny<string[]>()))
            .ReturnsAsync(new DecklistParseResult([], ["Invalid line"]));

        var deckService = CreateService(unitOfWork, userManager.Object, parserMock.Object);

        Func<Task> act = () => deckService.ImportDeckFromDecklistAsync(
            new DeckImportRequestDto
            {
                Name = "Imported",
                Format = DeckFormat.Legacy,
                Decklist = "invalid"
            },
            user.Id);

        var exception = await act.Should().ThrowAsync<DeckServiceException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        exception.Which.IncludeBody.Should().BeTrue();
        (await context.Decks.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task GetMissingDeckCardsAsync_ExcludesBasicLands()
    {
        // Arrange
        await using var context = CreateContext();
        var unitOfWork = CreateUnitOfWork(context);
        var user = CreateUser("user-1");
        var deck = new Deck
        {
            Name = "Test Deck",
            Format = DeckFormat.Modern,
            OwnerId = user.Id,
            NumberOfCards = 0,
            TotalPrice = 0,
            DeckList = string.Empty
        };
        context.Decks.Add(deck);
        await context.SaveChangesAsync();

        // Create card data for basic lands and regular cards
        var islandCard = _builder.CreateOracleCard("island-1", "oracle-island", "Island", "MIR", "Mirage") with
        {
            TypeLine = "Basic Land — Island"
        };
        var forestCard = _builder.CreateOracleCard("forest-1", "oracle-forest", "Forest", "MIR", "Mirage") with
        {
            TypeLine = "Basic Land — Forest"
        };
        var boltCard = _builder.CreateOracleCard("bolt-1", "oracle-bolt", "Lightning Bolt", "LEA", "Limited Edition Alpha") with
        {
            TypeLine = "Instant"
        };
        var counterCard = _builder.CreateOracleCard("counter-1", "oracle-counter", "Counterspell", "LEA", "Limited Edition Alpha") with
        {
            TypeLine = "Instant"
        };

        var cardDataService = CardDataServiceTestHelper.CreateWithCards([islandCard, forestCard, boltCard, counterCard]);
        var settingsServiceMock = new Mock<IUserSettingsService>();
        settingsServiceMock.Setup(s => s.GetMarketProviderAsync(It.IsAny<string>()))
            .ReturnsAsync(MarketProvider.Mkm);
        settingsServiceMock.Setup(s => s.ResolveCurrency(It.IsAny<MarketProvider>()))
            .Returns((MarketProvider provider) => provider == MarketProvider.Mkm ? Currency.Eur : Currency.Usd);

        var deckCardService = new DeckCardService(
            unitOfWork,
            NullLogger<DeckCardService>.Instance,
            cardDataService,
            settingsServiceMock.Object);

        // Create deck cards: 2 basic lands and 2 regular cards (none owned)
        var islandDeckCard = new DeckCard
        {
            DeckId = deck.Id,
            Deck = deck,
            ScryfallId = islandCard.Id,
            OracleId = islandCard.OracleId,
            Name = islandCard.Name,
            SetCode = islandCard.Set,
            SetName = islandCard.SetName,
            TypeLine = islandCard.TypeLine,
            MaindeckQuantity = 4,
            SideboardQuantity = 0
        };
        var forestDeckCard = new DeckCard
        {
            DeckId = deck.Id,
            Deck = deck,
            ScryfallId = forestCard.Id,
            OracleId = forestCard.OracleId,
            Name = forestCard.Name,
            SetCode = forestCard.Set,
            SetName = forestCard.SetName,
            TypeLine = forestCard.TypeLine,
            MaindeckQuantity = 3,
            SideboardQuantity = 0
        };
        var boltDeckCard = new DeckCard
        {
            DeckId = deck.Id,
            Deck = deck,
            ScryfallId = boltCard.Id,
            OracleId = boltCard.OracleId,
            Name = boltCard.Name,
            SetCode = boltCard.Set,
            SetName = boltCard.SetName,
            TypeLine = boltCard.TypeLine,
            MaindeckQuantity = 4,
            SideboardQuantity = 0
        };
        var counterDeckCard = new DeckCard
        {
            DeckId = deck.Id,
            Deck = deck,
            ScryfallId = counterCard.Id,
            OracleId = counterCard.OracleId,
            Name = counterCard.Name,
            SetCode = counterCard.Set,
            SetName = counterCard.SetName,
            TypeLine = counterCard.TypeLine,
            MaindeckQuantity = 2,
            SideboardQuantity = 0
        };

        context.DeckCards.AddRange(islandDeckCard, forestDeckCard, boltDeckCard, counterDeckCard);
        await context.SaveChangesAsync();

        var userManager = CreateUserManagerMock(user);
        var deckService = new DeckService(
            unitOfWork,
            userManager.Object,
            deckCardService,
            new ValidationService(),
            new Mock<IDecklistParserService>().Object,
            cardDataService,
            settingsServiceMock.Object,
            new Mock<IDeckHistoryService>().Object);

        // Act
        var result = await deckService.GetMissingDeckCardsAsync(deck.Id, user.Id);

        // Assert
        result.Should().HaveCount(2);
        result.Select(dc => dc.Name).Should().BeEquivalentTo(new[] { "Lightning Bolt", "Counterspell" });
        result.Should().NotContain(dc => dc.Name == "Island" || dc.Name == "Forest");
        result.All(dc => !dc.IsOwned).Should().BeTrue();
    }

    private static DeckService CreateService(IUnitOfWork unitOfWork, UserManager<AppUser> userManager, IDecklistParserService? parserService = null)
    {
        var cardDataService = CardDataServiceTestHelper.CreateWithCards(Array.Empty<ScryfallCardDto>());
        var settingsServiceMock = new Mock<IUserSettingsService>();
        settingsServiceMock.Setup(s => s.GetMarketProviderAsync(It.IsAny<string>()))
            .ReturnsAsync(MarketProvider.Mkm);
        settingsServiceMock.Setup(s => s.ResolveCurrency(It.IsAny<MarketProvider>()))
            .Returns((MarketProvider provider) => provider == MarketProvider.Mkm ? Currency.Eur : Currency.Usd);

        var deckCardService = new DeckCardService(
            unitOfWork,
            NullLogger<DeckCardService>.Instance,
            cardDataService,
            settingsServiceMock.Object);

        var historyServiceMock = new Mock<IDeckHistoryService>();
        historyServiceMock.Setup(h => h.InitializeDeckHistoryAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeckCommit
            {
                DeckId = 0,
                TreeId = 0,
                AuthorId = "user",
                Message = "Initial commit"
            });

        return new DeckService(
            unitOfWork,
            userManager,
            deckCardService,
            new ValidationService(),
            parserService ?? new Mock<IDecklistParserService>().Object,
            cardDataService,
            settingsServiceMock.Object,
            historyServiceMock.Object);
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

    private static Mock<UserManager<AppUser>> CreateUserManagerMock(params AppUser[] users)
    {
        var store = new Mock<IUserStore<AppUser>>();
        var manager = new Mock<UserManager<AppUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        foreach (var user in users)
        {
            manager.Setup(m => m.FindByIdAsync(user.Id)).ReturnsAsync(user);
        }
        return manager;
    }

    private static AppUser CreateUser(string id)
        => new()
        {
            Id = id,
            DisplayName = id,
            FirstName = id,
            LastName = "User",
            UserName = id,
            Email = $"{id}@example.com"
        };
}

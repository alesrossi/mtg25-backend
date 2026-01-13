using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Dtos.Cards;
using API.Dtos.Decks;
using API.Services;
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
using TestUtilities.Scryfall;
using Xunit;

namespace UnitTests.Services;

public class DeckServiceTests
{
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
        exception.Which.Body.Should().Be("No decks found");
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
            Format = "Modern",
            OwnerId = user.Id,
            NumberOfCards = 0,
            TotalPrice = 0
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
            Format = "Modern",
            OwnerId = user.Id,
            NumberOfCards = 0,
            TotalPrice = 0
        };
        context.Decks.Add(deck);
        await context.SaveChangesAsync();
        var deckCard = new DeckCard
        {
            DeckId = deck.Id,
            Deck = deck,
            ScryfallId = "existing",
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
            .ReturnsAsync(new DecklistParseResult(Array.Empty<CreateDeckCardDto>(), new[] { "Invalid line" }));

        var deckService = CreateService(unitOfWork, userManager.Object, parserMock.Object);

        Func<Task> act = () => deckService.ImportDeckFromDecklistAsync(
            new DeckImportRequestDto
            {
                Name = "Imported",
                Format = "Legacy",
                Decklist = "invalid"
            },
            user.Id);

        var exception = await act.Should().ThrowAsync<DeckServiceException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        exception.Which.IncludeBody.Should().BeTrue();
        (await context.Decks.CountAsync()).Should().Be(0);
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

        return new DeckService(
            unitOfWork,
            userManager,
            deckCardService,
            new ValidationService(),
            parserService ?? new Mock<IDecklistParserService>().Object,
            cardDataService,
            settingsServiceMock.Object);
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
        var manager = new Mock<UserManager<AppUser>>(store.Object, null, null, null, null, null, null, null, null);
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

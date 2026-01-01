using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.Dtos.Cards;
using API.Dtos.Trades;
using API.Services;
using Core.Interfaces;
using Core.Models;
using Core.Models.Identity;
using Core.Specifications;
using FluentAssertions;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TestUtilities.Builders;
using TestUtilities.Scryfall;

namespace UnitTests.Services;

public class TradeConnectionServiceTests : IDisposable
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IGenericRepository<Wishlist>> _wishlistRepositoryMock = new();
    private readonly Mock<IGenericRepository<BinderCard>> _binderCardRepositoryMock = new();
    private readonly Mock<ITradeSessionStore> _sessionStoreMock = new();
    private readonly AppIdentityDbContext _identityDbContext;
    private readonly NotificationService _notificationService;
    private readonly CardDataService _cardDataService;
    private readonly Mock<IUserSettingsService> _userSettingsServiceMock = new();
    private readonly TestDataBuilder _testDataBuilder = new();
    private int _idSequence = 1;

    public TradeConnectionServiceTests()
    {
        _identityDbContext = new AppIdentityDbContext(new DbContextOptionsBuilder<AppIdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        _notificationService = new NotificationService(_identityDbContext, NullLogger<NotificationService>.Instance);
        _cardDataService = CardDataServiceTestHelper.CreateWithCards(Array.Empty<ScryfallCardDto>());
        _userSettingsServiceMock.Setup(s => s.GetMarketProviderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MarketProvider.Mkm);
        _userSettingsServiceMock.Setup(s => s.ResolveCurrency(It.IsAny<MarketProvider>()))
            .Returns((MarketProvider provider) => provider == MarketProvider.Mkm ? Currency.Eur : Currency.Usd);

        _unitOfWorkMock.Setup(x => x.Repository<Wishlist>()).Returns(_wishlistRepositoryMock.Object);
        _unitOfWorkMock.Setup(x => x.Repository<BinderCard>()).Returns(_binderCardRepositoryMock.Object);
    }

    [Fact]
    public async Task PrepareConnectionAsync_ProjectsCardsAndMatches()
    {
        // Arrange
        var initiator = CreateUser("initiator");
        var partner = CreateUser("partner");

        var initiatorWishlists = new List<Wishlist>
        {
            CreateWishlistWithCard(initiator.Id, "Trade Match")
        };

        var partnerWishlists = new List<Wishlist>
        {
            CreateWishlistWithCard(partner.Id, "Trade Match")
        };

        var initiatorBinderCards = new List<BinderCard>
        {
            CreateBinderCard(initiator.Id, "Trade Match", quantityToTrade: 2)
        };

        var partnerBinderCards = new List<BinderCard>
        {
            CreateBinderCard(partner.Id, "Other Card")
        };

        SeedCardMarketData(initiatorBinderCards.Concat(partnerBinderCards));

        SetupWishlistRepository(initiatorWishlists.Concat(partnerWishlists).ToList());
        SetupBinderCardRepository(initiatorBinderCards.Concat(partnerBinderCards).ToList());

        var userManager = CreateUserManagerMock(initiator, partner);
        _sessionStoreMock.Setup(s => s.StoreAsync(It.IsAny<TradeConnectionDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var service = CreateService(userManager.Object);

        // Act
        var result = await service.PrepareConnectionAsync(initiator.Id, partner.Id, CancellationToken.None);

        // Assert
        result.TradeId.Should().NotBeNullOrWhiteSpace();
        result.InitiatorMatches.Should().ContainSingle(m => m.CardName == "Trade Match");
        result.PartnerMatches.Should().BeEmpty();
        result.InitiatorTotalValue.Should().Be(4.00);
        result.PartnerTotalValue.Should().Be(0);
        result.ValueDifference.Should().Be(4.00);
        result.InitiatorMatches.Single().MatchId.Should().NotBeNullOrWhiteSpace();
        result.PriceProvider.Should().Be(MarketProvider.Mkm);
        result.PriceCurrency.Should().Be(Currency.Eur);
        result.InitiatorMatches.Single().OfferingCard.MarketPrice.Should().Be(2);
        result.InitiatorMatches.Single().OfferingCard.TotalValue.Should().Be(4.00);
        result.InitiatorMatches.Single().OfferingCard.MaxQuantityToTrade.Should().Be(2);
        result.InitiatorMatches.Single().IsSelected.Should().BeTrue();
        _identityDbContext.Notifications.Should().ContainSingle(n => n.AppUserId == partner.Id);
        _sessionStoreMock.Verify();
    }

    [Fact]
    public async Task GetConnectionAsync_WhenUserNotParticipant_Throws()
    {
        // Arrange
        var userManager = CreateUserManagerMock();
        var service = CreateService(userManager.Object);
        var connection = new TradeConnectionDto
        {
            TradeId = "trade-123",
            Initiator = new TradeParticipantDto { UserId = "init" },
            Partner = new TradeParticipantDto { UserId = "partner" }
        };

        _sessionStoreMock.Setup(s => s.GetAsync("trade-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);

        // Act
        Func<Task> act = () => service.GetConnectionAsync("trade-123", "intruder", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task UpdateConnectionAsync_AdjustsQuantitiesAndSelection()
    {
        // Arrange
        var initiator = CreateUser("initiator");
        var partner = CreateUser("partner");

        var initiatorWishlists = new List<Wishlist>
        {
            CreateWishlistWithCard(initiator.Id, "Trade Match")
        };

        var partnerWishlists = new List<Wishlist>
        {
            CreateWishlistWithCard(partner.Id, "Trade Match")
        };

        var initiatorBinderCards = new List<BinderCard>
        {
            CreateBinderCard(initiator.Id, "Trade Match", quantityToTrade: 2)
        };

        var partnerBinderCards = new List<BinderCard>();

        SeedCardMarketData(initiatorBinderCards);

        SetupWishlistRepository(initiatorWishlists.Concat(partnerWishlists).ToList());
        SetupBinderCardRepository(initiatorBinderCards.Concat(partnerBinderCards).ToList());

        var userManager = CreateUserManagerMock(initiator, partner);

        TradeConnectionDto? storedConnection = null;
        _sessionStoreMock.Setup(s => s.StoreAsync(It.IsAny<TradeConnectionDto>(), It.IsAny<CancellationToken>()))
            .Callback<TradeConnectionDto, CancellationToken>((connection, _) => storedConnection = connection)
            .Returns(Task.CompletedTask);

        var service = CreateService(userManager.Object);
        var connection = await service.PrepareConnectionAsync(initiator.Id, partner.Id, CancellationToken.None);

        _sessionStoreMock.Setup(s => s.GetAsync(connection.TradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedConnection);

        var matchId = storedConnection!.InitiatorMatches.Single().MatchId;
        var request = new UpdateTradeRequest
        {
            InitiatorMatches = new[]
            {
                new TradeMatchUpdateDto
                {
                    MatchId = matchId,
                    QuantityToTrade = 1,
                    IsSelected = false
                }
            }
        };

        // Act
        var updated = await service.UpdateConnectionAsync(connection.TradeId, initiator.Id, request, CancellationToken.None);

        // Assert
        updated.InitiatorMatches.Single().OfferingCard.QuantityToTrade.Should().Be(1);
        updated.InitiatorMatches.Single().OfferingCard.TotalValue.Should().Be(2.00);
        updated.InitiatorMatches.Single().IsSelected.Should().BeFalse();
        updated.InitiatorTotalValue.Should().Be(0);
        updated.ValueDifference.Should().Be(0);
        _sessionStoreMock.Verify(s => s.StoreAsync(It.IsAny<TradeConnectionDto>(), It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task UpdateConnectionAsync_WhenQuantityExceedsMax_Throws()
    {
        // Arrange
        var initiator = CreateUser("initiator");
        var partner = CreateUser("partner");

        var initiatorWishlists = new List<Wishlist>
        {
            CreateWishlistWithCard(initiator.Id, "Trade Match")
        };

        var partnerWishlists = new List<Wishlist>
        {
            CreateWishlistWithCard(partner.Id, "Trade Match")
        };

        var initiatorBinderCards = new List<BinderCard>
        {
            CreateBinderCard(initiator.Id, "Trade Match", quantityToTrade: 1)
        };

        SeedCardMarketData(initiatorBinderCards);
        SetupWishlistRepository(initiatorWishlists.Concat(partnerWishlists).ToList());
        SetupBinderCardRepository(initiatorBinderCards);

        var userManager = CreateUserManagerMock(initiator, partner);
        TradeConnectionDto? storedConnection = null;
        _sessionStoreMock.Setup(s => s.StoreAsync(It.IsAny<TradeConnectionDto>(), It.IsAny<CancellationToken>()))
            .Callback<TradeConnectionDto, CancellationToken>((connection, _) => storedConnection = connection)
            .Returns(Task.CompletedTask);

        var service = CreateService(userManager.Object);
        var connection = await service.PrepareConnectionAsync(initiator.Id, partner.Id, CancellationToken.None);

        _sessionStoreMock.Setup(s => s.GetAsync(connection.TradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedConnection);

        var matchId = storedConnection!.InitiatorMatches.Single().MatchId;
        var request = new UpdateTradeRequest
        {
            InitiatorMatches = new[]
            {
                new TradeMatchUpdateDto
                {
                    MatchId = matchId,
                    QuantityToTrade = 5
                }
            }
        };

        // Act
        Func<Task> act = () => service.UpdateConnectionAsync(connection.TradeId, initiator.Id, request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CancelConnectionAsync_RemovesTradeFromStore()
    {
        // Arrange
        var connection = new TradeConnectionDto
        {
            TradeId = "trade-123",
            Initiator = new TradeParticipantDto { UserId = "init" },
            Partner = new TradeParticipantDto { UserId = "partner" },
            InitiatorMatches = Array.Empty<TradeMatchDto>(),
            PartnerMatches = Array.Empty<TradeMatchDto>()
        };

        _sessionStoreMock.Setup(s => s.GetAsync("trade-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);
        _sessionStoreMock.Setup(s => s.DeleteAsync("trade-123", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var userManager = CreateUserManagerMock();
        var service = CreateService(userManager.Object);

        // Act
        await service.CancelConnectionAsync("trade-123", "init", CancellationToken.None);

        // Assert
        _sessionStoreMock.Verify();
    }

    private TradeConnectionService CreateService(UserManager<AppUser> userManager)
    {
        return new TradeConnectionService(
            userManager,
            _unitOfWorkMock.Object,
            _sessionStoreMock.Object,
            _notificationService,
            _cardDataService,
            _userSettingsServiceMock.Object);
    }

    private AppUser CreateUser(string key)
    {
        var user = _testDataBuilder.CreateUser($"{key}@test.com", key);
        user.Id = key;
        user.DisplayName = key;
        user.FirstName = "First";
        user.LastName = "Last";
        return user;
    }

    private Wishlist CreateWishlistWithCard(string ownerId, string cardName)
    {
        var wishlist = _testDataBuilder.CreateWishlist(ownerId, isPublic: true);
        wishlist.Id = NextId();
        var card = _testDataBuilder.CreateWishlistCard(wishlist.Id, Guid.NewGuid().ToString(), cardName);
        card.Id = NextId();
        wishlist.WishlistCards = new List<WishlistCard> { card };
        return wishlist;
    }

    private BinderCard CreateBinderCard(string ownerId, string cardName, int quantityToTrade = 1)
    {
        var binder = _testDataBuilder.CreateTradeBinder(ownerId, isPublic: true);
        binder.Id = NextId();

        var collection = _testDataBuilder.CreateCollection(ownerId);
        collection.Id = NextId();

        var ownedCard = _testDataBuilder.CreateCard(collection.Id, cardName, price: 1);
        ownedCard.Id = NextId();
        ownedCard.IsFoil = false;

        var binderCard = _testDataBuilder.CreateBinderCard(binder.Id, ownedCard.Id, cardName, quantityToTrade);
        binderCard.Id = NextId();
        binderCard.Card = ownedCard;
        binderCard.TradeBinder = binder;
        return binderCard;
    }

    private int NextId() => _idSequence++;

    private void SetupWishlistRepository(IReadOnlyList<Wishlist> wishlists)
    {
        _wishlistRepositoryMock
            .Setup(r => r.ListAsync(It.IsAny<ISpecification<Wishlist>>(), It.IsAny<bool>()))
            .ReturnsAsync((ISpecification<Wishlist> spec, bool _) =>
            {
                var predicate = spec.Criteria?.Compile() ?? (_ => true);
                return wishlists.Where(predicate).ToList();
            });
    }

    private void SetupBinderCardRepository(IReadOnlyList<BinderCard> cards)
    {
        _binderCardRepositoryMock
            .Setup(r => r.ListAsync(It.IsAny<ISpecification<BinderCard>>(), It.IsAny<bool>()))
            .ReturnsAsync((ISpecification<BinderCard> spec, bool _) =>
            {
                var predicate = spec.Criteria?.Compile() ?? (_ => true);
                return cards.Where(predicate).ToList();
            });
    }

    private void SeedCardMarketData(IEnumerable<BinderCard> binderCards)
    {
        var entries = binderCards.Select(card =>
        {
            var scryfallCard = _testDataBuilder.CreateOracleCard(
                id: card.Card!.ScryfallId,
                name: card.Card!.Name);

            return scryfallCard with
            {
                Prices = new Prices(
                    Usd: "1.00",
                    UsdFoil: "1.10",
                    Eur: "2.00",
                    EurFoil: "2.10",
                    Tix: null)
            };
        }).ToList();

        CardDataServiceTestHelper.Populate(_cardDataService, entries);
    }

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

    public void Dispose()
    {
        _identityDbContext.Dispose();
    }
}

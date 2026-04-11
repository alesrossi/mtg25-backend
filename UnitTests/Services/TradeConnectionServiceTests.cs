using API.Constants;
using API.Dtos.Binders;
using API.Dtos.Cards;
using API.Dtos.Trades;
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

public class TradeConnectionServiceTests : IDisposable
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IGenericRepository<Wishlist>> _wishlistRepositoryMock = new();
    private readonly Mock<IGenericRepository<BinderCard>> _binderCardRepositoryMock = new();
    private readonly Mock<IGenericRepository<Collection>> _collectionRepositoryMock = new();
    private readonly Mock<ITradeSessionStore> _sessionStoreMock = new();
    private readonly MainContext _identityDbContext;
    private readonly IUnitOfWork _notificationUow;
    private readonly NotificationService _notificationService;
    private readonly CardDataService _cardDataService;
    private readonly Mock<IUserSettingsService> _userSettingsServiceMock = new();
    private readonly Mock<IUserSettingsService> _notificationSettingsMock = new();
    private readonly Mock<IMessageLocalizer> _messageLocalizerMock = new();
    private readonly TestDataBuilder _testDataBuilder = new();
    private readonly Dictionary<string, string> _oracleIdsByName = new(StringComparer.OrdinalIgnoreCase);
    private int _idSequence = 1;

    public TradeConnectionServiceTests()
    {
        _identityDbContext = new MainContext(new DbContextOptionsBuilder<MainContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _notificationUow = new UnitOfWork(_identityDbContext, NullLogger<UnitOfWork>.Instance, NullLoggerFactory.Instance);

        _notificationSettingsMock.Setup(s => s.GetSettingsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string userId, CancellationToken _) => new Settings
            {
                AppUserId = userId,
                AppUser = null!,
                LanguageUi = Language.It
            });
        _messageLocalizerMock.Setup(l => l.GetMessageForLanguage(It.IsAny<Language?>(), It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns("localized");

        _notificationService = new NotificationService(
            _notificationUow,
            NullLogger<NotificationService>.Instance,
            _notificationSettingsMock.Object,
            _messageLocalizerMock.Object);
        _cardDataService = CardDataServiceTestHelper.CreateWithCards([]);
        _userSettingsServiceMock.Setup(s => s.GetMarketProviderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MarketProvider.Mkm);
        _userSettingsServiceMock.Setup(s => s.ResolveCurrency(It.IsAny<MarketProvider>()))
            .Returns((MarketProvider provider) => provider == MarketProvider.Mkm ? Currency.Eur : Currency.Usd);

        _unitOfWorkMock.Setup(x => x.Repository<Wishlist>()).Returns(_wishlistRepositoryMock.Object);
        _unitOfWorkMock.Setup(x => x.Repository<BinderCard>()).Returns(_binderCardRepositoryMock.Object);
        _unitOfWorkMock.Setup(x => x.Repository<Collection>()).Returns(_collectionRepositoryMock.Object);
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
        _sessionStoreMock.Setup(s => s.StoreAsync(It.IsAny<TradeConnectionDto>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var service = CreateService(userManager.Object);

        // Act
        var result = await service.PrepareConnectionAsync(initiator.Id, partner.Id, liveTrading: true, CancellationToken.None);

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
        result.InitiatorMatches.Single().IsSelected.Should().BeTrue();
        _sessionStoreMock.Verify();
    }

    [Fact]
    public async Task PrepareConnectionAsync_WhenNotLiveTrading_UsesExtendedExpiration()
    {
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
        TimeSpan? capturedTtl = null;
        _sessionStoreMock.Setup(s => s.StoreAsync(It.IsAny<TradeConnectionDto>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Callback<TradeConnectionDto, TimeSpan?, CancellationToken>((_, ttl, _) => capturedTtl = ttl)
            .Returns(Task.CompletedTask);

        var service = CreateService(userManager.Object);
        await service.PrepareConnectionAsync(initiator.Id, partner.Id, false, CancellationToken.None);

        capturedTtl.Should().Be(TimeSpan.FromHours(12));
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

        // ReSharper disable once CollectionNeverUpdated.Local
        var partnerBinderCards = new List<BinderCard>();

        SeedCardMarketData(initiatorBinderCards);

        SetupWishlistRepository(initiatorWishlists.Concat(partnerWishlists).ToList());
        SetupBinderCardRepository(initiatorBinderCards.Concat(partnerBinderCards).ToList());

        var userManager = CreateUserManagerMock(initiator, partner);

        TradeConnectionDto? storedConnection = null;
        _sessionStoreMock.Setup(s => s.StoreAsync(It.IsAny<TradeConnectionDto>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Callback<TradeConnectionDto, TimeSpan?, CancellationToken>((connection, _, _) => storedConnection = connection)
            .Returns(Task.CompletedTask);

        var service = CreateService(userManager.Object);
        var connection = await service.PrepareConnectionAsync(initiator.Id, partner.Id, true, CancellationToken.None);

        _sessionStoreMock.Setup(s => s.GetAsync(connection.TradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedConnection);

        var matchId = storedConnection!.InitiatorMatches.Single().MatchId;
        var request = new UpdateTradeRequest
        {
            InitiatorMatches =
            [
                new TradeMatchUpdateDto
                {
                    MatchId = matchId,
                    QuantityToTrade = 1,
                    IsSelected = false
                }
            ]
        };

        // Act
        var updated = await service.UpdateConnectionAsync(connection.TradeId, initiator.Id, request, CancellationToken.None);

        // Assert
        updated.InitiatorMatches.Single().OfferingCard.QuantityToTrade.Should().Be(1);
        updated.InitiatorMatches.Single().OfferingCard.TotalValue.Should().Be(2.00);
        updated.InitiatorMatches.Single().IsSelected.Should().BeFalse();
        updated.InitiatorTotalValue.Should().Be(0);
        updated.ValueDifference.Should().Be(0);
        _sessionStoreMock.Verify(s => s.StoreAsync(It.IsAny<TradeConnectionDto>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.AtLeast(2));
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
            CreateBinderCard(initiator.Id, "Trade Match", quantityToTrade: 1, collectionQuantity: 2)
        };

        SeedCardMarketData(initiatorBinderCards);
        SetupWishlistRepository(initiatorWishlists.Concat(partnerWishlists).ToList());
        SetupBinderCardRepository(initiatorBinderCards);

        var userManager = CreateUserManagerMock(initiator, partner);
        TradeConnectionDto? storedConnection = null;
        _sessionStoreMock.Setup(s => s.StoreAsync(It.IsAny<TradeConnectionDto>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Callback<TradeConnectionDto, TimeSpan?, CancellationToken>((connection, _, _) => storedConnection = connection)
            .Returns(Task.CompletedTask);

        var service = CreateService(userManager.Object);
        var connection = await service.PrepareConnectionAsync(initiator.Id, partner.Id, true, CancellationToken.None);

        _sessionStoreMock.Setup(s => s.GetAsync(connection.TradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedConnection);

        var matchId = storedConnection!.InitiatorMatches.Single().MatchId;
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

        // Act
        Func<Task> act = () => service.UpdateConnectionAsync(connection.TradeId, initiator.Id, request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpdateConnectionAsync_AllowsInitiatorToSelectCollection()
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

        SeedCardMarketData(initiatorBinderCards);
        SetupWishlistRepository(initiatorWishlists.Concat(partnerWishlists).ToList());
        SetupBinderCardRepository(initiatorBinderCards);

        var userManager = CreateUserManagerMock(initiator, partner);
        TradeConnectionDto? storedConnection = null;
        _sessionStoreMock.Setup(s => s.StoreAsync(It.IsAny<TradeConnectionDto>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Callback<TradeConnectionDto, TimeSpan?, CancellationToken>((connection, _, _) => storedConnection = connection)
            .Returns(Task.CompletedTask);

        var destinationCollection = _testDataBuilder.CreateCollection(initiator.Id);
        destinationCollection.Id = NextId();
        _collectionRepositoryMock.Setup(r => r.GetByIdAsync(destinationCollection.Id, It.IsAny<bool>()))
            .ReturnsAsync(destinationCollection);

        var service = CreateService(userManager.Object);
        var connection = await service.PrepareConnectionAsync(initiator.Id, partner.Id, true, CancellationToken.None);

        _sessionStoreMock.Setup(s => s.GetAsync(connection.TradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedConnection);

        var request = new UpdateTradeRequest
        {
            InitiatorCollectionId = destinationCollection.Id
        };

        // Act
        var updated = await service.UpdateConnectionAsync(connection.TradeId, initiator.Id, request, CancellationToken.None);

        // Assert
        updated.InitiatorCollectionId.Should().Be(destinationCollection.Id);
    }

    [Fact]
    public async Task UpdateConnectionAsync_WhenCollectionNotOwned_Throws()
    {
        // Arrange
        var initiator = CreateUser("initiator");
        var partner = CreateUser("partner");
        var userManager = CreateUserManagerMock(initiator, partner);

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

        TradeConnectionDto? storedConnection = null;
        _sessionStoreMock.Setup(s => s.StoreAsync(It.IsAny<TradeConnectionDto>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Callback<TradeConnectionDto, TimeSpan?, CancellationToken>((connection, _, _) => storedConnection = connection)
            .Returns(Task.CompletedTask);

        var externalCollection = _testDataBuilder.CreateCollection("other-user");
        externalCollection.Id = NextId();
        _collectionRepositoryMock.Setup(r => r.GetByIdAsync(externalCollection.Id, It.IsAny<bool>()))
            .ReturnsAsync(externalCollection);

        var service = CreateService(userManager.Object);
        var connection = await service.PrepareConnectionAsync(initiator.Id, partner.Id, true, CancellationToken.None);

        _sessionStoreMock.Setup(s => s.GetAsync(connection.TradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedConnection);

        var request = new UpdateTradeRequest
        {
            InitiatorCollectionId = externalCollection.Id
        };

        // Act
        Func<Task> act = () => service.UpdateConnectionAsync(connection.TradeId, initiator.Id, request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpdateConnectionAsync_WhenRequesterUpdatesOtherParticipantCollection_Throws()
    {
        // Arrange
        var initiator = CreateUser("initiator");
        var partner = CreateUser("partner");
        var userManager = CreateUserManagerMock(initiator, partner);

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

        TradeConnectionDto? storedConnection = null;
        _sessionStoreMock.Setup(s => s.StoreAsync(It.IsAny<TradeConnectionDto>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Callback<TradeConnectionDto, TimeSpan?, CancellationToken>((connection, _, _) => storedConnection = connection)
            .Returns(Task.CompletedTask);

        var service = CreateService(userManager.Object);
        var connection = await service.PrepareConnectionAsync(initiator.Id, partner.Id, true, CancellationToken.None);

        _sessionStoreMock.Setup(s => s.GetAsync(connection.TradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedConnection);

        var request = new UpdateTradeRequest
        {
            InitiatorCollectionId = 42
        };

        // Act
        Func<Task> act = () => service.UpdateConnectionAsync(connection.TradeId, partner.Id, request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
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
            InitiatorMatches = [],
            PartnerMatches = []
        };

        _sessionStoreMock.Setup(s => s.GetAsync("trade-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);
        _sessionStoreMock.Setup(s => s.DeleteAsync("trade-123", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var notifRepoMock = new Mock<ICompositeRepository<Notification>>();
        notifRepoMock.Setup(r => r.Query).Returns(_identityDbContext.Notifications.AsQueryable());
        _unitOfWorkMock.Setup(x => x.CompositeRepository<Notification>()).Returns(notifRepoMock.Object);

        var userManager = CreateUserManagerMock();
        var service = CreateService(userManager.Object);

        // Act
        await service.CancelConnectionAsync("trade-123", "init", CancellationToken.None);

        // Assert
        _sessionStoreMock.Verify();
    }

    [Fact]
    public async Task CancelConnectionAsync_SetsTradeNotificationsApprovalToFalse()
    {
        // Arrange
        var tradeRequestNotif = new Notification
        {
            Name = NotificationConstants.TradeRequest,
            Message = "Trade request",
            Origin = "trade_request.init",
            ObjectId = "init",
            AppUserId = "partner",
            Approval = true,
            CreationDateTime = DateTime.UtcNow,
            AppUser = null!
        };
        var commitRequestNotif = new Notification
        {
            Name = NotificationConstants.TradeCommitRequest,
            Message = "Commit request",
            Origin = "trade-cancel-notif.init",
            ObjectId = "trade-cancel-notif",
            AppUserId = "partner",
            Approval = true,
            CreationDateTime = DateTime.UtcNow,
            AppUser = null!
        };
        _identityDbContext.Notifications.AddRange(tradeRequestNotif, commitRequestNotif);
        await _identityDbContext.SaveChangesAsync();
        _identityDbContext.ChangeTracker.Clear();

        var connection = new TradeConnectionDto
        {
            TradeId = "trade-cancel-notif",
            Initiator = new TradeParticipantDto { UserId = "init" },
            Partner = new TradeParticipantDto { UserId = "partner" },
            InitiatorMatches = [],
            PartnerMatches = []
        };
        _sessionStoreMock.Setup(s => s.GetAsync("trade-cancel-notif", It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);
        _sessionStoreMock.Setup(s => s.DeleteAsync("trade-cancel-notif", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var userManager = CreateUserManagerMock();
        var service = CreateService(userManager.Object, _notificationUow);

        // Act
        await service.CancelConnectionAsync("trade-cancel-notif", "init", CancellationToken.None);

        // Assert
        _identityDbContext.ChangeTracker.Clear();
        var notifications = await _identityDbContext.Notifications
            .Where(n => n.Id == tradeRequestNotif.Id || n.Id == commitRequestNotif.Id)
            .ToListAsync();
        notifications.Should().HaveCount(2);
        notifications.Should().AllSatisfy(n => n.Approval.Should().BeFalse());
    }

    [Fact]
    public async Task CancelConnectionAsync_DoesNotAffectUnrelatedNotifications()
    {
        // Arrange
        // Notification for a different trade's commit request
        var otherTradeCommitNotif = new Notification
        {
            Name = NotificationConstants.TradeCommitRequest,
            Message = "Other trade commit",
            Origin = "other-trade.init",
            ObjectId = "other-trade-id",
            AppUserId = "partner",
            Approval = true,
            CreationDateTime = DateTime.UtcNow,
            AppUser = null!
        };
        // trade_request between two completely unrelated users
        var unrelatedUserNotif = new Notification
        {
            Name = NotificationConstants.TradeRequest,
            Message = "Unrelated trade request",
            Origin = "trade_request.stranger",
            ObjectId = "stranger",
            AppUserId = "other-user",
            Approval = true,
            CreationDateTime = DateTime.UtcNow,
            AppUser = null!
        };
        _identityDbContext.Notifications.AddRange(otherTradeCommitNotif, unrelatedUserNotif);
        await _identityDbContext.SaveChangesAsync();
        _identityDbContext.ChangeTracker.Clear();

        var connection = new TradeConnectionDto
        {
            TradeId = "trade-cancel-unrelated",
            Initiator = new TradeParticipantDto { UserId = "init" },
            Partner = new TradeParticipantDto { UserId = "partner" },
            InitiatorMatches = [],
            PartnerMatches = []
        };
        _sessionStoreMock.Setup(s => s.GetAsync("trade-cancel-unrelated", It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);
        _sessionStoreMock.Setup(s => s.DeleteAsync("trade-cancel-unrelated", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var userManager = CreateUserManagerMock();
        var service = CreateService(userManager.Object, _notificationUow);

        // Act
        await service.CancelConnectionAsync("trade-cancel-unrelated", "init", CancellationToken.None);

        // Assert
        _identityDbContext.ChangeTracker.Clear();
        var notifications = await _identityDbContext.Notifications
            .Where(n => n.Id == otherTradeCommitNotif.Id || n.Id == unrelatedUserNotif.Id)
            .ToListAsync();
        notifications.Should().AllSatisfy(n => n.Approval.Should().BeTrue());
    }

    [Fact]
    public async Task CommitTradeAsync_TransfersCardsAndUpdatesWishlists()
    {
        // Arrange
        var initiator = CreateUser("initiator");
        var partner = CreateUser("partner");
        var userManager = CreateUserManagerMock(initiator, partner);

        var options = new DbContextOptionsBuilder<MainContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var mainContext = new MainContext(options);
        var unitOfWork = new UnitOfWork(mainContext, NullLogger<UnitOfWork>.Instance, NullLoggerFactory.Instance);

        var initiatorCollection = _testDataBuilder.CreateCollection(initiator.Id);
        initiatorCollection.NumberOfCards = 2;
        mainContext.Collections.Add(initiatorCollection);

        var partnerCollection = _testDataBuilder.CreateCollection(partner.Id);
        partnerCollection.NumberOfCards = 1;
        mainContext.Collections.Add(partnerCollection);

        var initiatorCard = _testDataBuilder.CreateCard(initiatorCollection.Id, "Lightning Bolt", price: 2);
        initiatorCard.Collection = initiatorCollection;
        initiatorCard.Quantity = 2;
        initiatorCard.OracleId = initiatorCard.ScryfallId;
        mainContext.Cards.Add(initiatorCard);

        var partnerCard = _testDataBuilder.CreateCard(partnerCollection.Id, "Counterspell", price: 3);
        partnerCard.Collection = partnerCollection;
        partnerCard.Quantity = 1;
        partnerCard.OracleId = partnerCard.ScryfallId;
        mainContext.Cards.Add(partnerCard);

        var initiatorBinder = _testDataBuilder.CreateTradeBinder(initiator.Id, isPublic: true);
        mainContext.TradeBinders.Add(initiatorBinder);

        var partnerBinder = _testDataBuilder.CreateTradeBinder(partner.Id, isPublic: true);
        mainContext.TradeBinders.Add(partnerBinder);

        var initiatorBinderCard = _testDataBuilder.CreateBinderCard(initiatorBinder.Id, initiatorCard.Id, "Lightning Bolt", quantityToTrade: 1);
        initiatorBinderCard.TradeBinder = initiatorBinder;
        initiatorBinderCard.Card = initiatorCard;
        mainContext.BinderCards.Add(initiatorBinderCard);

        var partnerBinderCard = _testDataBuilder.CreateBinderCard(partnerBinder.Id, partnerCard.Id, "Counterspell", quantityToTrade: 1);
        partnerBinderCard.TradeBinder = partnerBinder;
        partnerBinderCard.Card = partnerCard;
        mainContext.BinderCards.Add(partnerBinderCard);

        var partnerWishlist = _testDataBuilder.CreateWishlist(partner.Id, isPublic: true);
        mainContext.Wishlists.Add(partnerWishlist);
        var partnerWishlistCard = _testDataBuilder.CreateWishlistCard(partnerWishlist.Id, initiatorCard.OracleId, initiatorCard.Name);
        partnerWishlistCard.WishlistId = partnerWishlist.Id;
        partnerWishlistCard.Wishlist = partnerWishlist;
        partnerWishlistCard.DesiredQuantity = 1;
        partnerWishlist.WishlistCards = new List<WishlistCard> { partnerWishlistCard };
        mainContext.WishlistCards.Add(partnerWishlistCard);

        var initiatorWishlist = _testDataBuilder.CreateWishlist(initiator.Id, isPublic: true);
        mainContext.Wishlists.Add(initiatorWishlist);
        var initiatorWishlistCard = _testDataBuilder.CreateWishlistCard(initiatorWishlist.Id, partnerCard.OracleId, partnerCard.Name);
        initiatorWishlistCard.WishlistId = initiatorWishlist.Id;
        initiatorWishlistCard.Wishlist = initiatorWishlist;
        initiatorWishlistCard.DesiredQuantity = 1;
        initiatorWishlist.WishlistCards = new List<WishlistCard> { initiatorWishlistCard };
        mainContext.WishlistCards.Add(initiatorWishlistCard);

        await mainContext.SaveChangesAsync();

        SeedCardMarketData([initiatorBinderCard, partnerBinderCard]);

        var connection = new TradeConnectionDto
        {
            TradeId = "trade-commit",
            Initiator = new TradeParticipantDto { UserId = initiator.Id, DisplayName = initiator.DisplayName, Email = initiator.Email ?? string.Empty },
            Partner = new TradeParticipantDto { UserId = partner.Id, DisplayName = partner.DisplayName, Email = partner.Email ?? string.Empty },
            InitiatorCollectionId = initiatorCollection.Id,
            PartnerCollectionId = partnerCollection.Id,
            InitiatorMatches = new List<TradeMatchDto>
            {
                new TradeMatchDto
                {
                    MatchId = "init-match",
                    CardName = initiatorCard.Name,
                    FromUserId = initiator.Id,
                    ToUserId = partner.Id,
                    IsSelected = true,
                    OfferingCard = new BinderCardDto
                    {
                        Id = initiatorBinderCard.Id,
                        TradeBinderId = initiatorBinder.Id,
                        CardId = initiatorCard.Id,
                        Card = initiatorCard,
                        Name = initiatorCard.Name,
                        QuantityToTrade = 1,
                        MaxQuantityToTrade = 1,
                        MarketPrice = 9.25,
                        Currency = Currency.Eur
                    }
                }
            },
            PartnerMatches = new List<TradeMatchDto>
            {
                new TradeMatchDto
                {
                    MatchId = "partner-match",
                    CardName = partnerCard.Name,
                    FromUserId = partner.Id,
                    ToUserId = initiator.Id,
                    IsSelected = true,
                    OfferingCard = new BinderCardDto
                    {
                        Id = partnerBinderCard.Id,
                        TradeBinderId = partnerBinder.Id,
                        CardId = partnerCard.Id,
                        Card = partnerCard,
                        Name = partnerCard.Name,
                        QuantityToTrade = 1,
                        MaxQuantityToTrade = 1,
                        MarketPrice = 7.75,
                        Currency = Currency.Usd
                    }
                }
            }
        };

        _sessionStoreMock.Setup(s => s.GetAsync("trade-commit", It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);
        _sessionStoreMock.Setup(s => s.DeleteAsync("trade-commit", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var service = CreateService(userManager.Object, unitOfWork);

        // Act
        await service.CommitTradeAsync("trade-commit", initiator.Id, CancellationToken.None);

        // Assert
        var updatedInitiatorCard = await mainContext.Cards.FindAsync(initiatorCard.Id);
        updatedInitiatorCard!.Quantity.Should().Be(1);

        var recipientCards = mainContext.Cards.Where(c => c.CollectionId == partnerCollection.Id && c.Name == initiatorCard.Name).ToList();
        recipientCards.Should().ContainSingle(c => c.Quantity == 1);
        var partnerReceivedCard = recipientCards.Single();
        partnerReceivedCard.PurchasePrice.Should().Be(9.25);
        partnerReceivedCard.PurchasePriceCurrency.Should().Be(Currency.Eur);

        var initiatorReceivedCards = mainContext.Cards.Where(c => c.CollectionId == initiatorCollection.Id && c.Name == partnerCard.Name).ToList();
        initiatorReceivedCards.Should().ContainSingle(c => c.Quantity == 1);
        var initiatorReceivedCard = initiatorReceivedCards.Single();
        initiatorReceivedCard.PurchasePrice.Should().Be(7.75);
        initiatorReceivedCard.PurchasePriceCurrency.Should().Be(Currency.Usd);

        var partnerWishlistCards = mainContext.WishlistCards.Where(c => c.WishlistId == partnerWishlist.Id).ToList();
        partnerWishlistCards.Should().BeEmpty();

        var initiatorWishlistCards = mainContext.WishlistCards.Where(c => c.WishlistId == initiatorWishlist.Id).ToList();
        initiatorWishlistCards.Should().BeEmpty();

        (await mainContext.BinderCards.FindAsync(initiatorBinderCard.Id)).Should().BeNull();
        (await mainContext.BinderCards.FindAsync(partnerBinderCard.Id)).Should().BeNull();

        _sessionStoreMock.Verify();
    }

    [Fact]
    public async Task CommitTradeAsync_WhenRecipientCollectionMissing_Throws()
    {
        // Arrange
        var initiator = CreateUser("initiator");
        var partner = CreateUser("partner");
        var userManager = CreateUserManagerMock(initiator, partner);

        
        
        
        var connection = new TradeConnectionDto
        {
            TradeId = "trade-commit",
            Initiator = new TradeParticipantDto { UserId = initiator.Id },
            Partner = new TradeParticipantDto { UserId = partner.Id },
            PartnerCollectionId = 99,
            InitiatorCollectionId = null,
            InitiatorMatches = new List<TradeMatchDto>
            {
                new TradeMatchDto
                {
                    MatchId = "init-match",
                    CardName = "Lightning Bolt",
                    FromUserId = initiator.Id,
                    ToUserId = partner.Id,
                    IsSelected = true,
                    OfferingCard = new BinderCardDto
                    {
                        Id = 1,
                        TradeBinderId = 1,
                        CardId = 1,
                        Name = "Lightning Bolt",
                        QuantityToTrade = 1,
                        MaxQuantityToTrade = 1,
                        Card = null!
                    }
                }
            },
            PartnerMatches = new List<TradeMatchDto>
            {
                new TradeMatchDto
                {
                    MatchId = "partner-match",
                    CardName = "Counterspell",
                    FromUserId = partner.Id,
                    ToUserId = initiator.Id,
                    IsSelected = true,
                    OfferingCard = new BinderCardDto
                    {
                        Id = 2,
                        TradeBinderId = 2,
                        CardId = 2,
                        Name = "Counterspell",
                        QuantityToTrade = 1,
                        MaxQuantityToTrade = 1,
                        Card = null!
                    }
                }
            }
        };

        _sessionStoreMock.Setup(s => s.GetAsync("trade-commit", It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);

        var service = CreateService(userManager.Object);

        // Act
        var act = () => service.CommitTradeAsync("trade-commit", initiator.Id, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private TradeConnectionService CreateService(UserManager<AppUser> userManager, IUnitOfWork? unitOfWorkOverride = null)
    {
        return new TradeConnectionService(
            userManager,
            unitOfWorkOverride ?? _unitOfWorkMock.Object,
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

    private string GetOracleIdForName(string cardName)
    {
        if (_oracleIdsByName.TryGetValue(cardName, out var oracleId))
        {
            return oracleId;
        }

        oracleId = Guid.NewGuid().ToString();
        _oracleIdsByName[cardName] = oracleId;
        return oracleId;
    }

    private Wishlist CreateWishlistWithCard(string ownerId, string cardName)
    {
        var wishlist = _testDataBuilder.CreateWishlist(ownerId, isPublic: true);
        wishlist.Id = NextId();
        var oracleId = GetOracleIdForName(cardName);
        var card = _testDataBuilder.CreateWishlistCard(wishlist.Id, oracleId, cardName);
        card.Id = NextId();
        wishlist.WishlistCards = new List<WishlistCard> { card };
        return wishlist;
    }

    private BinderCard CreateBinderCard(string ownerId, string cardName, int quantityToTrade = 1, int? collectionQuantity = null)
    {
        var binder = _testDataBuilder.CreateTradeBinder(ownerId, isPublic: true);
        binder.Id = NextId();

        var collection = _testDataBuilder.CreateCollection(ownerId);
        collection.Id = NextId();

        var ownedCard = _testDataBuilder.CreateCard(collection.Id, cardName, price: 1);
        ownedCard.Id = NextId();
        ownedCard.IsFoil = false;
        ownedCard.ScryfallId = ownedCard.OracleId = GetOracleIdForName(cardName);
        if (collectionQuantity.HasValue)
        {
            ownedCard.Quantity = collectionQuantity.Value;
        }

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
                oracleId: card.Card!.OracleId,
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
        var manager = new Mock<UserManager<AppUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
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

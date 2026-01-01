using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

namespace UnitTests.Services;

public class TradeConnectionServiceTests : IDisposable
{
    private readonly Mock<IUnitOfWork> unitOfWorkMock = new();
    private readonly Mock<IGenericRepository<Wishlist>> wishlistRepositoryMock = new();
    private readonly Mock<IGenericRepository<BinderCard>> binderCardRepositoryMock = new();
    private readonly Mock<ITradeSessionStore> sessionStoreMock = new();
    private readonly AppIdentityDbContext identityDbContext;
    private readonly NotificationService notificationService;
    private readonly TestDataBuilder testDataBuilder = new();
    private int idSequence = 1;

    public TradeConnectionServiceTests()
    {
        identityDbContext = new AppIdentityDbContext(new DbContextOptionsBuilder<AppIdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        notificationService = new NotificationService(identityDbContext, NullLogger<NotificationService>.Instance);
        unitOfWorkMock.Setup(x => x.Repository<Wishlist>()).Returns(wishlistRepositoryMock.Object);
        unitOfWorkMock.Setup(x => x.Repository<BinderCard>()).Returns(binderCardRepositoryMock.Object);
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
            CreateBinderCard(initiator.Id, "Trade Match")
        };

        var partnerBinderCards = new List<BinderCard>
        {
            CreateBinderCard(partner.Id, "Other Card")
        };

        SetupWishlistRepository(initiatorWishlists.Concat(partnerWishlists).ToList());
        SetupBinderCardRepository(initiatorBinderCards.Concat(partnerBinderCards).ToList());

        var userManager = CreateUserManagerMock(initiator, partner);
        sessionStoreMock.Setup(s => s.StoreAsync(It.IsAny<TradeConnectionDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var service = CreateService(userManager.Object);

        // Act
        var result = await service.PrepareConnectionAsync(initiator.Id, partner.Id, CancellationToken.None);

        // Assert
        result.TradeId.Should().NotBeNullOrWhiteSpace();
        result.InitiatorMatches.Should().ContainSingle(m => m.CardName == "Trade Match");
        result.PartnerMatches.Should().BeEmpty();
        identityDbContext.Notifications.Should().ContainSingle(n => n.AppUserId == partner.Id);
        sessionStoreMock.Verify();
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

        sessionStoreMock.Setup(s => s.GetAsync("trade-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);

        // Act
        Func<Task> act = () => service.GetConnectionAsync("trade-123", "intruder", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    private TradeConnectionService CreateService(UserManager<AppUser> userManager)
    {
        return new TradeConnectionService(
            userManager,
            unitOfWorkMock.Object,
            sessionStoreMock.Object,
            notificationService);
    }

    private AppUser CreateUser(string key)
    {
        var user = testDataBuilder.CreateUser($"{key}@test.com", key);
        user.Id = key;
        user.DisplayName = key;
        user.FirstName = "First";
        user.LastName = "Last";
        return user;
    }

    private Wishlist CreateWishlistWithCard(string ownerId, string cardName)
    {
        var wishlist = testDataBuilder.CreateWishlist(ownerId, isPublic: true);
        wishlist.Id = NextId();
        var card = testDataBuilder.CreateWishlistCard(wishlist.Id, Guid.NewGuid().ToString(), cardName);
        card.Id = NextId();
        wishlist.WishlistCards = new List<WishlistCard> { card };
        return wishlist;
    }

    private BinderCard CreateBinderCard(string ownerId, string cardName)
    {
        var binder = testDataBuilder.CreateTradeBinder(ownerId, isPublic: true);
        binder.Id = NextId();

        var collection = testDataBuilder.CreateCollection(ownerId);
        collection.Id = NextId();

        var ownedCard = testDataBuilder.CreateCard(collection.Id, cardName, price: 1);
        ownedCard.Id = NextId();

        var binderCard = testDataBuilder.CreateBinderCard(binder.Id, ownedCard.Id, cardName, quantityToTrade: 1);
        binderCard.Id = NextId();
        binderCard.Card = ownedCard;
        binderCard.TradeBinder = binder;
        return binderCard;
    }

    private int NextId() => idSequence++;

    private void SetupWishlistRepository(IReadOnlyList<Wishlist> wishlists)
    {
        wishlistRepositoryMock
            .Setup(r => r.ListAsync(It.IsAny<ISpecification<Wishlist>>(), It.IsAny<bool>()))
            .ReturnsAsync((ISpecification<Wishlist> spec, bool _) =>
            {
                var predicate = spec.Criteria?.Compile() ?? (_ => true);
                return wishlists.Where(predicate).ToList();
            });
    }

    private void SetupBinderCardRepository(IReadOnlyList<BinderCard> cards)
    {
        binderCardRepositoryMock
            .Setup(r => r.ListAsync(It.IsAny<ISpecification<BinderCard>>(), It.IsAny<bool>()))
            .ReturnsAsync((ISpecification<BinderCard> spec, bool _) =>
            {
                var predicate = spec.Criteria?.Compile() ?? (_ => true);
                return cards.Where(predicate).ToList();
            });
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
        identityDbContext.Dispose();
    }
}

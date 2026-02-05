using API.Constants;
using API.Dtos.Notifications;
using API.Services;
using Core.Models.Identity;
using FluentAssertions;
using Core.Enums;
using Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace UnitTests.Services;

public class NotificationServiceTests
{
    [Fact]
    public async Task CreateNotificationAsync_PersistsNotification()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var dto = new NewNotificationDto
        {
            Name = "request_join_league",
            Message = "Notifications.RequestJoinLeague",
            MessageKey = "Notifications.RequestJoinLeague",
            MessageArgs = ["First", "Last", "League"],
            Origin = "League",
            ObjectId = "42",
            AppUserId = "user-1"
        };

        var result = await service.CreateNotificationAsync(dto);

        result.Id.Should().BeGreaterThan(0);
        result.Name.Should().Be(dto.Name);
        result.Message.Should().Be(dto.Message);
        result.ObjectId.Should().Be(dto.ObjectId);
        result.Origin.Should().Be(dto.Origin);
        result.AppUserId.Should().Be(dto.AppUserId);
        result.CreationDateTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        var stored = await context.Notifications.SingleAsync();
        stored.Name.Should().Be(dto.Name);
        stored.Message.Should().Be(dto.Message);
        stored.MessageKey.Should().Be(dto.MessageKey);
        stored.MessageArgsJson.Should().NotBeNull();
        stored.CreationDateTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetNotificationAsync_WhenOwnedByUser_ReturnsNotificationDto()
    {
        await using var context = CreateContext();
        var existing = new Notification
        {
            Name = "match_start",
            Message = "Notifications.MatchStart",
            MessageKey = "Notifications.MatchStart",
            MessageArgsJson = "[\"Round 1\"]",
            Origin = "League",
            AppUserId = "user-2",
            CreationDateTime = DateTime.UtcNow.AddMinutes(-10),
            AppUser = null!
        };
        context.Notifications.Add(existing);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var service = CreateService(context);

        var result = await service.GetNotificationAsync(existing.Id, existing.AppUserId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(existing.Id);
        result.Name.Should().Be(existing.Name);
        result.Message.Should().Be("localized");
        result.Origin.Should().Be(existing.Origin);
    }

    [Fact]
    public async Task GetNotificationAsync_WithMismatchedUser_ReturnsNull()
    {
        await using var context = CreateContext();
        var existing = new Notification
        {
            Name = "match_start",
            Message = "Round one is starting",
            Origin = "League",
            AppUserId = "user-2",
            CreationDateTime = DateTime.UtcNow,
            AppUser = null!
        };
        context.Notifications.Add(existing);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var service = CreateService(context);

        var result = await service.GetNotificationAsync(existing.Id, "another-user");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserNotificationsAsync_ReturnsNotificationsOrderedByNewest()
    {
        await using var context = CreateContext();
        const string userId = "user-3";
        context.Notifications.AddRange(
            new Notification
            {
                Name = "older",
                Message = "Notifications.Older",
                MessageKey = "Notifications.Older",
                MessageArgsJson = "[]",
                Origin = "League",
                AppUserId = userId,
                CreationDateTime = DateTime.UtcNow.AddDays(-1),
                AppUser = null!
            },
            new Notification
            {
                Name = "newer",
                Message = "Notifications.Newer",
                MessageKey = "Notifications.Newer",
                MessageArgsJson = "[]",
                Origin = "League",
                AppUserId = userId,
                CreationDateTime = DateTime.UtcNow,
                AppUser = null!
            });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var service = CreateService(context);

        var result = await service.GetUserNotificationsAsync(userId);

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("newer");
        result[1].Name.Should().Be("older");
    }

    [Fact]
    public async Task DeleteNotificationAsync_WhenNotificationExists_RemovesNotification()
    {
        await using var context = CreateContext();
        var notification = new Notification
        {
            Name = "match_start",
            Message = "Round one is starting",
            Origin = "League",
            AppUserId = "user-2",
            CreationDateTime = DateTime.UtcNow,
            AppUser = null!
        };
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var service = CreateService(context);

        var result = await service.DeleteNotificationAsync(notification.Id);

        result.Should().BeTrue();
        (await context.Notifications.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DeleteNotificationAsync_WhenNotificationDoesNotExist_ReturnsFalse()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var result = await service.DeleteNotificationAsync(999);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateNotificationAsync_SetsReadStateAndApproval()
    {
        await using var context = CreateContext();
        var notifications = new List<Notification>
        {
            new()
            {
                Name = "first",
                Message = "First message",
                Origin = "League",
                AppUserId = "user-1",
                CreationDateTime = DateTime.UtcNow.AddMinutes(-2),
                AppUser = null!
            },
            new()
            {
                Name = "second",
                Message = "Second message",
                Origin = "League",
                AppUserId = "user-1",
                CreationDateTime = DateTime.UtcNow.AddMinutes(-1),
                AppUser = null!
            },
            new()
            {
                Name = "untouched",
                Message = "Other",
                Origin = "League",
                AppUserId = "user-2",
                CreationDateTime = DateTime.UtcNow,
                AppUser = null!
            }
        };
        context.Notifications.AddRange(notifications);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var idsToUpdate = notifications.Take(2).Select(n => n.Id).ToList();
        var service = CreateService(context);

        var result = await service.UpdateNotificationAsync(idsToUpdate, isRead: true, approval: false);

        result.Should().BeTrue();
        var updated = await context.Notifications.Where(n => idsToUpdate.Contains(n.Id)).ToListAsync();
        updated.Should().OnlyContain(n => n.IsRead && n.Approval == false);
        var untouched = await context.Notifications.SingleAsync(n => n.Name == "untouched");
        untouched.IsRead.Should().BeFalse();
        untouched.Approval.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateNotificationAsync_WhenApproving_UpdatesApprovalField()
    {
        await using var context = CreateContext();
        var notification = new Notification
        {
            Name = "test_notification",
            Message = "Test message",
            Origin = "test.origin",
            AppUserId = "user-1",
            Approval = false,
            CreationDateTime = DateTime.UtcNow,
            AppUser = null!
        };
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var service = CreateService(context);

        var result = await service.UpdateNotificationAsync([notification.Id], isRead: null, approval: true, userToUpdate: "user-1");

        result.Should().BeTrue();
        var updated = await context.Notifications.SingleAsync(n => n.Id == notification.Id);
        updated.Approval.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateNotificationAsync_WhenApproving_DoesNotCreateUnexpectedNotifications()
    {
        await using var context = CreateContext();
        var notifications = new List<Notification>
        {
            new()
            {
                Name = NotificationConstants.FriendRequest,
                Message = "Friend request",
                Origin = "user1.user2",
                AppUserId = "user2",
                Approval = false,
                CreationDateTime = DateTime.UtcNow,
                AppUser = null!
            },
            new()
            {
                Name = NotificationConstants.TradeCommitRequest,
                Message = "Trade commit request",
                Origin = "trade.origin",
                AppUserId = "user3",
                Approval = false,
                CreationDateTime = DateTime.UtcNow,
                AppUser = null!
            },
            new()
            {
                Name = "generic_notification",
                Message = "Generic notification",
                Origin = "generic.origin",
                AppUserId = "user4",
                Approval = false,
                CreationDateTime = DateTime.UtcNow,
                AppUser = null!
            }
        };
        context.Notifications.AddRange(notifications);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var service = CreateService(context);
        var initialCount = await context.Notifications.CountAsync();

        var result = await service.UpdateNotificationAsync(
            notifications.Select(n => n.Id).ToList(),
            isRead: null,
            approval: true,
            userToUpdate: "approver");

        result.Should().BeTrue();
        var finalCount = await context.Notifications.CountAsync();
        finalCount.Should().Be(initialCount, "because approving non-league notifications should not create additional notifications");
        
        var updated = await context.Notifications.Where(n => notifications.Select(nt => nt.Id).Contains(n.Id)).ToListAsync();
        updated.Should().OnlyContain(n => n.Approval == true);
    }

    [Fact]
    public async Task UpdateNotificationAsync_WhenApprovingRequestJoinLeague_CallsLeagueService()
    {
        await using var context = CreateContext();
        const string leagueId = "123";
        const string userId = "user-requester";
        var notification = new Notification
        {
            Name = NotificationConstants.RequestJoinLeague,
            Message = "Notifications.RequestJoinLeague",
            MessageKey = "Notifications.RequestJoinLeague",
            Origin = $"{leagueId}.{userId}",
            ObjectId = leagueId,
            AppUserId = "user-admin",
            Approval = false,
            CreationDateTime = DateTime.UtcNow,
            AppUser = null!
        };
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        
        // Mock ILeagueService to verify it's called
        var leagueServiceMock = new Mock<ILeagueService>();
        leagueServiceMock
            .Setup(s => s.JoinLeagueAsync(123, userId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        
        // Mock IServiceScopeFactory to provide the mocked league service
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(sp => sp.GetRequiredService<ILeagueService>())
            .Returns(leagueServiceMock.Object);
        var serviceScopeMock = new Mock<IServiceScope>();
        serviceScopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);
        
        var serviceScopeFactoryMock = new Mock<IServiceScopeFactory>();
        serviceScopeFactoryMock
            .Setup(f => f.CreateAsyncScope())
            .Returns(new AsyncServiceScope(serviceScopeMock.Object));
        
        var service = CreateService(context, serviceScopeFactoryMock.Object);
        var initialCount = await context.Notifications.CountAsync();

        var result = await service.UpdateNotificationAsync([notification.Id], isRead: null, approval: true, userToUpdate: "user-admin");

        result.Should().BeTrue();
        var finalCount = await context.Notifications.CountAsync();
        // No additional notification should be created - user is added directly to league
        finalCount.Should().Be(initialCount, "because approving request_join_league should not create additional notification, user is added directly to league");
        
        var updated = await context.Notifications.SingleAsync(n => n.Id == notification.Id);
        updated.Approval.Should().BeTrue();
        
        // Verify that JoinLeagueAsync was called with correct parameters
        leagueServiceMock.Verify(
            s => s.JoinLeagueAsync(123, userId, It.IsAny<CancellationToken>()),
            Times.Once,
            "JoinLeagueAsync should be called once when approving a league join request");
    }

    private static AppIdentityDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppIdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new AppIdentityDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static NotificationService CreateService(AppIdentityDbContext context, IServiceScopeFactory? serviceScopeFactory = null)
    {
        var settingsService = new Mock<IUserSettingsService>();
        settingsService.Setup(s => s.GetSettingsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string userId, CancellationToken _) => new Settings
            {
                AppUserId = userId,
                AppUser = null!,
                LanguageUi = Language.It
            });

        var localizer = new Mock<IMessageLocalizer>();
        localizer.Setup(l => l.GetMessageForLanguage(It.IsAny<Language?>(), It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns("localized");

        return new NotificationService(context, NullLogger<NotificationService>.Instance, settingsService.Object, localizer.Object, serviceScopeFactory);
    }
}

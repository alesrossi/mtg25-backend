using System.Net;
using System.Text.Json;
using API.Constants;
using API.Dtos.Notifications;
using Core.Enums;
using Core.Models.Identity;
using FluentAssertions;
using Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TestUtilities.Authentication;
using TestUtilities.Builders;
using TestUtilities.Serialization;

namespace IntegrationTests.Controllers;

[Collection("Integration Tests")]
public class NotificationsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly TestDataBuilder _testDataBuilder;

    public NotificationsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _testDataBuilder = new TestDataBuilder();
    }

    [Fact]
    public async Task GetNotificationFromId_WithExistingNotification_ReturnsNotification()
    {
        var user = await CreateTestUserAsync("notifications-get@test.com", "notifications_get");
        var notification = await CreateNotificationAsync(user.Id, name: "League Update");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var response = await client.GetAsync($"/api/notifications/{notification.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<NotificationDto>(payload, JsonContentHelper.DefaultOptions);
        dto.Should().NotBeNull();
        dto.Id.Should().Be(notification.Id);
    }

    [Fact]
    public async Task GetNotificationFromId_WithoutAuthentication_ReturnsUnauthorized()
    {
        var user = await CreateTestUserAsync("notifications-get-unauth@test.com", "notifications_get_unauth");
        var notification = await CreateNotificationAsync(user.Id);
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/notifications/{notification.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetNotifications_ForCurrentUser_ReturnsNotifications()
    {
        var user = await CreateTestUserAsync("notifications-list@test.com", "notifications_list");
        await CreateNotificationAsync(user.Id);
        await CreateNotificationAsync(user.Id);
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var response = await client.GetAsync("/api/notifications");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<List<NotificationDto>>(payload, JsonContentHelper.DefaultOptions);
        dto.Should().NotBeNull();
        dto.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetNotifications_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/notifications");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteNotification_WithExistingNotification_ReturnsSuccess()
    {
        var user = await CreateTestUserAsync("notifications-delete@test.com", "notifications_delete");
        var notification = await CreateNotificationAsync(user.Id);
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var response = await client.DeleteAsync($"/api/notifications/{notification.Id}");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteNotification_WithoutAuthentication_ReturnsUnauthorized()
    {
        var user = await CreateTestUserAsync("notifications-delete-unauth@test.com", "notifications_delete_unauth");
        var notification = await CreateNotificationAsync(user.Id);
        using var client = _factory.CreateClient();

        var response = await client.DeleteAsync($"/api/notifications/{notification.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
    
    [Fact]
    public async Task UpdateNotificationApproval_WithExistingNotification_ReturnsSuccess()
    {
        var user = await CreateTestUserAsync("notifications-update@test.com", "notifications_update");
        var notification = await CreateNotificationAsync(user.Id, isRead: false);
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);
        int[] arr = [notification.Id];
        
        var response = await client.PutAsync($"/api/notifications/read", JsonContentHelper.CreateContent(arr));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await VerifyNotificationHasBeenRead(notification.Id);
    }

    [Fact]
    public async Task UpdateNotificationStatus_WithExistingNotification_ReturnsSuccess()
    {
        var user = await CreateTestUserAsync("notifications-update@test.com", "notifications_update");
        var notification = await CreateNotificationAsync(user.Id, isRead: false);
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);
        int[] arr = [notification.Id];
        
        var response = await client.PutAsync($"/api/notifications/read", JsonContentHelper.CreateContent(arr));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateNotificationStatus_WithoutAuthentication_ReturnsUnauthorized()
    {
        var user = await CreateTestUserAsync("notifications-update-unauth@test.com", "notifications_update_unauth");
        await CreateNotificationAsync(user.Id);
        using var client = _factory.CreateClient();

        var response = await client.PutAsync($"/api/notifications/read", JsonContentHelper.CreateContent(new { }));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ApproveNotification_RespectsContract()
    {
        var approver = await CreateTestUserAsync("approver@test.com", "approver");
        var requester = await CreateTestUserAsync("requester@test.com", "requester");
        
        // Create a real league for the join request
        var league = await CreateTestLeagueAsync("Test League", approver.Id);
        
        var friendRequestNotification = await CreateNotificationAsync(
            approver.Id,
            name: NotificationConstants.FriendRequest,
            origin: $"{requester.Id}.{approver.Id}");
        
        var tradeNotification = await CreateNotificationAsync(
            approver.Id,
            name: NotificationConstants.TradeCommitRequest,
            origin: $"trade.{requester.Id}");
        
        var genericNotification = await CreateNotificationAsync(
            approver.Id,
            name: "generic_notification",
            origin: "generic.origin");
        
        var leagueNotification = await CreateNotificationAsync(
            approver.Id,
            name: NotificationConstants.RequestJoinLeague,
            origin: $"{league.Id}.{requester.Id}",
            objectId: league.Id.ToString());
        
        using var client = _factory.CreateClientWithUser(approver.Id, approver.UserName!, approver.Email!);
        
        var initialCount = await GetNotificationCountAsync();
        
        var notificationsToApprove = new[] 
        { 
            friendRequestNotification.Id, 
            tradeNotification.Id, 
            genericNotification.Id,
            leagueNotification.Id
        };
        
        var response = await client.PutAsync("/api/notifications/approve", JsonContentHelper.CreateContent(notificationsToApprove));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Verify all notifications are approved
        await VerifyNotificationApproved(friendRequestNotification.Id);
        await VerifyNotificationApproved(tradeNotification.Id);
        await VerifyNotificationApproved(genericNotification.Id);
        await VerifyNotificationApproved(leagueNotification.Id);
        
        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var dbContext = verificationScope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var membership = await dbContext.UserLeagues
            .FirstOrDefaultAsync(ul => ul.LeagueId == league.Id && ul.UserId == requester.Id);
        membership.Should().NotBeNull("because the requester should be added to the league after approval");

        var finalCount = await GetNotificationCountAsync();
        finalCount.Should().Be(initialCount + 2, "because JoinLeagueAsync creates player_joined_league for owner and joined_league for requester");

        var playerJoinedNotifications = await GetNotificationsByNameAsync("player_joined_league");
        playerJoinedNotifications.Should().HaveCount(1, "because JoinLeagueAsync should create a notification for the league owner");
        playerJoinedNotifications[0].AppUserId.Should().Be(approver.Id, "because the notification should be sent to the league owner");

        var joinedLeagueNotifications = await GetNotificationsByNameAsync(NotificationConstants.JoinedLeague);
        joinedLeagueNotifications.Should().HaveCount(1, "because JoinLeagueAsync should notify the requester that they were added to the league");
        joinedLeagueNotifications[0].AppUserId.Should().Be(requester.Id, "because the notification should be sent to the requester");
    }

    private Task<AppUser> CreateTestUserAsync(string email, string userName) =>
        TestUserFactory.CreateAsync(_factory.Services, _testDataBuilder, email, userName, requirePassword: true);

    private async Task<Notification> CreateNotificationAsync(
        string userId,
        bool isRead = false,
        string? name = null,
        string? message = null,
        string? origin = null,
        string? objectId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var user = await context.Users.FirstAsync(u => u.Id == userId);

        var notification = new Notification
        {
            Name = name ?? $"Notification {Guid.NewGuid():N}"[..16],
            Message = message ?? "Test notification body for integration tests",
            IsRead = isRead,
            Origin = origin ?? "IntegrationTests",
            ObjectId = objectId,
            CreationDateTime = DateTime.UtcNow,
            AppUserId = userId,
            AppUser = user
        };

        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        return notification;
    }
    
    private Task VerifyNotificationHasBeenRead(int id)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        
        var notification = dbContext.Notifications.FirstOrDefault(n => n.Id == id);
        notification.Should().NotBeNull($"because notification {id} should be updated");
        notification.IsRead.Should().Be(true, $"because notification {id} should be set to read");
        return Task.CompletedTask;
    }
    
    private async Task VerifyNotificationApproved(int id)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        
        var notification = await dbContext.Notifications.FirstOrDefaultAsync(n => n.Id == id);
        notification.Should().NotBeNull($"because notification {id} should exist");
        notification.Approval.Should().BeTrue($"because notification {id} should be approved");
    }
    
    private async Task<int> GetNotificationCountAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        return await dbContext.Notifications.CountAsync();
    }
    
    private async Task<List<Notification>> GetNotificationsByNameAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        return await dbContext.Notifications
            .Where(n => n.Name == name)
            .ToListAsync();
    }
    
    private async Task<League> CreateTestLeagueAsync(string name, string ownerId, bool isActive = true, bool isPublic = true)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var league = new League
        {
            Name = name,
            OwnerId = ownerId,
            Code = $"TL{uniqueId}"[..8],
            Format = DeckFormat.Standard,
            TotalRounds = 5,
            RoundsToConsider = 4,
            MinimumRounds = 2,
            TotalPlayers = 0,
            PointsToGive = [3, 1, 0],
            PointsPerWin = 3,
            PointsPerDraw = 1,
            PointsPerLoss = 0,
            ScoringSystem = ScoringSystem.Positional,
            IsActive = isActive,
            IsPublic = isPublic
        };
        
        dbContext.Leagues.Add(league);
        await dbContext.SaveChangesAsync();

        var rounds = Enumerable.Range(1, league.TotalRounds)
            .Select(order => new Round
            {
                LeagueId = league.Id,
                League = league,
                Order = order
            })
            .ToList();

        dbContext.Rounds.AddRange(rounds);
        await dbContext.SaveChangesAsync();

        league.CurrentRound = rounds[0].Id;
        dbContext.Leagues.Update(league);
        await dbContext.SaveChangesAsync();

        dbContext.LeagueRoleAssignments.Add(new LeagueRoleAssignment
        {
            LeagueId = league.Id,
            UserId = ownerId,
            Roles = LeagueRole.Admin
        });

        dbContext.UserLeagues.Add(new AppUserLeague
        {
            UserId = ownerId,
            LeagueId = league.Id,
            Score = 0,
            RoundsPlayed = 0,
            Rounds = [],
            BestRound = 0,
            AvgPosition = 0,
            IsPlaying = false
        });

        await dbContext.SaveChangesAsync();
        
        return league;
    }
}

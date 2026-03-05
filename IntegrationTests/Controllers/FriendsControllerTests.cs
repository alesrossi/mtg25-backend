using System.Net;
using System.Net.Http.Json;
using API.Dtos.Friends;
using Core.Enums;
using Core.Models.Identity;
using Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TestUtilities.Authentication;
using TestUtilities.Builders;
using TestUtilities.Serialization;

namespace IntegrationTests.Controllers;

[Collection("Integration Tests")]
public class FriendsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly TestDataBuilder _testDataBuilder = new();

    public FriendsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SendFriendRequest_CreatesPendingFriendshipAndNotification()
    {
        var requester = await CreateTestUserAsync("requester@test.com", "requester");
        var target = await CreateTestUserAsync("target@test.com", "target");

        using var client = _factory.CreateClientWithUser(requester.Id, requester.UserName!, requester.Email!);
        var response = await client.PostAsync($"/api/friends/{target.Id}/request", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var scope = _factory.Services.CreateAsyncScope();
        var identityContext = scope.ServiceProvider.GetRequiredService<MainContext>();

        var friendship = await identityContext.AppUserFriends.FirstOrDefaultAsync(
            f => f.UserId == requester.Id && f.FriendId == target.Id);
        friendship.Should().NotBeNull();
        friendship.Status.Should().Be(FriendshipStatus.Pending);

        var notification = await identityContext.Notifications.FirstOrDefaultAsync(
            n => n.AppUserId == target.Id && n.Origin == $"{requester.Id}.{target.Id}");
        notification.Should().NotBeNull();
    }

    [Fact]
    public async Task SendFriendRequest_WhenDuplicate_ReturnsBadRequest()
    {
        var requester = await CreateTestUserAsync("requester@test.com", "requester");
        var target = await CreateTestUserAsync("target@test.com", "target");
        await CreateFriendshipAsync(requester.Id, target.Id);

        using var client = _factory.CreateClientWithUser(requester.Id, requester.UserName!, requester.Email!);
        var response = await client.PostAsync($"/api/friends/{target.Id}/request", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SendFriendRequestByEmail_CreatesPendingFriendshipAndNotification()
    {
        var requester = await CreateTestUserAsync("requester@test.com", "requester");
        var target = await CreateTestUserAsync("target@test.com", "target");

        using var client = _factory.CreateClientWithUser(requester.Id, requester.UserName!, requester.Email!);
        var request = new SendFriendRequestByEmailRequest { Email = target.Email! };
        var response = await client.PostAsync("/api/friends/request-by-email", JsonContentHelper.CreateContent(request));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var scope = _factory.Services.CreateAsyncScope();
        var identityContext = scope.ServiceProvider.GetRequiredService<MainContext>();

        var friendship = await identityContext.AppUserFriends.FirstOrDefaultAsync(
            f => f.UserId == requester.Id && f.FriendId == target.Id);
        friendship.Should().NotBeNull();
        friendship!.Status.Should().Be(FriendshipStatus.Pending);

        var notification = await identityContext.Notifications.FirstOrDefaultAsync(
            n => n.AppUserId == target.Id && n.Origin == $"{requester.Id}.{target.Id}");
        notification.Should().NotBeNull();
    }

    [Fact]
    public async Task SendFriendRequestByEmail_WhenUserNotFound_ReturnsNotFound()
    {
        var requester = await CreateTestUserAsync("requester@test.com", "requester");

        using var client = _factory.CreateClientWithUser(requester.Id, requester.UserName!, requester.Email!);
        var request = new SendFriendRequestByEmailRequest { Email = "nonexistent@test.com" };
        var response = await client.PostAsync("/api/friends/request-by-email", JsonContentHelper.CreateContent(request));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SendFriendRequestByEmail_WhenDuplicate_ReturnsBadRequest()
    {
        var requester = await CreateTestUserAsync("requester@test.com", "requester");
        var target = await CreateTestUserAsync("target@test.com", "target");
        await CreateFriendshipAsync(requester.Id, target.Id);

        using var client = _factory.CreateClientWithUser(requester.Id, requester.UserName!, requester.Email!);
        var request = new SendFriendRequestByEmailRequest { Email = target.Email! };
        var response = await client.PostAsync("/api/friends/request-by-email", JsonContentHelper.CreateContent(request));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SendFriendRequestByEmail_WhenSelfEmail_ReturnsBadRequest()
    {
        var requester = await CreateTestUserAsync("requester@test.com", "requester");

        using var client = _factory.CreateClientWithUser(requester.Id, requester.UserName!, requester.Email!);
        var request = new SendFriendRequestByEmailRequest { Email = requester.Email! };
        var response = await client.PostAsync("/api/friends/request-by-email", JsonContentHelper.CreateContent(request));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AcceptFriendRequest_AllowsRecipientToFinalize()
    {
        var requester = await CreateTestUserAsync("requester@test.com", "requester");
        var target = await CreateTestUserAsync("target@test.com", "target");
        await CreateFriendshipAsync(requester.Id, target.Id, status: FriendshipStatus.Pending);
        await ApproveFriendRequestAsync(requester.Id, target.Id, target.Id);

        using var client = _factory.CreateClientWithUser(target.Id, target.UserName!, target.Email!);
        var response = await client.PostAsync($"/api/friends/{requester.Id}/accept", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var scope = _factory.Services.CreateAsyncScope();
        var identityContext = scope.ServiceProvider.GetRequiredService<MainContext>();
        var friendship = await identityContext.AppUserFriends.FirstAsync();
        friendship.Status.Should().Be(FriendshipStatus.Accepted);
        friendship.RespondedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task AcceptFriendRequest_WhenRequesterCalls_ReturnsForbidden()
    {
        var requester = await CreateTestUserAsync("requester@test.com", "requester");
        var target = await CreateTestUserAsync("target@test.com", "target");
        await CreateFriendshipAsync(requester.Id, target.Id, status: FriendshipStatus.Pending);
        await ApproveFriendRequestAsync(requester.Id, target.Id, target.Id);

        using var client = _factory.CreateClientWithUser(requester.Id, requester.UserName!, requester.Email!);
        var response = await client.PostAsync($"/api/friends/{requester.Id}/accept", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AcceptFriendRequest_WhenNotApproved_ReturnsForbidden()
    {
        var requester = await CreateTestUserAsync("requester@test.com", "requester");
        var target = await CreateTestUserAsync("target@test.com", "target");
        await CreateFriendshipAsync(requester.Id, target.Id, status: FriendshipStatus.Pending);

        using var client = _factory.CreateClientWithUser(target.Id, target.UserName!, target.Email!);
        var response = await client.PostAsync($"/api/friends/{requester.Id}/accept", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
    

    [Fact]
    public async Task GetFriends_ReturnsAcceptedFriendships()
    {
        var requester = await CreateTestUserAsync("requester@test.com", "requester");
        var target = await CreateTestUserAsync("target@test.com", "target");
        await CreateFriendshipAsync(requester.Id, target.Id, status: FriendshipStatus.Accepted);

        using var client = _factory.CreateClientWithUser(requester.Id, requester.UserName!, requester.Email!);
        var response = await client.GetAsync("/api/friends");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var friends = await response.Content.ReadFromJsonAsync<List<FriendDto>>(JsonContentHelper.DefaultOptions);
        friends.Should().NotBeNull();
        friends.Should().ContainSingle();
        friends.Single().UserId.Should().Be(target.Id);
    }

    [Fact]
    public async Task DeleteFriendship_RemovesRelationship()
    {
        var requester = await CreateTestUserAsync("requester@test.com", "requester");
        var target = await CreateTestUserAsync("target@test.com", "target");
        await CreateFriendshipAsync(requester.Id, target.Id, status: FriendshipStatus.Accepted);

        using var client = _factory.CreateClientWithUser(requester.Id, requester.UserName!, requester.Email!);
        var response = await client.DeleteAsync($"/api/friends/{target.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var scope = _factory.Services.CreateAsyncScope();
        var identityContext = scope.ServiceProvider.GetRequiredService<MainContext>();
        var friendship = await identityContext.AppUserFriends.FirstOrDefaultAsync(
            f => (f.UserId == requester.Id && f.FriendId == target.Id)
                 || (f.UserId == target.Id && f.FriendId == requester.Id));
        friendship.Should().BeNull();
    }

    private async Task<AppUser> CreateTestUserAsync(string email, string userName)
    {
        return await TestUserFactory.CreateAsync(_factory.Services, _testDataBuilder, email, userName, requirePassword: true);
    }

    private async Task CreateFriendshipAsync(
        string requesterId,
        string friendId,
        FriendshipStatus status = FriendshipStatus.Pending)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var identityContext = scope.ServiceProvider.GetRequiredService<MainContext>();
        var requester = await identityContext.Users.FindAsync(requesterId)
            ?? throw new InvalidOperationException("Requester not found.");
        var friend = await identityContext.Users.FindAsync(friendId)
            ?? throw new InvalidOperationException("Friend not found.");

        var friendship = new AppUserFriend
        {
            UserId = requesterId,
            User = requester,
            FriendId = friendId,
            Friend = friend,
            RequestedById = requesterId,
            RequestedBy = requester,
            Status = status,
            RequestedAt = DateTime.UtcNow,
            RespondedAt = status == FriendshipStatus.Pending ? null : DateTime.UtcNow
        };

        identityContext.AppUserFriends.Add(friendship);
        await identityContext.SaveChangesAsync();
    }

    private async Task ApproveFriendRequestAsync(string requesterId, string friendId, string approverId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var identityContext = scope.ServiceProvider.GetRequiredService<MainContext>();

        var notification = new Notification
        {
            Name = "friend_request",
            Message = "Friend request",
            Origin = $"{requesterId}.{friendId}",
            ObjectId = $"{requesterId}:{friendId}",
            AppUserId = approverId,
            AppUser = null!,
            Approval = true,
            CreationDateTime = DateTime.UtcNow,
            IsRead = true
        };

        identityContext.Notifications.Add(notification);
        await identityContext.SaveChangesAsync();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.Constants;
using API.Dtos.Friends;
using API.Dtos.Notifications;
using Core.Enums;
using Core.Models.Identity;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace API.Services;

public interface IFriendService
{
    Task SendFriendRequestAsync(string requesterUserId, string targetUserId, CancellationToken cancellationToken = default);
    Task AcceptFriendRequestAsync(string requesterUserId, string recipientUserId, CancellationToken cancellationToken = default);
    Task DeleteFriendshipAsync(string userId, string friendUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FriendDto>> GetFriendsAsync(string userId, CancellationToken cancellationToken = default);
}

public sealed class FriendService : IFriendService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly AppIdentityDbContext _identityDbContext;
    private readonly NotificationService _notificationService;

    public FriendService(
        UserManager<AppUser> userManager,
        AppIdentityDbContext identityDbContext,
        NotificationService notificationService)
    {
        _userManager = userManager;
        _identityDbContext = identityDbContext;
        _notificationService = notificationService;
    }

    public async Task SendFriendRequestAsync(string requesterUserId, string targetUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requesterUserId) || string.IsNullOrWhiteSpace(targetUserId))
        {
            throw new ArgumentException("Both user identifiers are required.");
        }

        if (string.Equals(requesterUserId, targetUserId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Cannot send a friend request to yourself.");
        }

        var requester = await _userManager.FindByIdAsync(requesterUserId)
            ?? throw new KeyNotFoundException($"User '{requesterUserId}' was not found.");
        var target = await _userManager.FindByIdAsync(targetUserId)
            ?? throw new KeyNotFoundException($"User '{targetUserId}' was not found.");

        var existing = await FindFriendshipAsync(requesterUserId, targetUserId, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException("A friendship or pending request already exists between these users.");
        }

        var friendship = new AppUserFriend
        {
            UserId = requesterUserId,
            User = requester,
            FriendId = targetUserId,
            Friend = target,
            RequestedById = requesterUserId,
            RequestedBy = requester,
            Status = FriendshipStatus.Pending,
            RequestedAt = DateTime.UtcNow
        };

        _identityDbContext.AppUserFriends.Add(friendship);
        await _identityDbContext.SaveChangesAsync(cancellationToken);

        var notification = new NewNotificationDto
        {
            Name = NotificationConstants.FriendRequest,
            Message = $"{requester.DisplayName} sent you a friend request.",
            Origin = $"{requesterUserId}.{targetUserId}",
            ObjectId = $"{requesterUserId}:{targetUserId}",
            AppUserId = targetUserId
        };

        await _notificationService.CreateNotificationAsync(notification);
    }

    public async Task AcceptFriendRequestAsync(string requesterUserId, string recipientUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requesterUserId) || string.IsNullOrWhiteSpace(recipientUserId))
        {
            throw new ArgumentException("Both user identifiers are required.");
        }

        var friendship = await _identityDbContext.AppUserFriends
            .Include(f => f.User)
            .Include(f => f.Friend)
            .FirstOrDefaultAsync(
                f => f.UserId == requesterUserId && f.FriendId == recipientUserId,
                cancellationToken);

        if (friendship is null)
        {
            throw new KeyNotFoundException("Friend request not found.");
        }

        if (!string.Equals(friendship.FriendId, recipientUserId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Only the invited user may accept the request.");
        }

        if (friendship.Status != FriendshipStatus.Pending)
        {
            throw new InvalidOperationException("This friend request has already been processed.");
        }

        var approvalKey = $"{requesterUserId}:{recipientUserId}";
        var approved = await _notificationService.HasApprovedNotificationAsync(
            NotificationConstants.FriendRequest,
            approvalKey,
            cancellationToken);
        if (!approved)
        {
            throw new UnauthorizedAccessException("Friend request must be approved before accepting.");
        }

        friendship.Status = FriendshipStatus.Accepted;
        friendship.RespondedAt = DateTime.UtcNow;
        await _identityDbContext.SaveChangesAsync(cancellationToken);

        await _notificationService.DeleteNotificationsAsync(
            NotificationConstants.FriendRequest,
            approvalKey);
    }

    public async Task DeleteFriendshipAsync(string userId, string friendUserId, CancellationToken cancellationToken = default)
    {
        var friendship = await FindFriendshipAsync(userId, friendUserId, cancellationToken);
        if (friendship is null)
        {
            throw new KeyNotFoundException("Friendship not found.");
        }

        _identityDbContext.AppUserFriends.Remove(friendship);
        await _identityDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FriendDto>> GetFriendsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var friendships = await _identityDbContext.AppUserFriends
            .AsNoTracking()
            .Include(f => f.User)
            .Include(f => f.Friend)
            .Where(f => f.UserId == userId || f.FriendId == userId)
            .OrderByDescending(f => f.RequestedAt)
            .ToListAsync(cancellationToken);

        return friendships.Select(f =>
        {
            var isRequester = string.Equals(f.UserId, userId, StringComparison.Ordinal);
            var counterpart = isRequester ? f.Friend : f.User;
            return new FriendDto
            {
                UserId = counterpart.Id,
                DisplayName = counterpart.DisplayName,
                Email = counterpart.Email ?? string.Empty,
                Status = f.Status,
                IsRequester = isRequester,
                RequestedAt = f.RequestedAt,
                RespondedAt = f.RespondedAt
            };
        }).ToList();
    }

    private Task<AppUserFriend?> FindFriendshipAsync(string firstUserId, string secondUserId, CancellationToken cancellationToken)
    {
        return _identityDbContext.AppUserFriends
            .FirstOrDefaultAsync(
                f => (f.UserId == firstUserId && f.FriendId == secondUserId)
                     || (f.UserId == secondUserId && f.FriendId == firstUserId),
                cancellationToken);
    }
}

using System;
using Core.Enums;
using FluentAssertions;
using TestUtilities.Builders;

namespace UnitTests.Models;

public class AppUserFriendTests
{
    private readonly TestDataBuilder _testDataBuilder = new();

    [Fact]
    public void AppUserFriend_WhenCreated_HasDefaultPendingStatus()
    {
        var friendship = _testDataBuilder.CreateFriendship();

        friendship.Status.Should().Be(FriendshipStatus.Pending);
        friendship.RequestedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        friendship.RespondedAt.Should().BeNull();
        friendship.UserId.Should().NotBeNullOrEmpty();
        friendship.FriendId.Should().NotBeNullOrEmpty();
        friendship.RequestedById.Should().Be(friendship.RequestedBy.Id);
    }

    [Fact]
    public void AppUserFriend_WhenAccepted_UpdatesStatusAndTimestamp()
    {
        var friendship = _testDataBuilder.CreateFriendship();

        friendship.Status = FriendshipStatus.Accepted;
        friendship.RespondedAt = DateTime.UtcNow;

        friendship.Status.Should().Be(FriendshipStatus.Accepted);
        friendship.RespondedAt.Should().NotBeNull();
        friendship.RespondedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void AppUserFriend_WithCustomParticipants_PreservesReferences()
    {
        var requester = _testDataBuilder.CreateUser();
        var recipient = _testDataBuilder.CreateUser();
        var friendship = _testDataBuilder.CreateFriendship(requestedBy: requester, user: requester, friend: recipient);

        friendship.User.Should().BeSameAs(requester);
        friendship.Friend.Should().BeSameAs(recipient);
        friendship.RequestedBy.Should().BeSameAs(requester);
    }
}

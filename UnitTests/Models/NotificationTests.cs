using FluentAssertions;
using TestUtilities.Builders;

namespace UnitTests.Models;

public class NotificationTests
{
    private readonly TestDataBuilder _testDataBuilder;

    public NotificationTests()
    {
        _testDataBuilder = new TestDataBuilder();
    }

    [Fact]
    public void Notification_WhenCreated_HasValidInitialState()
    {
        var user = _testDataBuilder.CreateUser();
        var notification = _testDataBuilder.CreateNotification(user);

        notification.Id.Should().Be(0, "because EF assigns the identifier when persisting");
        notification.AppUserId.Should().Be(user.Id);
        notification.AppUser.Should().BeSameAs(user);
        notification.Name.Should().NotBeNullOrEmpty();
        notification.Message.Should().NotBeNullOrEmpty();
        notification.Origin.Should().NotBeNullOrEmpty();
        notification.IsRead.Should().BeFalse();
        notification.CreationDateTime.Should()
            .BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Notification_WhenMarkedAsRead_UpdatesFlag()
    {
        var notification = _testDataBuilder.CreateNotification(isRead: false);

        notification.IsRead.Should().BeFalse();
        notification.IsRead = true;
        notification.IsRead.Should().BeTrue();
    }

    [Fact]
    public void Notification_WithCustomContent_PreservesValues()
    {
        var user = _testDataBuilder.CreateUser();
        var timestamp = new DateTime(2024, 01, 01, 12, 00, 00, DateTimeKind.Utc);

        var notification = _testDataBuilder.CreateNotification(
            user,
            name: "League Standings Updated",
            message: "Round 5 is complete",
            isInstant: true,
            isRead: true,
            origin: "Leagues",
            creationDateTime: timestamp);

        notification.Name.Should().Be("League Standings Updated");
        notification.Message.Should().Be("Round 5 is complete");
        notification.IsRead.Should().BeTrue();
        notification.Origin.Should().Be("Leagues");
        notification.CreationDateTime.Should().Be(timestamp);
    }
}

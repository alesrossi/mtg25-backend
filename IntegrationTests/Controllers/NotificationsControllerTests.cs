using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using API.Dtos.Notifications;
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
        dto!.Id.Should().Be(notification.Id);
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
        await CreateNotificationAsync(user.Id, isInstant: true);
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var response = await client.GetAsync("/api/notifications");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<List<NotificationDto>>(payload, JsonContentHelper.DefaultOptions);
        dto.Should().NotBeNull();
        dto!.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetNotifications_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/notifications");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetInstantNotifications_ForCurrentUser_ReturnsNotifications()
    {
        var user = await CreateTestUserAsync("notifications-instant@test.com", "notifications_instant");
        await CreateNotificationAsync(user.Id, isInstant: true, name: "Instant Alert");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var response = await client.GetAsync("/api/notifications/instant");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<List<NotificationDto>>(payload, JsonContentHelper.DefaultOptions);
        dto.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteNotification_WithExistingNotification_ReturnsSuccess()
    {
        var user = await CreateTestUserAsync("notifications-delete@test.com", "notifications_delete");
        var notification = await CreateNotificationAsync(user.Id);
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var response = await client.PostAsync($"/api/notifications/{notification.Id}", JsonContentHelper.CreateContent(new { }));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteNotification_WithoutAuthentication_ReturnsUnauthorized()
    {
        var user = await CreateTestUserAsync("notifications-delete-unauth@test.com", "notifications_delete_unauth");
        var notification = await CreateNotificationAsync(user.Id);
        using var client = _factory.CreateClient();

        var response = await client.PostAsync($"/api/notifications/{notification.Id}", JsonContentHelper.CreateContent(new { }));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateNotificationStatus_WithExistingNotification_ReturnsSuccess()
    {
        var user = await CreateTestUserAsync("notifications-update@test.com", "notifications_update");
        var notification = await CreateNotificationAsync(user.Id, isRead: false);
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var response = await client.PutAsync($"/api/notifications/{notification.Id}", JsonContentHelper.CreateContent(new { }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateNotificationStatus_WithoutAuthentication_ReturnsUnauthorized()
    {
        var user = await CreateTestUserAsync("notifications-update-unauth@test.com", "notifications_update_unauth");
        var notification = await CreateNotificationAsync(user.Id);
        using var client = _factory.CreateClient();

        var response = await client.PutAsync($"/api/notifications/{notification.Id}", JsonContentHelper.CreateContent(new { }));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private Task<AppUser> CreateTestUserAsync(string email, string userName) =>
        TestUserFactory.CreateAsync(_factory.Services, _testDataBuilder, email, userName, requirePassword: true);

    private async Task<Notification> CreateNotificationAsync(
        string userId,
        bool isInstant = false,
        bool isRead = false,
        string? name = null,
        string? message = null)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var user = await context.Users.FirstAsync(u => u.Id == userId);

        var notification = new Notification
        {
            Name = name ?? $"Notification {Guid.NewGuid():N}"[..16],
            Message = message ?? "Test notification body for integration tests",
            IsRead = isRead,
            Origin = "IntegrationTests",
            CreationDateTime = DateTime.UtcNow,
            AppUserId = userId,
            AppUser = user
        };

        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        return notification;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Dtos.Notifications;
using API.Services;
using Core.Models.Identity;
using FluentAssertions;
using Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

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
            Message = "Player requested to join",
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
        stored.CreationDateTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetNotificationAsync_WhenOwnedByUser_ReturnsNotificationDto()
    {
        await using var context = CreateContext();
        var existing = new Notification
        {
            Name = "match_start",
            Message = "Round one is starting",
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
        result.Message.Should().Be(existing.Message);
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
        var userId = "user-3";
        context.Notifications.AddRange(
            new Notification
            {
                Name = "older",
                Message = "Old message",
                Origin = "League",
                AppUserId = userId,
                CreationDateTime = DateTime.UtcNow.AddDays(-1),
                AppUser = null!
            },
            new Notification
            {
                Name = "newer",
                Message = "New message",
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
        result.First().Name.Should().Be("newer");
        result.Last().Name.Should().Be("older");
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

    private static AppIdentityDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppIdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new AppIdentityDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static NotificationService CreateService(AppIdentityDbContext context) =>
        new(context, NullLogger<NotificationService>.Instance);
}

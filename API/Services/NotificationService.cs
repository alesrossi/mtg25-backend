using System.Text.Json;
using API.Constants;
using API.Dtos.Notifications;
using API.Logging;
using Core.Enums;
using Core.Models.Identity;
using Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace API.Services;

public class NotificationService
{
    private readonly AppIdentityDbContext _context;
    private readonly ILogger<NotificationService> _logger;
    private readonly IUserSettingsService _userSettingsService;
    private readonly IMessageLocalizer _messageLocalizer;
    private readonly IServiceScopeFactory? _serviceScopeFactory;
    
    private const string CreateNotificationOperation = "Notifications.Create";
    private const string DeleteNotificationOperation = "Notifications.Delete";
    private const string UpdateNotificationOperation = "Notifications.Update";
    
    /// <summary>
    /// Main constructor for dependency injection.
    /// IServiceScopeFactory is optional to support test scenarios and avoid circular dependency issues
    /// when NotificationService is used by LeagueService. It's used to lazily resolve ILeagueService
    /// when needed, avoiding constructor-level circular dependencies.
    /// </summary>
    public NotificationService(
        AppIdentityDbContext context,
        ILogger<NotificationService> logger,
        IUserSettingsService userSettingsService,
        IMessageLocalizer messageLocalizer,
        IServiceScopeFactory? serviceScopeFactory = null)
    {
        _context = context;
        _logger = logger;
        _userSettingsService = userSettingsService;
        _messageLocalizer = messageLocalizer;
        _serviceScopeFactory = serviceScopeFactory;
    }

    /// <summary>
    /// Constructor for test scenarios that don't require all dependencies.
    /// </summary>
    public NotificationService(AppIdentityDbContext context, ILogger<NotificationService> logger)
        : this(
            context,
            logger,
            new UserSettingsService(context),
            new MessageLocalizationService(new UserSettingsService(context), NullLogger<MessageLocalizationService>.Instance),
            null)
    {
    }

    public async Task<Notification> CreateNotificationAsync(NewNotificationDto newNotification)
    {
        using var scope = _logger.BeginOperationScope(CreateNotificationOperation, newNotification.Name);
        _logger.LogOperationStart(CreateNotificationOperation, new { newNotification.Name });
        
        var notification = new Notification
        {
            Name = newNotification.Name,
            Message = newNotification.Message,
            MessageKey = newNotification.MessageKey,
            MessageArgsJson = SerializeArgs(newNotification.MessageArgs),
            Origin = newNotification.Origin,
            ObjectId = (string?)newNotification.ObjectId!,
            CreationDateTime = DateTime.UtcNow,
            AppUserId = newNotification.AppUserId,
            AppUser = null!,
        };
        _context.Add(notification);
        
        await _context.SaveChangesAsync();

        _logger.LogOperationSuccess(CreateNotificationOperation, new { notification.Id, newNotification.Name });
        return notification;
    }

    public async Task<NotificationDto?> GetNotificationAsync(int id, string appUserId)
    {
        var notification = await _context.Notifications
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id && n.AppUserId == appUserId);

        if (notification is null)
        {
            return null;
        }

        var language = await ResolveUserLanguageAsync(appUserId);
        var message = ResolveMessage(notification, language);

        return new NotificationDto
        {
            Id = notification.Id,
            Name = notification.Name,
            Message = message,
            IsRead = notification.IsRead,
            Approval = notification.Approval,
            Origin = notification.Origin,
            CreationDateTime = notification.CreationDateTime
        };
    }

    public async Task<IReadOnlyList<NotificationDto>> GetUserNotificationsAsync(string appUserId)
    {
        var notifications = await _context.Notifications
            .AsNoTracking()
            .Where(n => n.AppUserId == appUserId)
            .OrderByDescending(n => n.CreationDateTime)
            .ToListAsync();

        var language = await ResolveUserLanguageAsync(appUserId);

        return notifications.Select(notification => new NotificationDto
        {
            Id = notification.Id,
            Name = notification.Name,
            Message = ResolveMessage(notification, language),
            IsRead = notification.IsRead,
            Approval = notification.Approval,
            Origin = notification.Origin,
            CreationDateTime = notification.CreationDateTime
        }).ToList();
    }
    
    public async Task<bool> DeleteNotificationAsync(int id)
    {
        using var scope = _logger.BeginOperationScope(DeleteNotificationOperation, id);
        _logger.LogOperationStart(DeleteNotificationOperation, new { id });

        var notification = await _context.Notifications.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id);
        if (notification is not null)
        {
            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();
        }
        else
        {
            return false;
        }

        _logger.LogOperationSuccess(DeleteNotificationOperation, new { id, notification.Name });
        return true;
    }
    
    public async Task<bool> UpdateNotificationAsync(List<int> ids, bool? isRead, bool? approval)
    {
        

        await _context.Notifications.AsNoTracking().Where(n => ids.Contains(n.Id)).ForEachAsync(notification =>
        {
            using var scope = _logger.BeginOperationScope(UpdateNotificationOperation, notification.Id);
            _logger.LogOperationStart(UpdateNotificationOperation, new { notification.Id });
            
            if (isRead is not null)
            {
                notification.IsRead = isRead.Value;
                _context.Notifications.Update(notification);
                
            }
            
            if (approval is not null)
            {
                notification.Approval = approval.Value;
                _context.Notifications.Update(notification);
            }
            
            _logger.LogOperationSuccess(UpdateNotificationOperation, new { notification.Id, notification.Name });
            
        });
        
        await _context.SaveChangesAsync();

        
        return true;
    }
    
    public async Task DeleteNotificationsAsync(string name, string objectId)
    {
        var notifications = await _context.Notifications
            .Where(n => n.Name == name && n.ObjectId == objectId)
            .ToListAsync();

        if (notifications.Count == 0)
        {
            return;
        }

        _context.Notifications.RemoveRange(notifications);
        await _context.SaveChangesAsync();
    }
    
    public async Task<bool> HasApprovedNotificationAsync(string name, string key, CancellationToken cancellationToken = default)
    {
        return await _context.Notifications
            .AsNoTracking()
            .AnyAsync(
                n => n.Name == name
                     && n.Approval
                     && (n.Origin.StartsWith(key) || n.ObjectId == key),
                cancellationToken);
    }
    
    /// <summary>
    /// Updates notification status (read/approval) and performs side effects based on notification type.
    /// When approving a league join request (<see cref="NotificationConstants.RequestJoinLeague"/>):
    /// - Only the league owner or a league admin can approve; otherwise <see cref="UnauthorizedAccessException"/> is thrown (no approval is persisted).
    /// - On success, the user is added to the league directly via the league service (no separate join step).
    /// - The requester receives a notification that they were added; the league owner receives a player_joined_league notification.
    /// General approach for league join approval:
    /// - Avoids HTTP overhead (no need to call the join endpoint)
    /// - Simplifies error handling (exceptions propagate directly)
    /// - Ensures authorization is checked before any persistence
    /// - Provides immediate feedback if the join operation fails
    /// </summary>
    public async Task<bool> UpdateNotificationAsync(List<int> ids, bool? isRead, bool? approval, string? userToUpdate)
    {
        var leagueJoinRequests = new List<(int leagueId, string userId)>();

        await _context.Notifications.AsNoTracking().Where(n => ids.Contains(n.Id)).ForEachAsync(notification =>
        {
            using var scope = _logger.BeginOperationScope(UpdateNotificationOperation, notification.Id);
            _logger.LogOperationStart(UpdateNotificationOperation, new { notification.Id });
            
            if (isRead is not null)
            {
                notification.IsRead = isRead.Value;
                _context.Notifications.Update(notification);
            }
            
            if (approval is not null)
            {
                notification.Approval = approval.Value;
                _context.Notifications.Update(notification);
                
                if (notification.Name == NotificationConstants.RequestJoinLeague && approval.Value)
                {
                    if (int.TryParse(notification.ObjectId, out var leagueId) && 
                        !string.IsNullOrEmpty(notification.Origin))
                    {
                        var originParts = notification.Origin.Split('.');
                        if (originParts.Length >= 2)
                        {
                            var userId = originParts[1];
                            leagueJoinRequests.Add((leagueId, userId));
                        }
                    }
                }
            }
            
            _logger.LogOperationSuccess(UpdateNotificationOperation, new { notification.Id, notification.Name });
        });

        if (leagueJoinRequests.Count > 0 && userToUpdate is not null && _serviceScopeFactory is not null)
        {
            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            var leagueService = scope.ServiceProvider.GetRequiredService<ILeagueService>();
            foreach (var (leagueId, _) in leagueJoinRequests)
            {
                var isAdmin = await leagueService.IsLeagueAdminAsync(leagueId, userToUpdate);
                if (!isAdmin)
                {
                    _logger.LogWarning(
                        "User {UserId} is not admin/owner of league {LeagueId}; cannot approve join request",
                        userToUpdate, leagueId);
                    throw new UnauthorizedAccessException();
                }
            }
        }
        
        await _context.SaveChangesAsync();

        if (leagueJoinRequests.Count > 0 && _serviceScopeFactory is not null)
        {
            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            var leagueService = scope.ServiceProvider.GetRequiredService<ILeagueService>();
            
            foreach (var (leagueId, userId) in leagueJoinRequests)
            {
                try
                {
                    await leagueService.JoinLeagueAsync(leagueId, userId);
                    _logger.LogInformation(
                        "User {UserId} successfully joined league {LeagueId} after approval",
                        userId, leagueId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to add user {UserId} to league {LeagueId} after approval",
                        userId, leagueId);
                    throw;
                }
            }
        }
        else if (leagueJoinRequests.Count > 0)
        {
            _logger.LogWarning(
                "Cannot process {Count} league join requests: IServiceScopeFactory not available",
                leagueJoinRequests.Count);
        }
        
        return true;
    }

    private static string? SerializeArgs(string[]? args)
    {
        if (args is null || args.Length == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(args);
    }

    private static string[] DeserializeArgs(string? argsJson)
    {
        if (string.IsNullOrWhiteSpace(argsJson))
        {
            return [];
        }

        return JsonSerializer.Deserialize<string[]>(argsJson) ?? [];
    }

    private async Task<Language?> ResolveUserLanguageAsync(string userId)
    {
        var settings = await _userSettingsService.GetSettingsAsync(userId);
        return settings?.LanguageUi;
    }

    private string ResolveMessage(Notification notification, Language? language)
    {
        if (!string.IsNullOrWhiteSpace(notification.MessageKey))
        {
            var args = DeserializeArgs(notification.MessageArgsJson);
            // ReSharper disable once CoVariantArrayConversion
            return _messageLocalizer.GetMessageForLanguage(language, notification.MessageKey, args);
        }

        return notification.Message;
    }
}

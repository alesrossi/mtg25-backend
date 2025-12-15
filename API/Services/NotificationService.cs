using API.Dtos.Notifications;
using API.Endpoints.Notifications;
using API.Logging;
using Core.Interfaces;
using Core.Models.Identity;
using Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace API.Services;

public class NotificationService
{
    private readonly AppIdentityDbContext _context;
    private readonly ILogger<NotificationService> _logger;
    
    private const string CreateNotificationOperation = "Notifications.Create";
    private const string DeleteNotificationOperation = "Notifications.Delete";
    private const string UpdateNotificationOperation = "Notifications.Update";
    
    public NotificationService(AppIdentityDbContext context, ILogger<NotificationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Notification> CreateNotificationAsync(NewNotificationDto newNotification)
    {
        using var scope = _logger.BeginOperationScope(CreateNotificationOperation, newNotification.Name);
        _logger.LogOperationStart(CreateNotificationOperation, new { newNotification.Name });
        
        var notification = new Notification
        {
            Name = newNotification.Name,
            Message = newNotification.Message,
            Origin = newNotification.Origin,
            ObjectId = newNotification.ObjectId,
            CreationDateTime = DateTime.UtcNow,
            AppUserId = newNotification.AppUserId,
            AppUser = null!,
        };
        _context.Add(notification);
        
        await _context.SaveChangesAsync();

        _logger.LogOperationSuccess(CreateNotificationOperation, new { notification.Id, newNotification.Name });
        return notification;
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
}
using Tria.Models;

namespace Tria.Services;

public interface INotificationService
{
    Task CreateAsync(string userId, string title, string message, string? linkUrl = null);
    Task<List<UserNotification>> GetForUserAsync(string userId, int limit = 50);
    Task<int> GetUnreadCountAsync(string userId);
    Task MarkAllReadAsync(string userId);
}

using Microsoft.EntityFrameworkCore;
using Tria.Data;
using Tria.Models;

namespace Tria.Services;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;

    public NotificationService(ApplicationDbContext db) => _db = db;

    public async Task CreateAsync(string userId, string title, string message, string? linkUrl = null)
    {
        _db.UserNotifications.Add(new UserNotification
        {
            UserId    = userId,
            Title     = title,
            Message   = message,
            LinkUrl   = linkUrl,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    public Task<List<UserNotification>> GetForUserAsync(string userId, int limit = 50)
        => _db.UserNotifications
              .Where(n => n.UserId == userId)
              .OrderByDescending(n => n.CreatedAt)
              .Take(limit)
              .ToListAsync();

    public Task<int> GetUnreadCountAsync(string userId)
        => _db.UserNotifications
              .CountAsync(n => n.UserId == userId && !n.IsRead);

    public async Task MarkAllReadAsync(string userId)
    {
        await _db.UserNotifications
                 .Where(n => n.UserId == userId && !n.IsRead)
                 .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }
}

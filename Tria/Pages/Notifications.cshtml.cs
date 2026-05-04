using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using Tria.Models;
using Tria.Services;

namespace Tria.Pages;

[Authorize(Roles = "Student")]
public class NotificationsModel : PageModel
{
    private readonly INotificationService _notifications;

    public List<UserNotification> Items { get; set; } = new();

    public NotificationsModel(INotificationService notifications)
        => _notifications = notifications;

    public async Task OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        Items = await _notifications.GetForUserAsync(userId);
        await _notifications.MarkAllReadAsync(userId);
    }
}

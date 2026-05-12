using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Tria.Data;
using Tria.Models;
using Tria.Services;

namespace Tria.Pages;

[Authorize(Roles = "Student")]
public class DashboardModel : PageModel
{
    private readonly ILearningService _learning;
    private readonly IProgressService _progress;
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notifications;

    public List<Course> Courses { get; set; } = new();
    public Dictionary<int, int> CourseProgress { get; set; } = new();
    public int TotalXp { get; set; }
    public int UnreadNotificationCount { get; set; }

    public DashboardModel(ILearningService learning, IProgressService progress, ApplicationDbContext db, INotificationService notifications)
    {
        _learning      = learning;
        _progress      = progress;
        _db            = db;
        _notifications = notifications;
    }

    public async Task OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var allCourses = _learning.GetAllCourses();

        var assignedIds = await _db.UserCourseAssignments
            .Where(a => a.UserId == userId)
            .Select(a => a.CourseId)
            .ToListAsync();
        Courses = allCourses.Where(c => assignedIds.Contains(c.Id)).ToList();

        foreach (var course in Courses)
        {
            var allLessons = course.Modules.SelectMany(m => m.Lessons).ToList();
            CourseProgress[course.Id] = await _progress.GetCourseProgressPercentAsync(userId, course.Id, allLessons);
        }

        TotalXp = await _progress.GetTotalXpAsync(userId);
        UnreadNotificationCount = await _notifications.GetUnreadCountAsync(userId);
    }
}

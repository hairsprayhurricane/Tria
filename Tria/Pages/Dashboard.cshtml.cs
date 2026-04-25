using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Tria.Data;
using Tria.Models;
using Tria.Services;

namespace Tria.Pages;

[Authorize]
public class DashboardModel : PageModel
{
    private readonly ILearningService _learning;
    private readonly IProgressService _progress;
    private readonly ApplicationDbContext _db;

    public List<Course> Courses { get; set; } = new();
    public Dictionary<int, int> CourseProgress { get; set; } = new();
    public int TotalXp { get; set; }

    public DashboardModel(ILearningService learning, IProgressService progress, ApplicationDbContext db)
    {
        _learning = learning;
        _progress = progress;
        _db = db;
    }

    public async Task OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var allCourses = _learning.GetAllCourses();

        if (User.IsInRole("Admin"))
        {
            Courses = allCourses;
        }
        else
        {
            var assignedIds = await _db.UserCourseAssignments
                .Where(a => a.UserId == userId)
                .Select(a => a.CourseId)
                .ToListAsync();
            Courses = allCourses.Where(c => assignedIds.Contains(c.Id)).ToList();
        }

        foreach (var course in Courses)
        {
            var allLessons = course.Modules.SelectMany(m => m.Lessons).ToList();
            CourseProgress[course.Id] = await _progress.GetCourseProgressPercentAsync(userId, course.Id, allLessons);
        }

        TotalXp = await _progress.GetTotalXpAsync(userId);
    }
}

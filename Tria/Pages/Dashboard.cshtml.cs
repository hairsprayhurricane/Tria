using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using Tria.Models;
using Tria.Services;

namespace Tria.Pages;

[Authorize]
public class DashboardModel : PageModel
{
    private readonly ILearningService _learning;
    private readonly IProgressService _progress;

    public List<Course> Courses { get; set; } = new();
    public Dictionary<int, int> CourseProgress { get; set; } = new();
    public int TotalXp { get; set; }

    public DashboardModel(ILearningService learning, IProgressService progress)
    {
        _learning = learning;
        _progress = progress;
    }

    public async Task OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        Courses = _learning.GetAllCourses();

        foreach (var course in Courses)
        {
            var allLessons = course.Modules.SelectMany(m => m.Lessons).ToList();
            CourseProgress[course.Id] = await _progress.GetCourseProgressPercentAsync(userId, course.Id, allLessons);
        }

        TotalXp = await _progress.GetTotalXpAsync(userId);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using Tria.Models;
using Tria.Services;

namespace Tria.Pages;

[Authorize]
public class ProgressModel : PageModel
{
    private readonly ILearningService _learning;
    private readonly IProgressService _progress;

    public List<Course> Courses { get; set; } = new();
    public Dictionary<int, int> CourseProgress { get; set; } = new();
    public Dictionary<int, int> ModuleProgress { get; set; } = new();
    public Dictionary<int, UserLessonProgress?> LessonProgressMap { get; set; } = new();
    public Dictionary<int, UserTestAttempt?> LatestAttempts { get; set; } = new();
    public int TotalXp { get; set; }

    public ProgressModel(ILearningService learning, IProgressService progress)
    {
        _learning = learning;
        _progress = progress;
    }

    public async Task OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        Courses = _learning.GetAllCourses();
        TotalXp = await _progress.GetTotalXpAsync(userId);

        foreach (var course in Courses)
        {
            var allLessons = course.Modules.SelectMany(m => m.Lessons).ToList();
            CourseProgress[course.Id] = await _progress.GetCourseProgressPercentAsync(userId, course.Id, allLessons);

            foreach (var module in course.Modules)
            {
                var modLessons = module.Lessons.Where(l => l.IsActive).ToList();
                ModuleProgress[module.Id] = await _progress.GetModuleProgressPercentAsync(userId, module.Id, modLessons);

                foreach (var lesson in module.Lessons)
                {
                    LessonProgressMap[lesson.Id] = await _progress.GetLessonProgressAsync(userId, lesson.Id);
                    LatestAttempts[lesson.Id] = await _progress.GetLatestAttemptAsync(userId, lesson.Id);
                }
            }
        }
    }
}

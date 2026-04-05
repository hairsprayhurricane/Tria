using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using Tria.Models;
using Tria.Services;

namespace Tria.Pages.Courses;

[Authorize]
public class ModuleDetailModel : PageModel
{
    private readonly ILearningService _learning;
    private readonly IProgressService _progress;

    public CourseModule? Module { get; set; }
    public Course? Course { get; set; }
    public Dictionary<int, UserLessonProgress?> LessonProgress { get; set; } = new();
    public Dictionary<int, UserTestAttempt?> LatestAttempts { get; set; } = new();

    public ModuleDetailModel(ILearningService learning, IProgressService progress)
    {
        _learning = learning;
        _progress = progress;
    }

    public async Task<IActionResult> OnGetAsync(int moduleId)
    {
        Module = _learning.GetModuleById(moduleId);
        if (Module == null) return NotFound();

        Course = _learning.GetCourseById(Module.CourseId);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        // TODO: Sequential lesson unlock will be implemented here.
        // Currently all lessons are open by default.

        foreach (var lesson in Module.Lessons)
        {
            LessonProgress[lesson.Id] = await _progress.GetLessonProgressAsync(userId, lesson.Id);
            LatestAttempts[lesson.Id] = await _progress.GetLatestAttemptAsync(userId, lesson.Id);
        }

        return Page();
    }
}

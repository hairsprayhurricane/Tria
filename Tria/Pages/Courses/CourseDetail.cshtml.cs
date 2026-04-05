using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using Tria.Models;
using Tria.Services;

namespace Tria.Pages.Courses;

[Authorize]
public class CourseDetailModel : PageModel
{
    private readonly ILearningService _learning;
    private readonly IProgressService _progress;

    public Course? Course { get; set; }
    public Dictionary<int, int> ModuleProgress { get; set; } = new();

    public CourseDetailModel(ILearningService learning, IProgressService progress)
    {
        _learning = learning;
        _progress = progress;
    }

    public async Task<IActionResult> OnGetAsync(int courseId)
    {
        Course = _learning.GetCourseById(courseId);
        if (Course == null) return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        foreach (var module in Course.Modules)
        {
            var lessons = module.Lessons.Where(l => l.IsActive).ToList();
            ModuleProgress[module.Id] = await _progress.GetModuleProgressPercentAsync(userId, module.Id, lessons);
        }

        return Page();
    }
}

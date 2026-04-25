using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Tria.Data;
using Tria.Models;
using Tria.Services;

namespace Tria.Pages.Courses;

[Authorize]
public class CourseDetailModel : PageModel
{
    private readonly ILearningService _learning;
    private readonly IProgressService _progress;
    private readonly ApplicationDbContext _db;

    public Course? Course { get; set; }
    public Dictionary<int, int> ModuleProgress { get; set; } = new();

    public CourseDetailModel(ILearningService learning, IProgressService progress, ApplicationDbContext db)
    {
        _learning = learning;
        _progress = progress;
        _db = db;
    }

    public async Task<IActionResult> OnGetAsync(int courseId)
    {
        Course = _learning.GetCourseById(courseId);
        if (Course == null) return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        if (!User.IsInRole("Admin"))
        {
            var hasAccess = await _db.UserCourseAssignments
                .AnyAsync(a => a.UserId == userId && a.CourseId == courseId);
            if (!hasAccess) return RedirectToPage("/Dashboard");
        }

        foreach (var module in Course.Modules)
        {
            var lessons = module.Lessons.Where(l => l.IsActive).ToList();
            ModuleProgress[module.Id] = await _progress.GetModuleProgressPercentAsync(userId, module.Id, lessons);
        }

        return Page();
    }
}

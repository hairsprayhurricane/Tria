using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Tria.Data;
using Tria.Models;
using Tria.Services;

namespace Tria.Pages.Admin;

[Authorize(Roles = "Admin")]
public class UserDetailModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly ILearningService _learning;
    private readonly IProgressService _progress;

    public IdentityUser? TargetUser { get; set; }
    public string UserRole { get; set; } = "—";
    public List<CourseRow> Courses { get; set; } = new();
    public int TotalXp { get; set; }
    public int CompletedLessons { get; set; }

    public class CourseRow
    {
        public int CourseId { get; set; }
        public string Title { get; set; } = "";
        public string? Color { get; set; }
        public bool IsAssigned { get; set; }
        public int ProgressPercent { get; set; }
    }

    public UserDetailModel(UserManager<IdentityUser> userManager, ApplicationDbContext db,
        ILearningService learning, IProgressService progress)
    {
        _userManager = userManager;
        _db = db;
        _learning = learning;
        _progress = progress;
    }

    public async Task<IActionResult> OnGetAsync(string userId)
    {
        TargetUser = await _userManager.FindByIdAsync(userId);
        if (TargetUser == null) return NotFound();

        var roles = await _userManager.GetRolesAsync(TargetUser);
        UserRole = roles.FirstOrDefault() ?? "—";

        var assignedIds = await _db.UserCourseAssignments
            .Where(a => a.UserId == userId)
            .Select(a => a.CourseId)
            .ToListAsync();

        var allCourses = _learning.GetAllCourses();
        foreach (var course in allCourses)
        {
            var allLessons = course.Modules.SelectMany(m => m.Lessons).ToList();
            var pct = await _progress.GetCourseProgressPercentAsync(userId, course.Id, allLessons);
            Courses.Add(new CourseRow
            {
                CourseId = course.Id,
                Title = course.Title,
                Color = course.Color,
                IsAssigned = assignedIds.Contains(course.Id),
                ProgressPercent = pct,
            });
        }

        TotalXp = await _progress.GetTotalXpAsync(userId);
        CompletedLessons = await _db.UserLessonProgress
            .CountAsync(p => p.UserId == userId && p.MaterialsCompleted);

        return Page();
    }

    public async Task<IActionResult> OnPostSaveCoursesAsync(string userId, List<int> assignedCourseIds)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        var current = await _db.UserCourseAssignments
            .Where(a => a.UserId == userId)
            .ToListAsync();

        var currentIds = current.Select(a => a.CourseId).ToHashSet();
        var newIds = assignedCourseIds.ToHashSet();

        var toRemove = current.Where(a => !newIds.Contains(a.CourseId)).ToList();
        _db.UserCourseAssignments.RemoveRange(toRemove);

        foreach (var id in newIds.Where(id => !currentIds.Contains(id)))
        {
            _db.UserCourseAssignments.Add(new UserCourseAssignment
            {
                UserId = userId,
                CourseId = id,
                AssignedAt = DateTime.UtcNow,
            });
        }

        await _db.SaveChangesAsync();
        return RedirectToPage(new { userId });
    }
}

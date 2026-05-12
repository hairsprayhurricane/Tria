using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Tria.Data;
using Tria.Services;

namespace Tria.Pages.Admin;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly ILearningService _learning;

    public List<UserRow> Users { get; set; } = new();
    public int TotalCourses { get; set; }
    public int TotalAssignments { get; set; }

    public class UserRow
    {
        public string Id { get; set; } = "";
        public string Email { get; set; } = "";
        public string Role { get; set; } = "—";
        public int AssignedCourses { get; set; }
        public int CompletedLessons { get; set; }
    }

    public IndexModel(UserManager<IdentityUser> userManager, ApplicationDbContext db, ILearningService learning)
    {
        _userManager = userManager;
        _db = db;
        _learning = learning;
    }

    public async Task OnGetAsync()
    {
        TotalCourses = _learning.GetAllCourses().Count;
        TotalAssignments = await _db.UserCourseAssignments.CountAsync();

        var assignmentCounts = await _db.UserCourseAssignments
            .GroupBy(a => a.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count);

        var lessonCounts = await _db.UserLessonProgress
            .Where(p => p.MaterialsCompleted)
            .GroupBy(p => p.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count);

        foreach (var user in _userManager.Users.OrderBy(u => u.Email).ToList())
        {
            var roles = await _userManager.GetRolesAsync(user);
            Users.Add(new UserRow
            {
                Id = user.Id,
                Email = user.Email ?? user.UserName ?? "—",
                Role = roles.FirstOrDefault() ?? "—",
                AssignedCourses = assignmentCounts.GetValueOrDefault(user.Id, 0),
                CompletedLessons = lessonCounts.GetValueOrDefault(user.Id, 0),
            });
        }
    }

    public async Task<IActionResult> OnPostDeleteUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user != null && user.Email != "admin@tria.com")
            await _userManager.DeleteAsync(user);
        return RedirectToPage();
    }
}

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
public class AssignCourseModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly ILearningService _learning;

    public List<UserOption> Users { get; set; } = new();
    public List<CourseOption> Courses { get; set; } = new();
    // userId → list of already-assigned courseIds (for client-side highlighting)
    public Dictionary<string, List<int>> UserAssignments { get; set; } = new();

    public string? SuccessMessage { get; set; }

    public class UserOption
    {
        public string Id { get; set; } = "";
        public string Email { get; set; } = "";
        public string Role { get; set; } = "—";
    }

    public class CourseOption
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? Color { get; set; }
    }

    public AssignCourseModel(UserManager<IdentityUser> userManager, ApplicationDbContext db, ILearningService learning)
    {
        _userManager = userManager;
        _db = db;
        _learning = learning;
    }

    public async Task OnGetAsync()
    {
        await LoadDataAsync();
    }

    public async Task<IActionResult> OnPostAsync(string userId, int courseId)
    {
        await LoadDataAsync();

        if (string.IsNullOrEmpty(userId) || courseId == 0)
            return Page();

        var already = await _db.UserCourseAssignments
            .AnyAsync(a => a.UserId == userId && a.CourseId == courseId);

        if (!already)
        {
            _db.UserCourseAssignments.Add(new UserCourseAssignment
            {
                UserId = userId,
                CourseId = courseId,
                AssignedAt = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync();
        }

        var user = Users.FirstOrDefault(u => u.Id == userId);
        var course = Courses.FirstOrDefault(c => c.Id == courseId);
        SuccessMessage = already
            ? $"Курс «{course?.Title}» уже назначен пользователю {user?.Email}."
            : $"Курс «{course?.Title}» успешно назначен пользователю {user?.Email}.";

        return Page();
    }

    private async Task LoadDataAsync()
    {
        var adminIds = (await _userManager.GetUsersInRoleAsync("Admin")).Select(u => u.Id).ToHashSet();

        foreach (var user in _userManager.Users.OrderBy(u => u.Email).ToList())
        {
            if (adminIds.Contains(user.Id)) continue;
            var roles = await _userManager.GetRolesAsync(user);
            Users.Add(new UserOption
            {
                Id = user.Id,
                Email = user.Email ?? user.UserName ?? "—",
                Role = roles.FirstOrDefault() ?? "—",
            });
        }

        Courses = _learning.GetAllCourses().Select(c => new CourseOption
        {
            Id = c.Id,
            Title = c.Title,
            Color = c.Color,
        }).ToList();

        var allAssignments = await _db.UserCourseAssignments.ToListAsync();
        UserAssignments = allAssignments
            .GroupBy(a => a.UserId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.CourseId).ToList());
    }
}

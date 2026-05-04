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
    public Dictionary<string, List<int>> UserAssignments { get; set; } = new();

    public string? SuccessMessage { get; set; }
    public bool SuccessIsWarn { get; set; }
    public string? LastUserId { get; set; }

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

    public async Task<IActionResult> OnPostAsync(string userId, List<int> courseIds)
    {
        LastUserId = userId;

        if (!string.IsNullOrEmpty(userId) && courseIds != null && courseIds.Any())
        {
            var existingIds = (await _db.UserCourseAssignments
                .Where(a => a.UserId == userId)
                .Select(a => a.CourseId)
                .ToListAsync()).ToHashSet();

            var toAdd = courseIds.Where(id => !existingIds.Contains(id)).ToList();

            foreach (var courseId in toAdd)
            {
                _db.UserCourseAssignments.Add(new UserCourseAssignment
                {
                    UserId = userId,
                    CourseId = courseId,
                    AssignedAt = DateTime.UtcNow,
                });
            }

            if (toAdd.Any())
                await _db.SaveChangesAsync();

            // Load AFTER save so UserAssignments reflects the new state
            await LoadDataAsync();

            var user = Users.FirstOrDefault(u => u.Id == userId);
            var skipped = courseIds.Count - toAdd.Count;

            if (toAdd.Count == 0)
            {
                SuccessIsWarn = true;
                SuccessMessage = skipped == 1
                    ? "Этот курс уже был назначен."
                    : $"Все {skipped} выбранных курсов уже были назначены.";
            }
            else
            {
                SuccessIsWarn = false;
                var addedTitles = Courses
                    .Where(c => toAdd.Contains(c.Id))
                    .Select(c => c.Title)
                    .ToList();

                SuccessMessage = toAdd.Count == 1
                    ? $"Курс «{addedTitles[0]}» назначен → {user?.Email}"
                    : $"Назначено {toAdd.Count} курсов → {user?.Email}" +
                      (skipped > 0 ? $" (ещё {skipped} уже было назначено)" : "");
            }
        }
        else
        {
            await LoadDataAsync();
        }

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

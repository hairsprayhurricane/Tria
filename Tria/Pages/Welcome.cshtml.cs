using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Tria.Data;
using Tria.Services;

namespace Tria.Pages;

public class WelcomeModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ILearningService _learning;

    public List<ReviewItem> Reviews { get; set; } = new();
    public int CourseCount { get; set; }

    public record ReviewItem(string AuthorName, string CourseTitle, int Rating, string Comment, DateTime CreatedAt);

    public WelcomeModel(ApplicationDbContext db, UserManager<IdentityUser> userManager, ILearningService learning)
    {
        _db = db;
        _userManager = userManager;
        _learning = learning;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToPage("/Dashboard");

        var allCourses = _learning.GetAllCourses();
        CourseCount = allCourses.Count;
        var courseMap = allCourses.ToDictionary(c => c.Id, c => c.Title);

        var raw = await _db.CourseReviews
            .Where(r => r.IsVisible && r.Comment.Length > 0)
            .OrderByDescending(r => r.CreatedAt)
            .Take(6)
            .ToListAsync();

        var userIds = raw.Select(r => r.UserId).Distinct().ToList();
        var emailMap = await _userManager.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToDictionaryAsync(u => u.Id, u => u.Email ?? "");

        Reviews = raw.Select(r =>
        {
            var email = emailMap.GetValueOrDefault(r.UserId, "");
            var name = email.Contains('@') ? email[..email.IndexOf('@')] : email;
            var course = courseMap.GetValueOrDefault(r.CourseId, $"Курс #{r.CourseId}");
            return new ReviewItem(name, course, r.Rating, r.Comment, r.CreatedAt);
        }).ToList();

        return Page();
    }
}

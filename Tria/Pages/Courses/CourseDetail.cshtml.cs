using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Tria.Data;
using Tria.Models;
using Tria.Services;

namespace Tria.Pages.Courses;

[Authorize(Roles = "Admin,Student")]
public class CourseDetailModel : PageModel
{
    private readonly ILearningService _learning;
    private readonly IProgressService _progress;
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public Course? Course { get; set; }
    public Dictionary<int, int> ModuleProgress { get; set; } = new();
    public bool IsEnrolled { get; set; }

    public CourseReview? MyReview { get; set; }
    public List<ReviewRow> Reviews { get; set; } = new();
    public double AverageRating { get; set; }

    [BindProperty] public int ReviewRating { get; set; }
    [BindProperty] public string ReviewComment { get; set; } = "";

    public class ReviewRow
    {
        public string AuthorEmail { get; set; } = "";
        public int Rating { get; set; }
        public string Comment { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public CourseDetailModel(ILearningService learning, IProgressService progress,
        ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _learning = learning;
        _progress = progress;
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> OnGetAsync(int courseId)
    {
        Course = _learning.GetCourseById(courseId);
        if (Course == null) return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        IsEnrolled = await _db.UserCourseAssignments
            .AnyAsync(a => a.UserId == userId && a.CourseId == courseId);

        if (!User.IsInRole("Admin") && !IsEnrolled)
            return RedirectToPage("/Dashboard"); // student not enrolled → back to their course list

        foreach (var module in Course.Modules)
        {
            var lessons = module.Lessons.Where(l => l.IsActive).ToList();
            ModuleProgress[module.Id] = await _progress.GetModuleProgressPercentAsync(userId, module.Id, lessons);
        }

        await LoadReviewsAsync(courseId, userId);
        return Page();
    }

    public async Task<IActionResult> OnPostReviewAsync(int courseId)
    {
        Course = _learning.GetCourseById(courseId);
        if (Course == null) return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        IsEnrolled = await _db.UserCourseAssignments
            .AnyAsync(a => a.UserId == userId && a.CourseId == courseId);

        if (!IsEnrolled) return RedirectToPage("/Dashboard");

        if (ReviewRating < 1 || ReviewRating > 5)
        {
            ModelState.AddModelError("", "Выберите оценку от 1 до 5.");
        }
        else
        {
            var existing = await _db.CourseReviews
                .FirstOrDefaultAsync(r => r.UserId == userId && r.CourseId == courseId);

            if (existing != null)
            {
                existing.Rating = ReviewRating;
                existing.Comment = ReviewComment.Trim();
                existing.CreatedAt = DateTime.UtcNow;
            }
            else
            {
                _db.CourseReviews.Add(new CourseReview
                {
                    UserId = userId,
                    CourseId = courseId,
                    Rating = ReviewRating,
                    Comment = ReviewComment.Trim(),
                });
            }
            await _db.SaveChangesAsync();
        }

        foreach (var module in Course.Modules)
        {
            var lessons = module.Lessons.Where(l => l.IsActive).ToList();
            ModuleProgress[module.Id] = await _progress.GetModuleProgressPercentAsync(userId, module.Id, lessons);
        }

        await LoadReviewsAsync(courseId, userId);
        return Page();
    }

    private async Task LoadReviewsAsync(int courseId, string userId)
    {
        MyReview = await _db.CourseReviews
            .FirstOrDefaultAsync(r => r.UserId == userId && r.CourseId == courseId);

        var visible = await _db.CourseReviews
            .Where(r => r.CourseId == courseId && r.IsVisible)
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .ToListAsync();

        if (visible.Any())
            AverageRating = visible.Average(r => r.Rating);

        var userIds = visible.Select(r => r.UserId).Distinct().ToList();
        var users = _userManager.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToList();
        var emailMap = users.ToDictionary(u => u.Id, u => u.Email ?? "—");

        Reviews = visible.Select(r => new ReviewRow
        {
            AuthorEmail = emailMap.GetValueOrDefault(r.UserId, "—"),
            Rating = r.Rating,
            Comment = r.Comment,
            CreatedAt = r.CreatedAt,
        }).ToList();
    }
}

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
public class ReviewsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ILearningService _learning;

    public List<ReviewRow> Reviews { get; set; } = new();
    public List<CourseOption> Courses { get; set; } = new();
    public int? FilterCourseId { get; set; }
    public int? FilterRating { get; set; }
    public string? StatusMessage { get; set; }

    public class ReviewRow
    {
        public int Id { get; set; }
        public string AuthorEmail { get; set; } = "";
        public string CourseTitle { get; set; } = "";
        public int CourseId { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public bool IsVisible { get; set; }
    }

    public class CourseOption
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
    }

    public ReviewsModel(ApplicationDbContext db, UserManager<IdentityUser> userManager, ILearningService learning)
    {
        _db = db;
        _userManager = userManager;
        _learning = learning;
    }

    public async Task OnGetAsync(int? courseId, int? rating)
    {
        FilterCourseId = courseId;
        FilterRating = rating;
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostToggleVisibilityAsync(int reviewId, int? courseId, int? rating)
    {
        var review = await _db.CourseReviews.FindAsync(reviewId);
        if (review != null)
        {
            review.IsVisible = !review.IsVisible;
            await _db.SaveChangesAsync();
            StatusMessage = review.IsVisible ? "Отзыв показан." : "Отзыв скрыт.";
        }
        FilterCourseId = courseId;
        FilterRating = rating;
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int reviewId, int? courseId, int? rating)
    {
        var review = await _db.CourseReviews.FindAsync(reviewId);
        if (review != null)
        {
            _db.CourseReviews.Remove(review);
            await _db.SaveChangesAsync();
            StatusMessage = "Отзыв удалён.";
        }
        FilterCourseId = courseId;
        FilterRating = rating;
        await LoadAsync();
        return Page();
    }

    private async Task LoadAsync()
    {
        var allCourses = _learning.GetAllCourses();
        var courseMap = allCourses.ToDictionary(c => c.Id, c => c.Title);
        Courses = allCourses.Select(c => new CourseOption { Id = c.Id, Title = c.Title })
                            .OrderBy(c => c.Title).ToList();

        var query = _db.CourseReviews.AsQueryable();
        if (FilterCourseId.HasValue)
            query = query.Where(r => r.CourseId == FilterCourseId.Value);
        if (FilterRating.HasValue)
            query = query.Where(r => r.Rating == FilterRating.Value);

        var raw = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();

        var userIds = raw.Select(r => r.UserId).Distinct().ToList();
        var emailMap = _userManager.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToDictionary(u => u.Id, u => u.Email ?? "—");

        Reviews = raw.Select(r => new ReviewRow
        {
            Id = r.Id,
            AuthorEmail = emailMap.GetValueOrDefault(r.UserId, "—"),
            CourseTitle = courseMap.GetValueOrDefault(r.CourseId, $"Курс #{r.CourseId}"),
            CourseId = r.CourseId,
            Rating = r.Rating,
            Comment = r.Comment,
            CreatedAt = r.CreatedAt,
            IsVisible = r.IsVisible,
        }).ToList();
    }
}

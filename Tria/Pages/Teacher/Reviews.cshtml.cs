using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Tria.Data;
using Tria.Services;

namespace Tria.Pages.Teacher;

[Authorize(Roles = "Teacher")]
public class ReviewsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ILearningService _learning;

    public List<ReviewRow> Reviews { get; set; } = new();
    public List<CourseOption> Courses { get; set; } = new();
    public int? FilterCourseId { get; set; }

    public class ReviewRow
    {
        public string StudentEmail { get; set; } = "";
        public string CourseTitle { get; set; } = "";
        public int Rating { get; set; }
        public string Comment { get; set; } = "";
        public DateTime CreatedAt { get; set; }
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

    public async Task OnGetAsync(int? courseId)
    {
        FilterCourseId = courseId;

        var teacherId = _userManager.GetUserId(User)!;

        var studentIds = await _db.TeacherStudentAssignments
            .Where(a => a.TeacherId == teacherId)
            .Select(a => a.StudentId)
            .ToListAsync();

        if (!studentIds.Any())
            return;

        var allCourses = _learning.GetAllCourses();
        var courseMap = allCourses.ToDictionary(c => c.Id, c => c.Title);

        var query = _db.CourseReviews
            .Where(r => studentIds.Contains(r.UserId) && r.IsVisible);

        if (FilterCourseId.HasValue)
            query = query.Where(r => r.CourseId == FilterCourseId.Value);

        var raw = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();

        var reviewerIds = raw.Select(r => r.UserId).Distinct().ToList();
        var emailMap = _userManager.Users
            .Where(u => reviewerIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToDictionary(u => u.Id, u => u.Email ?? "—");

        Reviews = raw.Select(r => new ReviewRow
        {
            StudentEmail = emailMap.GetValueOrDefault(r.UserId, "—"),
            CourseTitle = courseMap.GetValueOrDefault(r.CourseId, $"Курс #{r.CourseId}"),
            Rating = r.Rating,
            Comment = r.Comment,
            CreatedAt = r.CreatedAt,
        }).ToList();

        var coursesWithReviews = raw.Select(r => r.CourseId).Distinct().ToHashSet();
        Courses = allCourses
            .Where(c => coursesWithReviews.Contains(c.Id))
            .Select(c => new CourseOption { Id = c.Id, Title = c.Title })
            .OrderBy(c => c.Title)
            .ToList();
    }
}

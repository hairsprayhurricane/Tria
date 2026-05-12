using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Tria.Models;
using Tria.Services;

namespace Tria.Pages.Teacher;

[Authorize(Roles = "Teacher")]
public class IndexModel : PageModel
{
    private readonly ILearningService _learning;
    private readonly IWebHostEnvironment _env;

    public List<Course> Courses { get; set; } = new();
    public bool Deleted { get; set; }

    public IndexModel(ILearningService learning, IWebHostEnvironment env)
    {
        _learning = learning;
        _env = env;
    }

    public void OnGet(bool? deleted)
    {
        Courses = _learning.GetAllCourses();
        Deleted = deleted == true;
    }

    public IActionResult OnPostDeleteCourse(string courseKey)
    {
        if (!string.IsNullOrEmpty(courseKey))
        {
            var path = Path.Combine(_env.ContentRootPath, "Resources", "Courses", "RU", $"{courseKey}.xml");
            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);
        }
        return RedirectToPage(new { deleted = true });
    }
}

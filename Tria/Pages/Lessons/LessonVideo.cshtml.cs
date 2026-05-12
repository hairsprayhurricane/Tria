using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Tria.Models;
using Tria.Services;

namespace Tria.Pages.Lessons;

[Authorize(Roles = "Admin,Student")]
public class LessonVideoModel : PageModel
{
    private readonly ILearningService _learning;

    public Lesson? Lesson { get; set; }
    public CourseModule? Module { get; set; }
    public Course? Course { get; set; }
    public LessonMaterial? Material { get; set; }
    public int MaterialIndex { get; set; }

    public LessonVideoModel(ILearningService learning)
    {
        _learning = learning;
    }

    public IActionResult OnGet(int lessonId, int materialIndex)
    {
        Lesson = _learning.GetLessonById(lessonId);
        if (Lesson == null) return NotFound();

        if (materialIndex < 0 || materialIndex >= Lesson.Materials.Count) return NotFound();

        Material = Lesson.Materials[materialIndex];
        if (Material.Type != "Video" || Material.YoutubeId == null) return NotFound();

        Module = _learning.GetModuleById(Lesson.ModuleId);
        if (Module == null) return NotFound();

        Course = _learning.GetCourseById(Module.CourseId);
        if (Course == null) return NotFound();

        MaterialIndex = materialIndex;
        return Page();
    }
}

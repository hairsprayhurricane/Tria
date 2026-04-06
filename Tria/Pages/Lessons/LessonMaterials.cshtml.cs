using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using Tria.Models;
using Tria.Services;

namespace Tria.Pages.Lessons;

[Authorize]
public class LessonMaterialsModel : PageModel
{
    private readonly ILearningService _learning;
    private readonly IProgressService _progress;

    public Lesson? Lesson { get; set; }
    public CourseModule? Module { get; set; }
    public Course? Course { get; set; }
    public UserLessonProgress? Progress { get; set; }

    public LessonMaterialsModel(ILearningService learning, IProgressService progress)
    {
        _learning = learning;
        _progress = progress;
    }

    public async Task<IActionResult> OnGetAsync(int lessonId)
    {
        Lesson = _learning.GetLessonById(lessonId);
        if (Lesson == null) return NotFound();

        Module = _learning.GetModuleById(Lesson.ModuleId);
        if (Module == null) return NotFound();

        Course = _learning.GetCourseById(Module.CourseId);
        if (Course == null) return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        Progress = await _progress.GetLessonProgressAsync(userId, lessonId);

        return Page();
    }

    public async Task<IActionResult> OnPostCompleteMaterialsAsync(int lessonId)
    {
        Lesson = _learning.GetLessonById(lessonId);
        if (Lesson == null) return NotFound();

        var module = _learning.GetModuleById(Lesson.ModuleId);
        if (module == null) return NotFound();

        var course = _learning.GetCourseById(module.CourseId);
        if (course == null) return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _progress.CompleteMaterialsAsync(userId, lessonId, module.Id, course.Id, Lesson.XpReward);

        return RedirectToPage("/Lessons/LessonMaterials", new { lessonId });
    }
}

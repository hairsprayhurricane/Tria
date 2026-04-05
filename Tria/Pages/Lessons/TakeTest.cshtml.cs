using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using Tria.Models;
using Tria.Services;

namespace Tria.Pages.Lessons;

[Authorize]
public class TakeTestModel : PageModel
{
    private readonly ILearningService _learning;
    private readonly IProgressService _progress;

    public Lesson? Lesson { get; set; }
    public CourseModule? Module { get; set; }
    public Course? Course { get; set; }
    public UserTestAttempt? LatestAttempt { get; set; }

    public TakeTestModel(ILearningService learning, IProgressService progress)
    {
        _learning = learning;
        _progress = progress;
    }

    public async Task<IActionResult> OnGetAsync(int lessonId)
    {
        Lesson = _learning.GetLessonById(lessonId);
        if (Lesson == null) return NotFound();

        if (Lesson.Test == null) return RedirectToPage("/Lessons/LessonDetail", new { lessonId });

        Module = _learning.GetModuleById(Lesson.ModuleId);
        Course = Module != null ? _learning.GetCourseById(Module.CourseId) : null;

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        LatestAttempt = await _progress.GetLatestAttemptAsync(userId, lessonId);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int lessonId)
    {
        Lesson = _learning.GetLessonById(lessonId);
        if (Lesson == null) return NotFound();

        if (Lesson.Test == null) return RedirectToPage("/Lessons/LessonDetail", new { lessonId });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var answers = new List<UserAnswer>();

        for (int i = 0; i < Lesson.Test.Questions.Count; i++)
        {
            var question = Lesson.Test.Questions[i];
            if (question.Type == "MultipleChoice")
            {
                var raw = Request.Form[$"q_{i}"].FirstOrDefault();
                int selected = int.TryParse(raw, out var idx) ? idx : -1;
                answers.Add(new UserAnswer
                {
                    QuestionIndex = i,
                    QuestionType = "MultipleChoice",
                    SelectedOptionIndex = selected
                });
            }
            else // ShortAnswer
            {
                var text = Request.Form[$"q_{i}_text"].FirstOrDefault() ?? "";
                answers.Add(new UserAnswer
                {
                    QuestionIndex = i,
                    QuestionType = "ShortAnswer",
                    TextAnswer = text
                });
            }
        }

        await _progress.SubmitTestAsync(userId, lessonId, answers, Lesson.Test);

        return RedirectToPage("/Lessons/LessonDetail", new { lessonId });
    }
}

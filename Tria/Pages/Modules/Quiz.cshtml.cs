using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using Tria.Models;
using Tria.Services;

namespace Tria.Pages.Modules
{
    [Authorize]
    public class ModulesQuizModel : PageModel
    {
        private readonly ILearningService _learningService;

        public ModulesQuizModel(ILearningService learningService)
        {
            _learningService = learningService;
        }

        public dynamic? Module { get; set; }
        public QuizContent? Quiz { get; set; }
        public bool ShowResult { get; set; }
        public int Score { get; set; }
        public bool Passed { get; set; }

        public async Task OnGetAsync(int moduleId)
        {
            Module = await _learningService.GetModuleByIdAsync(moduleId);
            
            if (Module?.ContentJson != null)
            {
                Quiz = JsonSerializer.Deserialize<QuizContent>(Module.ContentJson);
            }
        }

        public async Task<IActionResult> OnPostAsync(int moduleId)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
            
            Module = await _learningService.GetModuleByIdAsync(moduleId);
            
            if (Module?.ContentJson != null)
            {
                Quiz = JsonSerializer.Deserialize<QuizContent>(Module.ContentJson);
            }

            if (Request.Form.TryGetValue("resetQuiz", out _))
            {
                return RedirectToPage(new { moduleId });
            }

            int correctCount = 0;
            
            if (Quiz != null)
            {
                for (int i = 0; i < Quiz.Questions.Count; i++)
                {
                    if (Request.Form.TryGetValue($"answer_{i}", out var answer) && 
                        int.TryParse(answer, out int selectedIndex) &&
                        selectedIndex == Quiz.Questions[i].CorrectOptionIndex)
                    {
                        correctCount++;
                    }
                }
            }

            Score = Quiz!.Questions.Count > 0 ? (int)((double)correctCount / Quiz.Questions.Count * 100) : 0;
            Passed = Score >= Quiz.PassScore;

            await _learningService.CompleteModuleAsync(userId, moduleId, Score);

            ShowResult = true;

            return Page();
        }
    }
}

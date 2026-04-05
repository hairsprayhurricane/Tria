using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Tria.Models;
using Tria.Services;

namespace Tria.Pages.Modules;

[Authorize]
public class ModulesGameModel : PageModel
{
    private readonly ILearningService _learning;

    public CourseModule? Module { get; set; }
    public Course? Course { get; set; }
    public string GameBasePath { get; set; } = "";

    public ModulesGameModel(ILearningService learning)
    {
        _learning = learning;
    }

    public IActionResult OnGet(int moduleId)
    {
        Module = _learning.GetModuleById(moduleId);
        if (Module == null || !Module.HasGame || string.IsNullOrEmpty(Module.GameKey))
            return NotFound();

        Course = _learning.GetCourseById(Module.CourseId);
        GameBasePath = $"/Resources/GameContent/{Module.GameKey}";
        return Page();
    }
}

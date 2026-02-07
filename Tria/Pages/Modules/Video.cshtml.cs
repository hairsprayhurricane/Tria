using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using Tria.Models;
using Tria.Services;

namespace Tria.Pages.Modules
{
    [Authorize]
    public class ModulesVideoModel : PageModel
    {
        private readonly ILearningService _learningService;

        public ModulesVideoModel(ILearningService learningService)
        {
            _learningService = learningService;
        }

        public dynamic? Module { get; set; }
        public bool IsCompleted { get; set; }

        public async Task OnGetAsync(int moduleId)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
            
            Module = await _learningService.GetModuleByIdAsync(moduleId);
            
            var db = HttpContext.RequestServices
                .GetRequiredService<Tria.Data.ApplicationDbContext>();
            
            var progress = db.UserModuleProgress
                .FirstOrDefault(p => p.UserId == userId && p.ModuleId == moduleId);
            
            IsCompleted = progress?.IsCompleted ?? false;
        }

        public async Task<IActionResult> OnPostAsync(int moduleId)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
            
            await _learningService.CompleteModuleAsync(userId, moduleId);
            
            return RedirectToPage(new { moduleId });
        }
    }
}

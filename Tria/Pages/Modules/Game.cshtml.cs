using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Tria.Services;

namespace Tria.Pages.Modules
{
    [Authorize(AuthenticationSchemes = "Identity.Application,Guest")]
    public class ModulesGameModel : PageModel
    {
        private readonly ILearningService _learningService;

        public ModulesGameModel(ILearningService learningService)
        {
            _learningService = learningService;
        }

        public dynamic? Module { get; set; }
        public bool IsCompleted { get; set; }

        // Путь к папке с Unity WebGL-сборками относительно wwwroot.
        // Файлы игры ожидаются в: wwwroot/Resources/GameContent/{blockKey}_Game/
        public string GameBasePath { get; set; } = "";

        public async Task OnGetAsync(int moduleId)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";

            Module = await _learningService.GetModuleByIdAsync(moduleId);

            if (Module != null)
            {
                // Ключ блока используется для формирования пути к игре: {blockKey}_Game
                string blockKey = (string)(Module.Block?.Key ?? Module.BlockKey ?? "");
                GameBasePath = $"/Resources/GameContent/{blockKey}_Game";
            }

            var db = HttpContext.RequestServices
                .GetRequiredService<Tria.Data.ApplicationDbContext>();

            var progress = db.UserModuleProgress
                .FirstOrDefault(p => p.UserId == userId && p.ModuleId == moduleId);

            IsCompleted = progress?.IsCompleted ?? false;
        }
    }
}
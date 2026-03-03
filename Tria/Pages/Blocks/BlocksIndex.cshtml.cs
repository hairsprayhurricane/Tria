using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Tria.Services;

namespace Tria.Pages.Blocks
{
    [Authorize(AuthenticationSchemes = "Identity.Application,Guest")]
    public class BlocksIndexModel : PageModel
    {
        private readonly ILearningService _learningService;

        public BlocksIndexModel(ILearningService learningService)
        {
            _learningService = learningService;
        }

        public dynamic? Block { get; set; }
        public List<dynamic> Modules { get; set; } = new();
        public int Progress { get; set; }
        public List<int> CompletedModules { get; set; } = new();

        /// <summary>
        /// Есть ли у блока Unity-игра (берётся из HasGame в course.xml).
        /// </summary>
        public bool HasGame { get; set; }

        /// <summary>
        /// Путь к странице игры: /Modules/game/{blockKey}_Game.
        /// </summary>
        public string GameUrl { get; set; } = "";

        /// <summary>
        /// Разблокирована ли игра — все модули блока (Video и Quiz) пройдены.
        /// </summary>
        public bool GameUnlocked { get; set; }

        public async Task OnGetAsync(int blockId)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";

            Block = await _learningService.GetBlockByIdAsync(blockId);

            if (Block != null)
            {
                Modules = (await _learningService.GetModulesByBlockIdAsync(blockId))
                    .Cast<dynamic>()
                    .ToList();

                Progress = await _learningService.GetBlockProgressPercentAsync(userId, blockId);

                var userProgress = HttpContext.RequestServices
                    .GetRequiredService<Tria.Data.ApplicationDbContext>()
                    .UserModuleProgress
                    .Where(p => p.UserId == userId && p.IsCompleted)
                    .Select(p => p.ModuleId)
                    .ToList();

                CompletedModules = userProgress;

                HasGame = (bool)(Block.HasGame ?? false);

                if (HasGame)
                {
                    GameUrl = $"/Modules/game/{Block.Id}";

                    // Игра разблокирована только если все модули блока кроме Game пройдены
                    var requiredModuleIds = Modules
                        .Where(m => (string)m.Type != "Game")
                        .Select(m => (int)m.Id)
                        .ToList();

                    GameUnlocked = requiredModuleIds.Count > 0
                        && requiredModuleIds.All(id => CompletedModules.Contains(id));
                }
            }
        }
    }
}
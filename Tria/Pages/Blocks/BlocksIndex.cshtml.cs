using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Tria.Services;

namespace Tria.Pages.Blocks
{
    [Authorize]
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
            }
        }
    }
}

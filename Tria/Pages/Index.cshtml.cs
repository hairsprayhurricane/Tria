using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Tria.Services;

namespace Tria.Pages
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ILearningService _learningService;

        public IndexModel(ILearningService learningService)
        {
            _learningService = learningService;
        }

        public List<dynamic> Blocks { get; set; } = new();
        public int OverallProgress { get; set; }
        public Dictionary<int, int> BlockProgress { get; set; } = new();

        public async Task OnGetAsync()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
            
            var blocks = await _learningService.GetAllBlocksAsync();
            Blocks = blocks.Cast<dynamic>().ToList();
            
            OverallProgress = await _learningService.GetOverallProgressPercentAsync(userId);
            
            foreach (var block in blocks)
            {
                BlockProgress[block.Id] = await _learningService.GetBlockProgressPercentAsync(userId, block.Id);
            }
        }

        public IActionResult OnPost()
        {
            return SignOut("Identity.Application");
        }
    }
}

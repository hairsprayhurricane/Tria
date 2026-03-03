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

        public dynamic? Block { get; set; }
        public string GameBasePath { get; set; } = "";

        public async Task OnGetAsync(int blockId)
        {
            Block = await _learningService.GetBlockByIdAsync(blockId);

            if (Block != null)
            {
                string blockKey = (string)Block.Key;
                GameBasePath = $"/Resources/GameContent/{blockKey}_Game";
            }
        }
    }
}
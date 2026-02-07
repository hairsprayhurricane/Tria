using Tria.Models;

namespace Tria.Services
{
    public interface ILearningService
    {
        Task<List<LearningBlock>> GetAllBlocksAsync();
        Task<LearningBlock?> GetBlockByIdAsync(int blockId);
        Task<List<Module>> GetModulesByBlockIdAsync(int blockId);
        Task<Module?> GetModuleByIdAsync(int moduleId);
        Task CompleteModuleAsync(string userId, int moduleId, int? score = null);
        Task<int> GetOverallProgressPercentAsync(string userId);
        Task<int> GetBlockProgressPercentAsync(string userId, int blockId);
        Task<bool> IsCertifiedAsync(string userId);
    }
}

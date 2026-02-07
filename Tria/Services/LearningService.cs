using Microsoft.EntityFrameworkCore;
using Tria.Data;
using Tria.Models;

namespace Tria.Services
{
    public class LearningService : ILearningService
    {
        private readonly ApplicationDbContext _context;

        public LearningService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<LearningBlock>> GetAllBlocksAsync()
        {
            return await _context.LearningBlocks
                .Where(b => b.IsActive)
                .OrderBy(b => b.Order)
                .Include(b => b.Modules.Where(m => m.IsActive))
                .ToListAsync();
        }

        public async Task<LearningBlock?> GetBlockByIdAsync(int blockId)
        {
            return await _context.LearningBlocks
                .Where(b => b.IsActive && b.Id == blockId)
                .Include(b => b.Modules.Where(m => m.IsActive))
                .FirstOrDefaultAsync();
        }

        public async Task<List<Module>> GetModulesByBlockIdAsync(int blockId)
        {
            return await _context.Modules
                .Where(m => m.BlockId == blockId && m.IsActive)
                .OrderBy(m => m.Order)
                .ToListAsync();
        }

        public async Task<Module?> GetModuleByIdAsync(int moduleId)
        {
            return await _context.Modules
                .Where(m => m.Id == moduleId && m.IsActive)
                .FirstOrDefaultAsync();
        }

        public async Task CompleteModuleAsync(string userId, int moduleId, int? score = null)
        {
            var progress = await _context.UserModuleProgress
                .FirstOrDefaultAsync(p => p.UserId == userId && p.ModuleId == moduleId);

            if (progress == null)
            {
                progress = new UserModuleProgress
                {
                    UserId = userId,
                    ModuleId = moduleId,
                    IsCompleted = true,
                    Score = score,
                    Attempts = 1,
                    CompletedAt = DateTime.UtcNow
                };
                _context.UserModuleProgress.Add(progress);
            }
            else
            {
                progress.IsCompleted = true;
                progress.Score = score ?? progress.Score;
                progress.Attempts++;
                progress.CompletedAt = DateTime.UtcNow;
                _context.UserModuleProgress.Update(progress);
            }

            await _context.SaveChangesAsync();
        }

        public async Task<int> GetOverallProgressPercentAsync(string userId)
        {
            var totalModules = await _context.Modules
                .CountAsync(m => m.IsActive);

            if (totalModules == 0) return 0;

            var completedModules = await _context.UserModuleProgress
                .CountAsync(p => p.UserId == userId && p.IsCompleted);

            return (int)((double)completedModules / totalModules * 100);
        }

        public async Task<int> GetBlockProgressPercentAsync(string userId, int blockId)
        {
            var totalModules = await _context.Modules
                .CountAsync(m => m.BlockId == blockId && m.IsActive);

            if (totalModules == 0) return 0;

            var completedModules = await _context.UserModuleProgress
                .Include(p => p.Module)
                .CountAsync(p => p.UserId == userId && 
                                  p.IsCompleted && 
                                  p.Module.BlockId == blockId);

            return (int)((double)completedModules / totalModules * 100);
        }

        public async Task<bool> IsCertifiedAsync(string userId)
        {
            var progressPercent = await GetOverallProgressPercentAsync(userId);
            return progressPercent >= 80;
        }
    }
}

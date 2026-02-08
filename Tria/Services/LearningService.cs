using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Xml.Linq;
using Tria.Data;
using Tria.Models;

namespace Tria.Services;

public class LearningService : ILearningService
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;

    public LearningService(ApplicationDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    public Task<List<LearningBlock>> GetAllBlocksAsync()
        => Task.FromResult(LoadBlocks());

    public Task<LearningBlock?> GetBlockByIdAsync(int blockId)
        => Task.FromResult(LoadBlocks().FirstOrDefault(b => b.IsActive && b.Id == blockId));

    public Task<List<Module>> GetModulesByBlockIdAsync(int blockId)
        => Task.FromResult(LoadBlocks().FirstOrDefault(b => b.IsActive && b.Id == blockId)?.Modules
            .Where(m => m.IsActive).OrderBy(m => m.Order).ToList() ?? new List<Module>());

    public Task<Module?> GetModuleByIdAsync(int moduleId)
        => Task.FromResult(LoadBlocks().SelectMany(b => b.Modules).FirstOrDefault(m => m.IsActive && m.Id == moduleId));

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
        var totalModules = LoadBlocks().SelectMany(b => b.Modules).Count(m => m.IsActive);
        if (totalModules == 0) return 0;

        var completedModules = await _context.UserModuleProgress
            .CountAsync(p => p.UserId == userId && p.IsCompleted);

        return (int)((double)completedModules / totalModules * 100);
    }

    public async Task<int> GetBlockProgressPercentAsync(string userId, int blockId)
    {
        var block = LoadBlocks().FirstOrDefault(b => b.IsActive && b.Id == blockId);
        if (block == null) return 0;

        var moduleIds = block.Modules.Where(m => m.IsActive).Select(m => m.Id).ToList();
        if (moduleIds.Count == 0) return 0;

        var completedModules = await _context.UserModuleProgress.CountAsync(p =>
            p.UserId == userId && p.IsCompleted && moduleIds.Contains(p.ModuleId));

        return (int)((double)completedModules / moduleIds.Count * 100);
    }

    public async Task<bool> IsCertifiedAsync(string userId)
        => await GetOverallProgressPercentAsync(userId) >= 80;

    private sealed record BlockText(string? Title, string? Description);

    private List<LearningBlock> LoadBlocks()
    {
        var coursePath = Path.Combine(_env.ContentRootPath, "Content", "course.xml");

        var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        if (lang != "ru" && lang != "en") lang = "ru";

        var textsPath = Path.Combine(_env.ContentRootPath, "Content", $"course.{lang}.xml");

        var courseDoc = XDocument.Load(coursePath);
        var textsDoc = XDocument.Load(textsPath);

        var blockTexts = textsDoc.Root?
            .Element("Blocks")?
            .Elements("Block")
            .Select(x => new
            {
                Key = (string?)x.Attribute("Key") ?? "",
                Title = (string?)x.Attribute("Title"),
                Description = (string?)x.Attribute("Description")
            })
            .Where(x => x.Key.Length > 0)
            .ToDictionary(x => x.Key, x => new BlockText(x.Title, x.Description))
            ?? new Dictionary<string, BlockText>();

        var moduleTexts = textsDoc.Root?
            .Element("Modules")?
            .Elements("Module")
            .Select(x => new
            {
                Key = (string?)x.Attribute("Key") ?? "",
                Title = (string?)x.Attribute("Title")
            })
            .Where(x => x.Key.Length > 0)
            .ToDictionary(x => x.Key, x => x.Title)
            ?? new Dictionary<string, string?>();

        var blocks = new List<LearningBlock>();
        var blockId = 1;
        var moduleId = 1;

        var blockEls = courseDoc.Root?.Element("Blocks")?.Elements("Block") ?? Enumerable.Empty<XElement>();
        foreach (var b in blockEls)
        {
            var key = (string?)b.Attribute("Key") ?? "";
            var isActive = (bool?)b.Attribute("IsActive") ?? true;

            var block = new LearningBlock
            {
                Id = blockId++,
                Title = blockTexts.TryGetValue(key, out var bt) ? (bt.Title ?? key) : key,
                Description = blockTexts.TryGetValue(key, out var bt2) ? bt2.Description : null,
                Color = (string?)b.Attribute("Color") ?? "#000000",
                Order = (int?)b.Attribute("Order") ?? 0,
                IsActive = isActive,
                Modules = new List<Module>()
            };

            var moduleEls = b.Element("Modules")?.Elements("Module") ?? Enumerable.Empty<XElement>();
            foreach (var m in moduleEls)
            {
                var mKey = (string?)m.Attribute("Key") ?? "";
                var mIsActive = (bool?)m.Attribute("IsActive") ?? true;

                block.Modules.Add(new Module
                {
                    Id = moduleId++,
                    BlockId = block.Id,
                    Key = mKey,
                    Title = moduleTexts.TryGetValue(mKey, out var mt) && !string.IsNullOrWhiteSpace(mt) ? mt! : mKey,
                    Type = (string?)m.Attribute("Type") ?? "Video",
                    YoutubeId = (string?)m.Attribute("YoutubeId"),
                    Order = (int?)m.Attribute("Order") ?? 0,
                    IsActive = mIsActive,
                    CreatedAt = DateTime.UtcNow
                });
            }

            block.Modules = block.Modules.Where(x => x.IsActive).OrderBy(x => x.Order).ToList();

            if (block.IsActive)
                blocks.Add(block);
        }

        return blocks.OrderBy(x => x.Order).ToList();
    }

}

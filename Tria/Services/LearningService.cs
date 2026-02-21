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
        => Task.FromResult(
            LoadBlocks()
                .FirstOrDefault(b => b.IsActive && b.Id == blockId)?
                .Modules.Where(m => m.IsActive).OrderBy(m => m.Order).ToList()
            ?? new List<Module>());

    public Task<Module?> GetModuleByIdAsync(int moduleId)
        => Task.FromResult(
            LoadBlocks()
                .SelectMany(b => b.Modules)
                .FirstOrDefault(m => m.IsActive && m.Id == moduleId));

    public Task<QuizContent?> GetQuizByModuleIdAsync(int moduleId)
    {
        var module = LoadBlocks().SelectMany(b => b.Modules).FirstOrDefault(m => m.Id == moduleId && m.IsActive);
        if (module == null || module.Type != "Quiz") return Task.FromResult<QuizContent?>(null);

        // QuizContent XML module.Quiz
        return Task.FromResult(module.Quiz);
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

    private List<LearningBlock> LoadBlocks()
    {
        var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        if (lang.Length != 2) lang = "ru";

        var path = Path.Combine(_env.ContentRootPath, "Content", $"course.{lang}.xml");

        // fallback
        if (!File.Exists(path))
            path = Path.Combine(_env.ContentRootPath, "Content", "course.ru.xml");

        var doc = XDocument.Load(path);

        var blocks = new List<LearningBlock>();
        var blockEls = doc.Root?.Element("Blocks")?.Elements("Block") ?? Enumerable.Empty<XElement>();

        foreach (var b in blockEls)
        {
            var block = new LearningBlock
            {
                Id = (int?)b.Attribute("Id") ?? 0,
                Key = (string?)b.Attribute("Key") ?? "",
                Title = (string?)b.Attribute("Title") ?? "",
                Description = (string?)b.Attribute("Description"),
                Color = (string?)b.Attribute("Color") ?? "#000000",
                Order = (int?)b.Attribute("Order") ?? 0,
                IsActive = (bool?)b.Attribute("IsActive") ?? true,
                Modules = new List<Module>()
            };

            var moduleEls = b.Element("Modules")?.Elements("Module") ?? Enumerable.Empty<XElement>();
            foreach (var m in moduleEls)
            {
                var module = new Module
                {
                    Id = (int?)m.Attribute("Id") ?? 0,
                    Key = (string?)m.Attribute("Key") ?? "",
                    BlockId = block.Id,
                    Title = (string?)m.Attribute("Title") ?? "",
                    Type = (string?)m.Attribute("Type") ?? "Video",
                    YoutubeId = (string?)m.Attribute("YoutubeId"),
                    Order = (int?)m.Attribute("Order") ?? 0,
                    IsActive = (bool?)m.Attribute("IsActive") ?? true,
                    CreatedAt = DateTime.UtcNow
                };

                if (module.Type == "Quiz")
                {
                    module.Quiz = ParseQuiz(m.Element("Quiz"), module.Title);
                }

                block.Modules.Add(module);
            }

            block.Modules = block.Modules.Where(x => x.IsActive).OrderBy(x => x.Order).ToList();

            if (block.IsActive)
                blocks.Add(block);
        }

        return blocks.OrderBy(x => x.Order).ToList();
    }

    private static QuizContent? ParseQuiz(XElement? quizEl, string moduleTitle)
    {
        if (quizEl == null) return null;

        var quiz = new QuizContent
        {
            Title = moduleTitle,
            Description = (string?)quizEl.Attribute("Description") ?? "",
            PassScore = (int?)quizEl.Attribute("PassScore") ?? 80,
            Questions = new List<QuizQuestion>()
        };

        foreach (var q in quizEl.Elements("Question"))
        {
            var question = new QuizQuestion
            {
                Text = (string?)q.Element("Text") ?? "",
                Options = q.Elements("Option").Select(x => (string?)x ?? "").ToList(),
                CorrectOptionIndex = (int?)q.Element("CorrectOptionIndex") ?? 0
            };

            if (!string.IsNullOrWhiteSpace(question.Text) && question.Options.Count > 0)
                quiz.Questions.Add(question);
        }

        return quiz.Questions.Count == 0 ? null : quiz;
    }
}

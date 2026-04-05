using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using Tria.Models;
using Tria.Services;

namespace Tria.Pages;

public record Achievement(string Title, string Description, string Tier, bool Earned, string Icon);

[Authorize]
public class AchievementsModel : PageModel
{
    private readonly ILearningService _learning;
    private readonly IProgressService _progress;

    public List<Achievement> Achievements { get; set; } = new();
    public int TotalXp { get; set; }

    public AchievementsModel(ILearningService learning, IProgressService progress)
    {
        _learning = learning;
        _progress = progress;
    }

    public async Task OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        TotalXp = await _progress.GetTotalXpAsync(userId);

        var allProgress = await _progress.GetAllProgressAsync(userId);
        var courses = _learning.GetAllCourses();

        bool anyCompleted = allProgress.Any(p => p.MaterialsCompleted);

        // Global: first lesson completed
        Achievements.Add(new Achievement(
            "Первый урок завершён",
            "Завершите хотя бы один урок",
            "bronze",
            anyCompleted,
            "🥉"
        ));

        // Global: XP hunter
        Achievements.Add(new Achievement(
            "XP-охотник",
            "Наберите 100 XP",
            "silver",
            TotalXp >= 100,
            "🥈"
        ));

        // Per-course achievements
        foreach (var course in courses)
        {
            var allLessons = course.Modules.SelectMany(m => m.Lessons).ToList();
            if (!allLessons.Any()) continue;

            int completedCount = allLessons.Count(l =>
            {
                var lp = allProgress.FirstOrDefault(p => p.LessonId == l.Id);
                return lp?.MaterialsCompleted == true && lp.TestPassed;
            });

            int pct = (int)((double)completedCount / allLessons.Count * 100);

            Achievements.Add(new Achievement(
                $"Первые шаги: {course.Title}",
                $"Завершите 33% уроков курса \"{course.Title}\"",
                "bronze",
                pct >= 33,
                "🥉"
            ));

            Achievements.Add(new Achievement(
                $"На полпути: {course.Title}",
                $"Завершите 66% уроков курса \"{course.Title}\"",
                "silver",
                pct >= 66,
                "🥈"
            ));

            Achievements.Add(new Achievement(
                $"Курс завершён: {course.Title}",
                $"Завершите все уроки курса \"{course.Title}\"",
                "gold",
                pct >= 100,
                "🥇"
            ));
        }
    }
}

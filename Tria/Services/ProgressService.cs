using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Tria.Data;
using Tria.Models;

namespace Tria.Services;

public class ProgressService : IProgressService
{
    private readonly ApplicationDbContext _db;

    public ProgressService(ApplicationDbContext db)
    {
        _db = db;
    }

    // ── Lesson progress ───────────────────────────────────────────────────────

    public Task<UserLessonProgress?> GetLessonProgressAsync(string userId, int lessonId)
        => _db.UserLessonProgress
              .FirstOrDefaultAsync(p => p.UserId == userId && p.LessonId == lessonId);

    public Task<List<UserLessonProgress>> GetAllProgressAsync(string userId)
        => _db.UserLessonProgress.Where(p => p.UserId == userId).ToListAsync();

    public async Task CompleteMaterialsAsync(string userId, int lessonId, int moduleId, int courseId, int xp)
    {
        var progress = await _db.UserLessonProgress
            .FirstOrDefaultAsync(p => p.UserId == userId && p.LessonId == lessonId);

        if (progress == null)
        {
            _db.UserLessonProgress.Add(new UserLessonProgress
            {
                UserId               = userId,
                LessonId             = lessonId,
                ModuleId             = moduleId,
                CourseId             = courseId,
                MaterialsCompleted   = true,
                XpEarned             = xp,
                MaterialsCompletedAt = DateTime.UtcNow,
            });
        }
        else if (!progress.MaterialsCompleted)
        {
            progress.MaterialsCompleted   = true;
            progress.XpEarned            += xp;
            progress.MaterialsCompletedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    // ── Test attempts ─────────────────────────────────────────────────────────

    public Task<List<UserTestAttempt>> GetTestAttemptsAsync(string userId, int lessonId)
        => _db.UserTestAttempts
              .Where(a => a.UserId == userId && a.LessonId == lessonId)
              .OrderByDescending(a => a.StartedAt)
              .ToListAsync();

    public Task<UserTestAttempt?> GetLatestAttemptAsync(string userId, int lessonId)
        => _db.UserTestAttempts
              .Where(a => a.UserId == userId && a.LessonId == lessonId)
              .OrderByDescending(a => a.StartedAt)
              .FirstOrDefaultAsync();

    public async Task<UserTestAttempt> SubmitTestAsync(
        string userId, int lessonId, List<UserAnswer> answers, LessonTest test)
    {
        // Grade each answer
        bool hasShortAnswer = false;
        int score = 0;
        int maxScore = test.Questions.Count;

        for (int i = 0; i < answers.Count && i < test.Questions.Count; i++)
        {
            var q = test.Questions[i];
            var a = answers[i];
            a.QuestionIndex = i;
            a.QuestionType  = q.Type;

            if (q.Type == "MultipleChoice")
            {
                a.IsCorrect    = a.SelectedOptionIndex == q.CorrectOptionIndex;
                a.PointsEarned = a.IsCorrect ? 1 : 0;
                score         += a.PointsEarned;
            }
            else // ShortAnswer
            {
                // TODO: grade via AI model in a future update; default to incorrect.
                a.IsCorrect    = false;
                a.PointsEarned = 0;
                hasShortAnswer = true;
            }
        }

        var attemptCount = await _db.UserTestAttempts
            .CountAsync(a => a.UserId == userId && a.LessonId == lessonId);

        int scorePercent = maxScore > 0 ? (int)((double)score / maxScore * 100) : 0;

        var status = hasShortAnswer
            ? TestStatus.Checking
            : (scorePercent >= test.PassScore ? TestStatus.Passed : TestStatus.Failed);

        var attempt = new UserTestAttempt
        {
            UserId        = userId,
            LessonId      = lessonId,
            AttemptNumber = attemptCount + 1,
            Status        = status,
            Score         = score,
            MaxScore      = maxScore,
            AnswersJson   = JsonSerializer.Serialize(answers),
            StartedAt     = DateTime.UtcNow,
            CompletedAt   = DateTime.UtcNow,
        };

        _db.UserTestAttempts.Add(attempt);

        // Update lesson progress: mark test as passed if applicable
        if (status == TestStatus.Passed)
        {
            var lessonProgress = await _db.UserLessonProgress
                .FirstOrDefaultAsync(p => p.UserId == userId && p.LessonId == lessonId);

            if (lessonProgress != null)
                lessonProgress.TestPassed = true;
        }

        await _db.SaveChangesAsync();
        return attempt;
    }

    // ── Aggregate progress ────────────────────────────────────────────────────

    public async Task<int> GetCourseProgressPercentAsync(string userId, int courseId, List<Lesson> allLessons)
    {
        if (allLessons.Count == 0) return 0;

        var lessonIds = allLessons.Select(l => l.Id).ToList();
        var completed = await _db.UserLessonProgress
            .CountAsync(p => p.UserId == userId && p.CourseId == courseId
                             && p.MaterialsCompleted && p.TestPassed
                             && lessonIds.Contains(p.LessonId));

        return (int)((double)completed / allLessons.Count * 100);
    }

    public async Task<int> GetModuleProgressPercentAsync(string userId, int moduleId, List<Lesson> moduleLessons)
    {
        if (moduleLessons.Count == 0) return 0;

        var lessonIds = moduleLessons.Select(l => l.Id).ToList();
        var completed = await _db.UserLessonProgress
            .CountAsync(p => p.UserId == userId && p.ModuleId == moduleId
                             && p.MaterialsCompleted && p.TestPassed
                             && lessonIds.Contains(p.LessonId));

        return (int)((double)completed / moduleLessons.Count * 100);
    }

    // ── XP ────────────────────────────────────────────────────────────────────

    public async Task<int> GetTotalXpAsync(string userId)
        => await _db.UserLessonProgress
                    .Where(p => p.UserId == userId)
                    .SumAsync(p => p.XpEarned);
}

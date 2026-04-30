using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Tria.Data;
using Tria.Models;
using Tria.Options;

namespace Tria.Services;

public class AiGradingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOllamaGradingService _ollama;
    private readonly ILogger<AiGradingBackgroundService> _logger;
    private readonly OllamaOptions _opts;

    public AiGradingBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOllamaGradingService ollama,
        ILogger<AiGradingBackgroundService> logger,
        IOptions<OllamaOptions> opts)
    {
        _scopeFactory = scopeFactory;
        _ollama = ollama;
        _logger = logger;
        _opts = opts.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingAttemptsAsync(stoppingToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in AI grading service");
            }

            await Task.Delay(TimeSpan.FromSeconds(_opts.GradingIntervalSeconds), stoppingToken)
                      .ContinueWith(_ => { }, CancellationToken.None); // don't throw on cancel
        }
    }

    private async Task ProcessPendingAttemptsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var learning = scope.ServiceProvider.GetRequiredService<ILearningService>();

        var pending = await db.UserTestAttempts
            .Where(a => a.Status == TestStatus.Checking)
            .OrderBy(a => a.StartedAt)
            .ToListAsync(ct);

        foreach (var attempt in pending)
        {
            if (ct.IsCancellationRequested) break;
            await GradeAttemptAsync(attempt, db, learning, ct);
        }
    }

    private async Task GradeAttemptAsync(
        UserTestAttempt attempt,
        ApplicationDbContext db,
        ILearningService learning,
        CancellationToken ct)
    {
        var lesson = learning.GetLessonById(attempt.LessonId);
        if (lesson?.Test == null)
        {
            // Lesson was removed from XML — can't grade, just fail it
            attempt.Status = TestStatus.Failed;
            await db.SaveChangesAsync(ct);
            return;
        }

        var answers = attempt.GetAnswers();
        bool anyUpdated = false;

        foreach (var answer in answers)
        {
            if (ct.IsCancellationRequested) break;
            if (answer.QuestionType != "ShortAnswer" || answer.AiCheckedAt.HasValue)
                continue;

            var question = lesson.Test.Questions.ElementAtOrDefault(answer.QuestionIndex);
            if (question == null) continue;

            try
            {
                _logger.LogInformation(
                    "Grading attempt {Id} question {Index} for user {User}",
                    attempt.Id, answer.QuestionIndex, attempt.UserId);

                var (isCorrect, comment) = await _ollama.GradeAnswerAsync(
                    question.Text, answer.TextAnswer ?? "");

                answer.IsCorrect     = isCorrect;
                answer.PointsEarned  = isCorrect ? 1 : 0;
                answer.AiComment     = comment;
                answer.AiCheckedAt   = DateTime.UtcNow;
                anyUpdated = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Could not grade attempt {Id} question {Index} — will retry next tick",
                    attempt.Id, answer.QuestionIndex);
            }
        }

        if (!anyUpdated) return;

        // Finalize if all ShortAnswer questions are now checked
        bool allChecked = answers
            .Where(a => a.QuestionType == "ShortAnswer")
            .All(a => a.AiCheckedAt.HasValue);

        if (allChecked)
        {
            var score        = answers.Sum(a => a.PointsEarned);
            var scorePercent = attempt.MaxScore > 0
                ? (int)((double)score / attempt.MaxScore * 100) : 0;

            attempt.Score       = score;
            attempt.Status      = scorePercent >= lesson.Test.PassScore
                ? TestStatus.Passed : TestStatus.Failed;
            attempt.CompletedAt = DateTime.UtcNow;

            if (attempt.Status == TestStatus.Passed)
            {
                var progress = await db.UserLessonProgress
                    .FirstOrDefaultAsync(p =>
                        p.UserId == attempt.UserId && p.LessonId == attempt.LessonId, ct);
                if (progress != null)
                    progress.TestPassed = true;
            }
        }

        attempt.AnswersJson = JsonSerializer.Serialize(answers);
        await db.SaveChangesAsync(ct);
    }
}

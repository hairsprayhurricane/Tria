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
    private readonly SentinelLogger _sentinel;

    public AiGradingBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOllamaGradingService ollama,
        ILogger<AiGradingBackgroundService> logger,
        IOptions<OllamaOptions> opts,
        SentinelLogger sentinel)
    {
        _scopeFactory = scopeFactory;
        _ollama = ollama;
        _logger = logger;
        _opts = opts.Value;
        _sentinel = sentinel;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _sentinel.Log("Фоновый сервис проверки запущен");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingAttemptsAsync(stoppingToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _sentinel.LogError("ProcessPendingAttempts", ex);
                _logger.LogError(ex, "Unhandled error in AI grading service");
            }

            await Task.Delay(TimeSpan.FromSeconds(_opts.GradingIntervalSeconds), stoppingToken)
                      .ContinueWith(_ => { }, CancellationToken.None); // don't throw on cancel
        }
    }

    private async Task ProcessPendingAttemptsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db           = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var learning     = scope.ServiceProvider.GetRequiredService<ILearningService>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var pending = await db.UserTestAttempts
            .Where(a => a.Status == TestStatus.Checking)
            .OrderBy(a => a.StartedAt)
            .ToListAsync(ct);

        if (pending.Count > 0)
            _sentinel.Log($"Найдено попыток для проверки: {pending.Count}");

        foreach (var attempt in pending)
        {
            if (ct.IsCancellationRequested) break;
            await GradeAttemptAsync(attempt, db, learning, notifications, ct);
        }
    }

    private async Task GradeAttemptAsync(
        UserTestAttempt attempt,
        ApplicationDbContext db,
        ILearningService learning,
        INotificationService notifications,
        CancellationToken ct)
    {
        var lesson = learning.GetLessonById(attempt.LessonId);
        if (lesson?.Test == null)
        {
            _sentinel.Log($"Попытка #{attempt.Id} — урок {attempt.LessonId} не найден в XML, помечаю как Failed");
            attempt.Status = TestStatus.Failed;
            await db.SaveChangesAsync(ct);
            return;
        }

        _sentinel.Log($"--- Получен ответ на проверку | Попытка #{attempt.Id} | Урок: {lesson.Title} | Пользователь: {attempt.UserId}");

        var answers = attempt.GetAnswers();
        bool anyUpdated = false;

        foreach (var answer in answers)
        {
            if (ct.IsCancellationRequested) break;
            if (answer.QuestionType != "ShortAnswer" || answer.AiCheckedAt.HasValue)
                continue;

            var question = lesson.Test.Questions.ElementAtOrDefault(answer.QuestionIndex);
            if (question == null) continue;

            _sentinel.Log($"Идёт оценка вопроса #{answer.QuestionIndex + 1}: {question.Text}");

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
                _sentinel.LogError($"Оценка вопроса #{answer.QuestionIndex + 1}", ex);
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

            var lessonTitle = lesson.Title;
            var passed      = attempt.Status == TestStatus.Passed;
            var notifMsg    = passed
                ? $"ИИ-ассистент проверил ваши текстовые ответы в уроке «{lessonTitle}». Тест пройден ✓"
                : $"ИИ-ассистент проверил ваши текстовые ответы в уроке «{lessonTitle}». Тест не пройден.";

            _sentinel.Log($"Проверка завершена | Попытка #{attempt.Id} | Результат: {(passed ? "ПРОЙДЕН" : "НЕ ПРОЙДЕН")} | Счёт: {attempt.Score}/{attempt.MaxScore}");

            try
            {
                await notifications.CreateAsync(
                    attempt.UserId,
                    "Проверка ответа завершена",
                    notifMsg,
                    $"/Lesson/{attempt.LessonId}/Test");
            }
            catch (Exception ex)
            {
                _sentinel.LogError("Создание уведомления", ex);
                _logger.LogWarning(ex, "Failed to create notification for attempt {Id}", attempt.Id);
            }
        }

        attempt.AnswersJson = JsonSerializer.Serialize(answers);
        await db.SaveChangesAsync(ct);
    }
}

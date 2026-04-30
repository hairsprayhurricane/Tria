using System.Text.Json;

namespace Tria.Models;

public enum TestStatus { Checking, Passed, Failed }

public class UserTestAttempt
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public int LessonId { get; set; }
    public int AttemptNumber { get; set; }
    public TestStatus Status { get; set; } = TestStatus.Checking;

    /// <summary>Points earned out of MaxScore.</summary>
    public int Score { get; set; } = 0;

    /// <summary>Maximum possible points for this attempt.</summary>
    public int MaxScore { get; set; } = 0;

    /// <summary>Percentage 0-100.</summary>
    public int ScorePercent => MaxScore > 0 ? (int)((double)Score / MaxScore * 100) : 0;

    /// <summary>JSON-serialized List&lt;UserAnswer&gt;.</summary>
    public string? AnswersJson { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public List<UserAnswer> GetAnswers()
        => string.IsNullOrEmpty(AnswersJson)
            ? new()
            : JsonSerializer.Deserialize<List<UserAnswer>>(AnswersJson) ?? new();
}

public class UserAnswer
{
    public int QuestionIndex { get; set; }

    /// <summary>MultipleChoice | ShortAnswer</summary>
    public string QuestionType { get; set; } = "";

    /// <summary>Selected option index for MultipleChoice.</summary>
    public int? SelectedOptionIndex { get; set; }

    /// <summary>Text entered for ShortAnswer.</summary>
    public string? TextAnswer { get; set; }

    public bool IsCorrect { get; set; }
    public int PointsEarned { get; set; }

    // AI grading (ShortAnswer only)
    public string? AiComment { get; set; }
    public DateTime? AiCheckedAt { get; set; }
}

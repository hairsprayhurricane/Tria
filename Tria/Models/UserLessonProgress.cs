namespace Tria.Models;

public class UserLessonProgress
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public int LessonId { get; set; }
    public int ModuleId { get; set; }
    public int CourseId { get; set; }

    /// <summary>True when user has clicked "Материалы пройдены".</summary>
    public bool MaterialsCompleted { get; set; } = false;

    /// <summary>XP awarded for completing materials (based on lesson difficulty).</summary>
    public int XpEarned { get; set; } = 0;

    /// <summary>True when test is passed (score >= passScore). False if failed or not taken.</summary>
    public bool TestPassed { get; set; } = false;

    /// <summary>True when both materials are completed and test is passed.</summary>
    public bool IsCompleted => MaterialsCompleted && TestPassed;

    public DateTime? MaterialsCompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

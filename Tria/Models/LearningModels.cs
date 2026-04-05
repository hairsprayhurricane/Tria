namespace Tria.Models;

public enum DifficultyLevel { Easy, Medium, Hard }

public class Course
{
    public int Id { get; set; }
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public List<CourseModule> Modules { get; set; } = new();
}

public class CourseModule
{
    public int Id { get; set; }
    public string Key { get; set; } = "";
    public int CourseId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Easy;
    public int Order { get; set; }
    public bool IsActive { get; set; } = true;
    public bool HasGame { get; set; } = false;
    public string? GameKey { get; set; }
    public List<Lesson> Lessons { get; set; } = new();
}

public class Lesson
{
    public int Id { get; set; }
    public string Key { get; set; } = "";
    public int ModuleId { get; set; }
    public string Title { get; set; } = "";
    public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Easy;
    public int Order { get; set; }
    public bool IsActive { get; set; } = true;
    public List<LessonMaterial> Materials { get; set; } = new();
    public LessonTest? Test { get; set; }

    // TODO: In a future update, lessons can be locked until prerequisite lessons are completed.
    // This will be controlled by a PrerequisiteLessonIds field and enforced in ProgressService.

    public int XpReward => Difficulty switch
    {
        DifficultyLevel.Easy   => 10,
        DifficultyLevel.Medium => 25,
        DifficultyLevel.Hard   => 45,
        _                      => 10
    };
}

public class LessonMaterial
{
    /// <summary>Video, PDF, Image</summary>
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string? YoutubeId { get; set; }   // for Type=Video
    public string? FilePath { get; set; }    // for Type=PDF or Image (path under wwwroot)
}

public class LessonTest
{
    /// <summary>Minimum percentage (0-100) required to pass.</summary>
    public int PassScore { get; set; } = 80;
    public List<TestQuestion> Questions { get; set; } = new();
}

public class TestQuestion
{
    /// <summary>MultipleChoice | ShortAnswer</summary>
    public string Type { get; set; } = "MultipleChoice";
    public string Text { get; set; } = "";

    // MultipleChoice only
    public List<string> Options { get; set; } = new();
    public int CorrectOptionIndex { get; set; }

    // TODO: ShortAnswer questions will be graded by an AI model in a future update.
    // Currently they are auto-marked as incorrect (0 points).
}

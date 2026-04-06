using System.Globalization;
using System.Xml.Linq;
using Tria.Models;

namespace Tria.Services;

public class LearningService : ILearningService
{
    private readonly IWebHostEnvironment _env;

    // Cache per language key so XML is parsed once per request lifecycle.
    private readonly Dictionary<string, List<Course>> _cache = new();

    public LearningService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public List<Course> GetAllCourses() => Load();

    public Course? GetCourseById(int courseId)
        => Load().FirstOrDefault(c => c.IsActive && c.Id == courseId);

    public CourseModule? GetModuleById(int moduleId)
        => Load()
            .SelectMany(c => c.Modules)
            .FirstOrDefault(m => m.IsActive && m.Id == moduleId);

    public Lesson? GetLessonById(int lessonId)
        => Load()
            .SelectMany(c => c.Modules)
            .SelectMany(m => m.Lessons)
            .FirstOrDefault(l => l.IsActive && l.Id == lessonId);

    public List<Lesson> GetLessonsByModuleId(int moduleId)
    {
        var module = GetModuleById(moduleId);
        return module?.Lessons.Where(l => l.IsActive).OrderBy(l => l.Order).ToList()
               ?? new List<Lesson>();
    }

    // ── XML parsing ──────────────────────────────────────────────────────────

    private List<Course> Load()
    {
        var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        if (lang.Length != 2) lang = "ru";

        if (_cache.TryGetValue(lang, out var cached)) return cached;

        var path = Path.Combine(_env.ContentRootPath, "Resources/Content", $"course.{lang}.xml");
        if (!File.Exists(path))
            path = Path.Combine(_env.ContentRootPath, "Resources/Content", "course.ru.xml");

        var doc = XDocument.Load(path);
        var courses = new List<Course>();

        foreach (var ce in doc.Root?.Elements("Course") ?? Enumerable.Empty<XElement>())
        {
            var course = new Course
            {
                Id          = (int?)ce.Attribute("Id") ?? 0,
                Key         = (string?)ce.Attribute("Key") ?? "",
                Title       = (string?)ce.Attribute("Title") ?? "",
                Description = (string?)ce.Attribute("Description") ?? "",
                IsActive    = (bool?)ce.Attribute("IsActive") ?? true,
                Color       = (string?)ce.Attribute("Color"),
            };

            foreach (var me in ce.Element("Modules")?.Elements("Module") ?? Enumerable.Empty<XElement>())
            {
                var module = new CourseModule
                {
                    Id          = (int?)me.Attribute("Id") ?? 0,
                    Key         = (string?)me.Attribute("Key") ?? "",
                    CourseId    = course.Id,
                    Title       = (string?)me.Attribute("Title") ?? "",
                    Description = (string?)me.Attribute("Description") ?? "",
                    Difficulty  = ParseDifficulty((string?)me.Attribute("Difficulty")),
                    Order       = (int?)me.Attribute("Order") ?? 0,
                    IsActive    = (bool?)me.Attribute("IsActive") ?? true,
                    HasGame     = (bool?)me.Attribute("HasGame") ?? false,
                    GameKey     = (string?)me.Attribute("GameKey"),
                };

                foreach (var le in me.Element("Lessons")?.Elements("Lesson") ?? Enumerable.Empty<XElement>())
                {
                    var lesson = new Lesson
                    {
                        Id         = (int?)le.Attribute("Id") ?? 0,
                        Key        = (string?)le.Attribute("Key") ?? "",
                        ModuleId   = module.Id,
                        Title      = (string?)le.Attribute("Title") ?? "",
                        Difficulty = ParseDifficulty((string?)le.Attribute("Difficulty")),
                        Order      = (int?)le.Attribute("Order") ?? 0,
                        IsActive   = (bool?)le.Attribute("IsActive") ?? true,
                    };

                    foreach (var mat in le.Element("Materials")?.Elements("Material") ?? Enumerable.Empty<XElement>())
                    {
                        lesson.Materials.Add(new LessonMaterial
                        {
                            Type      = (string?)mat.Attribute("Type") ?? "",
                            Title     = (string?)mat.Attribute("Title") ?? "",
                            YoutubeId = (string?)mat.Attribute("YoutubeId"),
                            FilePath  = (string?)mat.Attribute("FilePath"),
                        });
                    }

                    lesson.Test = ParseTest(le.Element("Test"));

                    if (lesson.IsActive)
                        module.Lessons.Add(lesson);
                }

                module.Lessons = module.Lessons.OrderBy(l => l.Order).ToList();

                if (module.IsActive)
                    course.Modules.Add(module);
            }

            course.Modules = course.Modules.OrderBy(m => m.Order).ToList();

            if (course.IsActive)
                courses.Add(course);
        }

        _cache[lang] = courses;
        return courses;
    }

    private static LessonTest? ParseTest(XElement? testEl)
    {
        if (testEl == null) return null;

        var test = new LessonTest
        {
            PassScore = (int?)testEl.Attribute("PassScore") ?? 80,
        };

        foreach (var qe in testEl.Elements("Question"))
        {
            var q = new TestQuestion
            {
                Type = (string?)qe.Attribute("Type") ?? "MultipleChoice",
                Text = (string?)qe.Element("Text") ?? "",
                CorrectOptionIndex = (int?)qe.Element("CorrectOptionIndex") ?? 0,
            };

            foreach (var opt in qe.Element("Options")?.Elements("Option") ?? Enumerable.Empty<XElement>())
                q.Options.Add((string?)opt ?? "");

            if (!string.IsNullOrWhiteSpace(q.Text))
                test.Questions.Add(q);
        }

        return test.Questions.Count > 0 ? test : null;
    }

    private static DifficultyLevel ParseDifficulty(string? value) => value?.ToLower() switch
    {
        "medium" => DifficultyLevel.Medium,
        "hard"   => DifficultyLevel.Hard,
        _        => DifficultyLevel.Easy,
    };
}

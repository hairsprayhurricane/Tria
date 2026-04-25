using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Tria.Pages.Teacher;

[Authorize(Roles = "Teacher")]
public class CourseEditorModel : PageModel
{
    private readonly IWebHostEnvironment _env;

    public bool IsNewCourse { get; set; } = true;
    public string InitialJson { get; set; } = "null";
    public string? ResultMessage { get; set; }
    public bool ResultSuccess { get; set; }

    public CourseEditorModel(IWebHostEnvironment env)
    {
        _env = env;
    }

    public void OnGet(string? courseKey, bool? saved)
    {
        IsNewCourse = string.IsNullOrEmpty(courseKey);
        if (!IsNewCourse)
        {
            var file = CoursePath(courseKey!);
            if (System.IO.File.Exists(file))
                InitialJson = LoadCourseJson(file);
            else
                IsNewCourse = true;
        }
        if (saved == true)
            ResultMessage = "Курс успешно сохранён!";
        ResultSuccess = saved == true;
    }

    public IActionResult OnPostDelete(string courseKey)
    {
        if (!string.IsNullOrEmpty(courseKey))
        {
            var path = CoursePath(courseKey);
            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);
        }
        return RedirectToPage("/Teacher/Index", new { deleted = true });
    }

    public async Task<IActionResult> OnPostAsync([FromForm] string courseJson)
    {
        await Task.CompletedTask;
        var ruPath = RuPath();

        CourseDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<CourseDto>(courseJson, JsonOpts());
        }
        catch (Exception ex)
        {
            ResultMessage = "Ошибка парсинга данных: " + ex.Message;
            ResultSuccess = false;
            InitialJson = courseJson;
            IsNewCourse = true;
            return Page();
        }

        if (dto == null || string.IsNullOrWhiteSpace(dto.Title))
        {
            ResultMessage = "Название курса не может быть пустым.";
            ResultSuccess = false;
            InitialJson = courseJson;
            IsNewCourse = string.IsNullOrEmpty(dto?.OriginalKey);
            return Page();
        }

        // Auto key
        if (string.IsNullOrWhiteSpace(dto.Key))
            dto.Key = "course_temp";

        dto.Key = SanitizeKey(dto.Key);

        // Assign IDs
        int nextId = GetNextId(ruPath);
        if (dto.Id == 0) dto.Id = nextId++;
        foreach (var m in dto.Modules)
        {
            if (m.Id == 0) m.Id = nextId++;
            m.Key = string.IsNullOrWhiteSpace(m.Key) ? $"module_{m.Id}" : SanitizeKey(m.Key);
            foreach (var l in m.Lessons)
            {
                if (l.Id == 0) l.Id = nextId++;
                l.Key = string.IsNullOrWhiteSpace(l.Key) ? $"lesson_{l.Id}" : SanitizeKey(l.Key);
            }
        }

        // If key was "course_temp", use ID-based key
        if (dto.Key == "course_temp")
            dto.Key = $"course_{dto.Id}";

        var targetPath = CoursePath(dto.Key);
        var originalPath = !string.IsNullOrEmpty(dto.OriginalKey) ? CoursePath(dto.OriginalKey) : null;

        // Duplicate check for new courses
        if (string.IsNullOrEmpty(dto.OriginalKey) && System.IO.File.Exists(targetPath))
        {
            ResultMessage = $"Курс с ключом «{dto.Key}» уже существует. Измените ключ.";
            ResultSuccess = false;
            InitialJson = JsonSerializer.Serialize(dto, JsonOpts());
            IsNewCourse = true;
            return Page();
        }

        // Write XML
        try
        {
            BuildXml(dto).Save(targetPath);

            // Rename: delete old file if key changed
            if (originalPath != null && originalPath != targetPath && System.IO.File.Exists(originalPath))
                System.IO.File.Delete(originalPath);
        }
        catch (Exception ex)
        {
            ResultMessage = "Ошибка записи файла: " + ex.Message;
            ResultSuccess = false;
            InitialJson = JsonSerializer.Serialize(dto, JsonOpts());
            IsNewCourse = string.IsNullOrEmpty(dto.OriginalKey);
            return Page();
        }

        return RedirectToPage(new { courseKey = dto.Key, saved = true });
    }

    // ── Paths ─────────────────────────────────────────────────────────────────

    private string RuPath() =>
        Path.Combine(_env.ContentRootPath, "Resources", "Courses", "RU");

    private string CoursePath(string key) =>
        Path.Combine(RuPath(), $"{key}.xml");

    // ── ID generation ─────────────────────────────────────────────────────────

    private static int GetNextId(string ruPath)
    {
        int max = 0;
        foreach (var file in Directory.EnumerateFiles(ruPath, "*.xml"))
        {
            try
            {
                foreach (var attr in XDocument.Load(file).Descendants().SelectMany(e => e.Attributes("Id")))
                    if (int.TryParse(attr.Value, out var n) && n > max) max = n;
            }
            catch { }
        }
        return max + 1;
    }

    // ── XML loading ───────────────────────────────────────────────────────────

    private static string LoadCourseJson(string path)
    {
        var doc = XDocument.Load(path);
        var ce = doc.Root!;

        var dto = new CourseDto
        {
            Id          = (int?)ce.Attribute("Id") ?? 0,
            Key         = (string?)ce.Attribute("Key") ?? "",
            OriginalKey = (string?)ce.Attribute("Key") ?? "",
            Title       = (string?)ce.Attribute("Title") ?? "",
            Description = (string?)ce.Attribute("Description") ?? "",
            Color       = (string?)ce.Attribute("Color") ?? "#6366f1",
            IsActive    = (bool?)ce.Attribute("IsActive") ?? true,
        };

        foreach (var me in ce.Element("Modules")?.Elements("Module") ?? Enumerable.Empty<XElement>())
        {
            var mod = new ModuleDto
            {
                Id          = (int?)me.Attribute("Id") ?? 0,
                Key         = (string?)me.Attribute("Key") ?? "",
                Title       = (string?)me.Attribute("Title") ?? "",
                Description = (string?)me.Attribute("Description") ?? "",
                Difficulty  = (string?)me.Attribute("Difficulty") ?? "Easy",
                Order       = (int?)me.Attribute("Order") ?? 0,
                IsActive    = (bool?)me.Attribute("IsActive") ?? true,
                HasGame     = (bool?)me.Attribute("HasGame") ?? false,
                GameKey     = (string?)me.Attribute("GameKey") ?? "",
            };

            foreach (var le in me.Element("Lessons")?.Elements("Lesson") ?? Enumerable.Empty<XElement>())
            {
                var lesson = new LessonDto
                {
                    Id         = (int?)le.Attribute("Id") ?? 0,
                    Key        = (string?)le.Attribute("Key") ?? "",
                    Title      = (string?)le.Attribute("Title") ?? "",
                    Difficulty = (string?)le.Attribute("Difficulty") ?? "Easy",
                    Order      = (int?)le.Attribute("Order") ?? 0,
                    IsActive   = (bool?)le.Attribute("IsActive") ?? true,
                };

                foreach (var mat in le.Element("Materials")?.Elements("Material") ?? Enumerable.Empty<XElement>())
                {
                    lesson.Materials.Add(new MaterialDto
                    {
                        Type      = (string?)mat.Attribute("Type") ?? "Video",
                        Title     = (string?)mat.Attribute("Title") ?? "",
                        YoutubeId = (string?)mat.Attribute("YoutubeId") ?? "",
                        FilePath  = (string?)mat.Attribute("FilePath") ?? "",
                    });
                }

                var testEl = le.Element("Test");
                if (testEl != null)
                {
                    var test = new TestDto
                    {
                        PassScore = (int?)testEl.Attribute("PassScore") ?? 80,
                    };

                    foreach (var qe in testEl.Elements("Question"))
                    {
                        var q = new QuestionDto
                        {
                            Type               = (string?)qe.Attribute("Type") ?? "MultipleChoice",
                            Text               = (string?)qe.Element("Text") ?? "",
                            CorrectOptionIndex = (int?)qe.Element("CorrectOptionIndex") ?? 0,
                        };
                        foreach (var opt in qe.Element("Options")?.Elements("Option") ?? Enumerable.Empty<XElement>())
                            q.Options.Add((string?)opt ?? "");
                        test.Questions.Add(q);
                    }

                    lesson.Test = test;
                }

                mod.Lessons.Add(lesson);
            }

            dto.Modules.Add(mod);
        }

        return JsonSerializer.Serialize(dto, JsonOpts());
    }

    // ── XML writing ───────────────────────────────────────────────────────────

    private static XDocument BuildXml(CourseDto dto)
    {
        int order = 0;
        return new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("Course",
                new XAttribute("Id", dto.Id),
                new XAttribute("Key", dto.Key),
                new XAttribute("Title", dto.Title),
                new XAttribute("Description", dto.Description ?? ""),
                new XAttribute("IsActive", dto.IsActive ? "true" : "false"),
                new XAttribute("Color", dto.Color ?? "#6366f1"),
                new XAttribute("Access", "public"),
                new XElement("Modules",
                    dto.Modules.Select(m =>
                    {
                        int lo = 0;
                        return new XElement("Module",
                            new XAttribute("Id", m.Id),
                            new XAttribute("Key", m.Key),
                            new XAttribute("CourseId", dto.Id),
                            new XAttribute("Title", m.Title ?? ""),
                            new XAttribute("Description", m.Description ?? ""),
                            new XAttribute("Difficulty", m.Difficulty ?? "Easy"),
                            new XAttribute("Order", ++order),
                            new XAttribute("IsActive", m.IsActive ? "true" : "false"),
                            new XAttribute("HasGame", m.HasGame ? "true" : "false"),
                            m.HasGame && !string.IsNullOrEmpty(m.GameKey) ? new XAttribute("GameKey", m.GameKey) : null,
                            new XElement("Lessons",
                                m.Lessons.Select(l => new XElement("Lesson",
                                    new XAttribute("Id", l.Id),
                                    new XAttribute("Key", l.Key),
                                    new XAttribute("ModuleId", m.Id),
                                    new XAttribute("Title", l.Title ?? ""),
                                    new XAttribute("Difficulty", l.Difficulty ?? "Easy"),
                                    new XAttribute("Order", ++lo),
                                    new XAttribute("IsActive", l.IsActive ? "true" : "false"),
                                    new XElement("Materials",
                                        l.Materials.Select(mat =>
                                        {
                                            var el = new XElement("Material",
                                                new XAttribute("Type", mat.Type ?? "Video"),
                                                new XAttribute("Title", mat.Title ?? ""));
                                            if (mat.Type == "Video" && !string.IsNullOrEmpty(mat.YoutubeId))
                                                el.Add(new XAttribute("YoutubeId", mat.YoutubeId));
                                            else if (!string.IsNullOrEmpty(mat.FilePath))
                                                el.Add(new XAttribute("FilePath", mat.FilePath));
                                            return el;
                                        })
                                    ),
                                    l.Test != null && l.Test.Questions.Count > 0
                                        ? new XElement("Test",
                                            new XAttribute("PassScore", l.Test.PassScore),
                                            l.Test.Questions.Select(q => new XElement("Question",
                                                new XAttribute("Type", q.Type ?? "MultipleChoice"),
                                                new XElement("Text", q.Text ?? ""),
                                                q.Type != "ShortAnswer" ? new XElement("Options",
                                                    q.Options.Select(o => new XElement("Option", o))
                                                ) : null,
                                                q.Type != "ShortAnswer" ? new XElement("CorrectOptionIndex", q.CorrectOptionIndex) : null
                                            ))
                                          )
                                        : null
                                ))
                            )
                        );
                    })
                )
            )
        );
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string SanitizeKey(string key) =>
        Regex.Replace(key.ToLower().Trim().Replace(" ", "_"), @"[^a-z0-9_\-]", "");

    private static JsonSerializerOptions JsonOpts() => new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition      = JsonIgnoreCondition.Never,
        WriteIndented               = false,
    };

    // ── DTOs ──────────────────────────────────────────────────────────────────

    public class CourseDto
    {
        public int Id { get; set; }
        public string Key { get; set; } = "";
        public string OriginalKey { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Color { get; set; } = "#6366f1";
        public bool IsActive { get; set; } = true;
        public List<ModuleDto> Modules { get; set; } = new();
    }

    public class ModuleDto
    {
        public int Id { get; set; }
        public string Key { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Difficulty { get; set; } = "Easy";
        public int Order { get; set; }
        public bool IsActive { get; set; } = true;
        public bool HasGame { get; set; }
        public string GameKey { get; set; } = "";
        public List<LessonDto> Lessons { get; set; } = new();
    }

    public class LessonDto
    {
        public int Id { get; set; }
        public string Key { get; set; } = "";
        public string Title { get; set; } = "";
        public string Difficulty { get; set; } = "Easy";
        public int Order { get; set; }
        public bool IsActive { get; set; } = true;
        public List<MaterialDto> Materials { get; set; } = new();
        public TestDto? Test { get; set; }
    }

    public class MaterialDto
    {
        public string Type { get; set; } = "Video";
        public string Title { get; set; } = "";
        public string YoutubeId { get; set; } = "";
        public string FilePath { get; set; } = "";
    }

    public class TestDto
    {
        public int PassScore { get; set; } = 80;
        public List<QuestionDto> Questions { get; set; } = new();
    }

    public class QuestionDto
    {
        public string Type { get; set; } = "MultipleChoice";
        public string Text { get; set; } = "";
        public List<string> Options { get; set; } = new();
        public int CorrectOptionIndex { get; set; }
    }
}

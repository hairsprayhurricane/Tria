using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Tria.Models;
using Tria.Services;

namespace Tria.Pages.Lessons;

[Authorize(Roles = "Admin,Student")]
public class LessonVideoModel : PageModel
{
    private readonly ILearningService _learning;

    public Lesson? Lesson { get; set; }
    public CourseModule? Module { get; set; }
    public Course? Course { get; set; }
    public LessonMaterial? Material { get; set; }
    public int MaterialIndex { get; set; }
    public string EmbedSrc { get; private set; } = "";
    public string WatchUrl { get; private set; } = "";

    public LessonVideoModel(ILearningService learning)
    {
        _learning = learning;
    }

    public IActionResult OnGet(int lessonId, int materialIndex)
    {
        Lesson = _learning.GetLessonById(lessonId);
        if (Lesson == null) return NotFound();

        if (materialIndex < 0 || materialIndex >= Lesson.Materials.Count) return NotFound();

        Material = Lesson.Materials[materialIndex];
        if (Material.Type != "Video") return NotFound();

        Module = _learning.GetModuleById(Lesson.ModuleId);
        if (Module == null) return NotFound();

        Course = _learning.GetCourseById(Module.CourseId);
        if (Course == null) return NotFound();

        MaterialIndex = materialIndex;
        EmbedSrc = BuildEmbedSrc(Material);
        WatchUrl = Material.VideoSource == "VKVideo" ? (Material.EmbedUrl ?? "") : "";
        return Page();
    }

    private static string BuildEmbedSrc(LessonMaterial mat) => mat.VideoSource switch
    {
        "RuTube"  => ConvertRuTube(mat.EmbedUrl ?? ""),
        "VKVideo" => ConvertVk(mat.EmbedUrl ?? ""),
        _          => "https://www.youtube.com/embed/" + ExtractYoutubeId(mat.YoutubeId ?? ""),
    };

    private static string ExtractYoutubeId(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var m = Regex.Match(input, @"(?:youtu\.be/|youtube\.com/(?:watch\?v=|embed/|v/))([a-zA-Z0-9_-]{11})");
        return m.Success ? m.Groups[1].Value : input;
    }

    private static string ConvertRuTube(string url)
    {
        var m = Regex.Match(url, @"rutube\.ru/video/([a-zA-Z0-9]+)");
        if (m.Success) return $"https://rutube.ru/play/embed/{m.Groups[1].Value}/";
        return url; // already embed or unknown format
    }

    private static string ConvertVk(string url)
    {
        // Already an embed URL (with or without hash) — use as-is
        if (url.Contains("video_ext.php")) return url;
        // Convert watch URL → vkvideo.ru embed (works for public videos)
        var m = Regex.Match(url, @"video(-?\d+)_(\d+)");
        if (m.Success)
            return $"https://vkvideo.ru/video_ext.php?oid={m.Groups[1].Value}&id={m.Groups[2].Value}";
        return url;
    }
}

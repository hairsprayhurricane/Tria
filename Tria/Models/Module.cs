using System.ComponentModel.DataAnnotations.Schema;
using Tria.Models;

namespace Tria.Models;

public class Module
{
    public int Id { get; set; }
    public string Key { get; set; } = null!;
    public int BlockId { get; set; }
    public string Title { get; set; } = null!;
    public string Type { get; set; } = null!;
    public int Order { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? YoutubeId { get; set; }
    public string? VideoDescription { get; set; }

    public string? ContentJson { get; set; }
    [NotMapped]
    public QuizContent? Quiz { get; set; }

    public LearningBlock Block { get; set; } = null!;
    public ICollection<UserModuleProgress> UserProgress { get; set; } = new List<UserModuleProgress>();
}
